using Microsoft.Win32;
using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using static CraftApp.MainWindow;

namespace CraftApp
{
    public partial class MainWindow : Window
    {
        private bool allowMultiple = true;
        private List<Item> m_titleGoods = new List<Item>();
        private List<Item> m_titleWeapons = new List<Item>();
        private DataTable m_equipMtrlSetParamCsv = null;
        private DataTable m_shopLineupParam_RecipeCsv = null;
        private List<NamedShopLineupParam_RecipeSet> m_namedShopLineupParam_Recipe_Consumables_Offensive = new List<NamedShopLineupParam_RecipeSet>();
        private List<NamedShopLineupParam_RecipeSet> m_namedShopLineupParam_Recipe_Consumables_Support = new List<NamedShopLineupParam_RecipeSet>();
        private List<NamedShopLineupParam_RecipeSet> m_namedShopLineupParam_Recipe_Consumables_Defensive = new List<NamedShopLineupParam_RecipeSet>();
        private List<NamedShopLineupParam_RecipeSet> m_namedShopLineupParam_Recipe_Consumables_Misc = new List<NamedShopLineupParam_RecipeSet>();
        private List<NamedShopLineupParam_RecipeSet> m_namedShopLineupParam_Recipe_Ingredients = new List<NamedShopLineupParam_RecipeSet>();
        private StreamWriter writer;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = allowMultiple; // Allow selecting multiple files only if allowMultiple is false
            bool? result = openFileDialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                // Process selected file(s)
                string[] selectedFiles = openFileDialog.FileNames;
                BrowseFiles_Load(selectedFiles);
            }
        }
        private void BrowseFiles_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && (allowMultiple && files.Length == 1 || !allowMultiple))
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        private void BrowseFiles_Drop(object sender, DragEventArgs e)
        {
            string[] droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedFiles != null && (!allowMultiple && droppedFiles.Length == 1 || allowMultiple))
            {
                BrowseFiles_Load(droppedFiles);
            }
        }
 
        private void BrowseFiles_Load(string[] selectedFiles)
        {
            foreach (string fileName in selectedFiles)
            {
                if (fileName.EndsWith(".json"))
                {
                    if (fileName.Contains("TitleGoods") || fileName.Contains("TitleWeapons"))
                    {
                        string jsonString = File.ReadAllText(fileName);


                        // Deserialize into the temporary Root structure
                        Root root = JsonSerializer.Deserialize<Root>(jsonString);

                        // Convert Root to FmgContainer
                        FmgContainer fmgFile = ConvertToFmgContainer(root);

                        List<string> excludeTitleList = new List<string> { "%null%", "", "[ERROR]", null };
                        fmgFile.Entries.RemoveAll(item => excludeTitleList.Contains(item.Text));
                        if (fileName.Contains("TitleGoods")){
                            if (m_titleGoods.Count > 0)
                            {
                                m_titleGoods.AddRange(fmgFile.Entries);
                            }
                            else
                                m_titleGoods = fmgFile.Entries;
                        }
                        else{
                            if (m_titleWeapons.Count > 0)
                            {
                                m_titleWeapons.AddRange(fmgFile.Entries);
                            }
                            else
                                m_titleWeapons = fmgFile.Entries;
                        }
                    }


                }
                else if (fileName.Contains("EquipMtrlSetParam"))
                {
                    m_equipMtrlSetParamCsv = ReadCsv(fileName);
                }
                else if (fileName.Contains("ShopLineupParam_Recipe") && m_titleGoods != null)
                {
                    m_shopLineupParam_RecipeCsv = ReadCsv(fileName);
                }
                else
                {
                    // file not fitting reqs
                }
                // each file
            }
            if (m_titleGoods != null)
                TitleGoods.IsChecked = true;
            if (m_titleWeapons != null)
                TitleWeapons.IsChecked = true;
            if (m_equipMtrlSetParamCsv != null)
                EquipMtrlSetParam.IsChecked = true;
            if (m_shopLineupParam_RecipeCsv != null)
                ShopLineupParam_Recipe.IsChecked = true;
            // after all files are looped through            
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
        // Create copies of our existing CSVs so we can manipulate them without editing original
        // Consider keeping other columns if ERR decides to consider crafting from non-Goods
        DataTable _equipMtrlSetParamCsv = KeepColumns(m_equipMtrlSetParamCsv, new string[] { "ID", "materialId01", "materialId02", "materialId03",
                        "materialId04", "materialId05", "materialId06", "itemNum01", "itemNum02", "itemNum03", "itemNum04", "itemNum05", "itemNum06", });
            DataTable _shopLineupParam_RecipeCsv = KeepColumns(m_shopLineupParam_RecipeCsv, new string[] { "ID", "Name", "equipId", "setNum", "mtrlId", "equipType" });
            List<EquipMtrlSetParamRow> equipMtrlSetParam = new List<EquipMtrlSetParamRow>();
            List<ShopLineupParam_RecipeRow> shopLineupParam_Recipe = new List<ShopLineupParam_RecipeRow>();

            // Go through all rows in equipMtrlSetParamCsv
            foreach (DataRow row in _equipMtrlSetParamCsv.Rows)
            {
                // Get the ID of the individual rows
                EquipMtrlSetParamRow equipMtrlSetParamRow = new EquipMtrlSetParamRow(Convert.ToInt32(row["ID"]));
                // loop through the headers
                foreach (DataColumn column in _equipMtrlSetParamCsv.Columns)
                {
                    // Go through all columns of the row, continue if hit 'MaterialId'
                    if (column.ColumnName.StartsWith("materialId"))
                    {
                        // skip column if empty
                        if (row[column].ToString() == "-1" || row[column].ToString() == null)
                            continue;

                        // Extract the index (last 2 digits) from the column name
                        string indexString = column.ColumnName.Substring(column.ColumnName.Length - 2);
                        // Create a corresponding itemNum
                        string itemNumColumn = "itemNum" + indexString;

                        int itemID = Convert.ToInt32(row[column]);
                        equipMtrlSetParamRow.materialName.Add(m_titleGoods.Find(item => item.IDList.Contains(itemID))?.Text);
                        equipMtrlSetParamRow.quantity.Add(Convert.ToInt32(row[itemNumColumn]));
                    }
                    // each column
                }
                // after all columns / each row
                if (equipMtrlSetParamRow.materialName.Count > 0)
                    equipMtrlSetParam.Add(equipMtrlSetParamRow);
            }
            foreach (DataRow row in _shopLineupParam_RecipeCsv.Rows)
            {
                    int equipType = Convert.ToInt32(row["equipType"]); // weapon or goods
                    int id = Convert.ToInt32(row["equipId"]); // id of item crafted
                    string itemName;
                    if (equipType == 0)
                        itemName = m_titleWeapons.Find(item => item.IDList.Contains(id))?.Text;
                    else
                        itemName = m_titleGoods.Find(item => item.IDList.Contains(id))?.Text;

                    if (itemName != null)
                    {
                        row["Name"] = itemName;
                    }
            }
            _shopLineupParam_RecipeCsv = KeepColumns(_shopLineupParam_RecipeCsv, new string[] { "ID", "Name", "setNum", "mtrlId" });

            List<string> exclusionList = new List<string> { "[ERROR]", "Drawstring", "Fletched", "Roped" };
            for (int i = _shopLineupParam_RecipeCsv.Rows.Count - 1; i >= 0; i--)
            {
                string name = _shopLineupParam_RecipeCsv.Rows[i]["Name"].ToString();
                if (exclusionList.Any(exclusion => name.Contains(exclusion)))
                {
                    _shopLineupParam_RecipeCsv.Rows.RemoveAt(i);
                }
            }
            foreach (DataRow row in _shopLineupParam_RecipeCsv.Rows)
            {
                int quantity = Convert.ToInt32(row[2]);
                int equipMtrlSetParamID = Convert.ToInt32(row[3].ToString());
                ShopLineupParam_RecipeRow shopLineupParam_RecipeRow = new ShopLineupParam_RecipeRow(Convert.ToInt32(row[0].ToString()));
                shopLineupParam_RecipeRow.Name = row[1].ToString();
                shopLineupParam_RecipeRow.quantity = quantity;
                shopLineupParam_RecipeRow.equipMtrlSetParam = equipMtrlSetParam.Find(row => row.ID == equipMtrlSetParamID);
                if (shopLineupParam_RecipeRow.Name != "")
                    shopLineupParam_Recipe.Add(shopLineupParam_RecipeRow);
            }
            shopLineupParam_Recipe = shopLineupParam_Recipe.Distinct().ToList();
            // Spoilers? Stuff that dodged filtering?
            int[] excludeList = new int[] { 30203, 30204, 32009 };
            RemoveRangeFromListByID(ref shopLineupParam_Recipe, excludeList);
            ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Smithing Stone"); // Remove Smithing Stones, maybe later
            //ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Golden Rune");
            //ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Lord's Rune");
            ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Furlcalling Finger Remedy");
            m_namedShopLineupParam_Recipe_Consumables_Offensive.Add(new NamedShopLineupParam_RecipeSet("Hefty Pots", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Hefty "))); // Hefty first so only Throwing Pots are left
            m_namedShopLineupParam_Recipe_Consumables_Offensive.Add(new NamedShopLineupParam_RecipeSet("Throwing Pots", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Pot")));
            (m_namedShopLineupParam_Recipe_Consumables_Offensive[0], m_namedShopLineupParam_Recipe_Consumables_Offensive[1]) = (m_namedShopLineupParam_Recipe_Consumables_Offensive[1], m_namedShopLineupParam_Recipe_Consumables_Offensive[0]); // TIL IDE0180
            m_namedShopLineupParam_Recipe_Consumables_Offensive.Add(new NamedShopLineupParam_RecipeSet("Arrows", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Arrow")));
            m_namedShopLineupParam_Recipe_Consumables_Offensive.Add(new NamedShopLineupParam_RecipeSet("Bolts", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Bolt")));

            // thrown items
            var offensiveThrown = new NamedShopLineupParam_RecipeSet("Throwing Items", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Dart"));
            offensiveThrown.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Chakram"));
            offensiveThrown.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Harpoon"));
            m_namedShopLineupParam_Recipe_Consumables_Offensive.Add(offensiveThrown);

            // Misc
            var offensiveMisc = new NamedShopLineupParam_RecipeSet("Misc", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Surging Frenzied Flame"));
            offensiveMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Spritestone"));
            offensiveMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Fire Coil"));
            offensiveMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Glinting Nail"));
            offensiveMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Call of Tibia"));
            offensiveMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Branch"));
            offensiveMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Sanctified Stone"));
            offensiveMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Weighty Stone"));
            offensiveMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Cuckoo Glintstone"));
            m_namedShopLineupParam_Recipe_Consumables_Offensive.Add(offensiveMisc);


            m_namedShopLineupParam_Recipe_Consumables_Support.Add(new NamedShopLineupParam_RecipeSet("Aromatics", ExtractAndRemoveRangeFromListByID(ref shopLineupParam_Recipe, 30900, 30999))); // Aromatic, Elixirs, Spraymists
            m_namedShopLineupParam_Recipe_Consumables_Support.Add(new NamedShopLineupParam_RecipeSet("Greases", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Grease")));
            m_namedShopLineupParam_Recipe_Consumables_Support.Add(new NamedShopLineupParam_RecipeSet("Raisins", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Raisin")));

            // Misc
            var supportMisc = new NamedShopLineupParam_RecipeSet("Misc", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Turtle Neck"));
            supportMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Golden Vow"));
            supportMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Fowl Foot"));
            supportMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Flesh"));
            supportMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Warming Stone"));
            supportMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Sunwarmth Stone"));
            supportMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Frenzyflame Stone"));
            supportMisc.RecipeSet.AddRange(ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, "Fingerprint Nostrum"));

            m_namedShopLineupParam_Recipe_Consumables_Defensive.Add(new NamedShopLineupParam_RecipeSet("Livers", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Liver")));
            m_namedShopLineupParam_Recipe_Consumables_Defensive.Add(new NamedShopLineupParam_RecipeSet("Cured Meat", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Cured Meat")));
            m_namedShopLineupParam_Recipe_Consumables_Defensive.Add(new NamedShopLineupParam_RecipeSet("Boluses", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Boluses")));

            m_namedShopLineupParam_Recipe_Ingredients.Add(new NamedShopLineupParam_RecipeSet("Alchemics", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Alchemic")));
            m_namedShopLineupParam_Recipe_Ingredients.Add(new NamedShopLineupParam_RecipeSet("Substances", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Substance")));
            m_namedShopLineupParam_Recipe_Ingredients.Add(new NamedShopLineupParam_RecipeSet("Extracts", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Extract")));
            m_namedShopLineupParam_Recipe_Ingredients.Add(new NamedShopLineupParam_RecipeSet("Amalgams", ExtractAndRemoveFromListByName(ref shopLineupParam_Recipe, " Amalgam")));
            m_namedShopLineupParam_Recipe_Ingredients = m_namedShopLineupParam_Recipe_Ingredients.Distinct().ToList();

            m_namedShopLineupParam_Recipe_Consumables_Misc.Add(new NamedShopLineupParam_RecipeSet("Misc", shopLineupParam_Recipe));

            using (writer = new StreamWriter("Crafting-Remainder.txt"))
            {
                foreach(ShopLineupParam_RecipeRow row in shopLineupParam_Recipe)
                {
                    writer.WriteLine(row.Name);
                }
                writer.Close();
            }
            WriteMainTab();
            Environment.Exit(0);

        }
        private void WriteMainTab()
        {
            using (writer = new StreamWriter("Crafting-Wiki.txt"))
            {
                writer.WriteLine("<center>");
                if (m_namedShopLineupParam_Recipe_Consumables_Offensive != null || m_namedShopLineupParam_Recipe_Ingredients != null)
                {
                    OpenTabber(); 
                    CreateTab("Consumables");
                    OpenTabber();
                    if (m_namedShopLineupParam_Recipe_Consumables_Offensive != null)
                    {
                        CreateTab("Offensive");
                        WriteConsumableTab(m_namedShopLineupParam_Recipe_Consumables_Offensive);
                        CloseTab();
                    }
                    if (m_namedShopLineupParam_Recipe_Consumables_Support != null)
                    {
                        CreateTab("Support");
                        WriteConsumableTab(m_namedShopLineupParam_Recipe_Consumables_Support);
                        CloseTab();
                    }
                    if (m_namedShopLineupParam_Recipe_Consumables_Defensive != null)
                    {
                        CreateTab("Defensive");
                        WriteConsumableTab(m_namedShopLineupParam_Recipe_Consumables_Defensive);
                        CloseTab();
                    }
                    if (m_namedShopLineupParam_Recipe_Consumables_Misc != null)
                    {
                        WriteConsumableTab(m_namedShopLineupParam_Recipe_Consumables_Misc, false);
                    }
                    CloseTab();
                    CloseTab();
                    if (m_namedShopLineupParam_Recipe_Ingredients != null)
                    {
                        CreateTab("Ingredients");
                        WriteIngredientTab(m_namedShopLineupParam_Recipe_Ingredients);
                        CloseTab();

                    }
                    CloseTab();
                }

                writer.Close();
            }
        }
        private void WriteConsumableTab(List<NamedShopLineupParam_RecipeSet> namedRecipeSets, bool newTab = true)
        {
            if(newTab)
                OpenTabber();
            foreach (NamedShopLineupParam_RecipeSet namedShopLineupParam_RecipeSet in namedRecipeSets)
            {
                CreateTab(namedShopLineupParam_RecipeSet.Name);
                WriteConsumableSubTab(namedShopLineupParam_RecipeSet.RecipeSet);
                CloseTab();
            }
            if (newTab)
                CloseTab();
        }
        private void WriteIngredientTab(List<NamedShopLineupParam_RecipeSet> namedRecipeSets)
        {
            OpenTabber();
            foreach (NamedShopLineupParam_RecipeSet namedShopLineupParam_RecipeSet in namedRecipeSets)
            {
                CreateTab(namedShopLineupParam_RecipeSet.Name);
                WriteIngredientSubTab(namedShopLineupParam_RecipeSet);
                CloseTab();
            }
            CloseTab();
        }
        private void OpenTabber()
        {
            writer.WriteLine("<div class=\"tabberex\">");
        }
        private void CreateTab(string name)
        {
            writer.Write("<div class=\"tabberex-tab\">\n<span class=\"tabberex-tab-header\" style=\"text-align:center;\">");
            writer.Write(name);
            writer.WriteLine("</span>");
        }
        private void CloseTab() => writer.WriteLine("</div>");
        private void WriteConsumableSubTab(List<ShopLineupParam_RecipeRow> recipeRows)
        {
            OpenTabber();
            foreach (ShopLineupParam_RecipeRow row in recipeRows)
            {
                // Write Tab
                string tabName = "{{TitledItem|" + row.Name + "}}" + "X" + row.quantity;
                CreateTab(tabName);
                WriteConsumableTableFromRow(row.equipMtrlSetParam);
                CloseTab();
            }
            CloseTab();
        }
        private void WriteIngredientSubTab(NamedShopLineupParam_RecipeSet namedShopLineupParam_RecipeSet)
        {
            OpenTabber();
            List<string> uniqueNames = namedShopLineupParam_RecipeSet.RecipeSet.Select(set => set.Name).Distinct().ToList();
            foreach (string uniqueName in uniqueNames)
            {
                string tabName = "{{TitledItem|" + uniqueName + "}}";
                CreateTab(tabName);
                WriteIngredientTableFromRow(namedShopLineupParam_RecipeSet.RecipeSet.Where(set => set.Name == uniqueName).ToList());
                CloseTab();
            }
            CloseTab();
        }
        private void WriteConsumableTableFromRow(EquipMtrlSetParamRow equipMtrlSetParam)
        {
            writer.WriteLine("{|class=\"article-table\" style=\"text-align:center;\")");
            writer.WriteLine("!style=\"width:200px;text-align:center;\"|Required item");
            writer.WriteLine("!style=\"text-align:center;\"|Quantity");
            int index = 0;
            // Write Row
            foreach (string ingredient in equipMtrlSetParam.materialName)
            {
                writer.WriteLine("|-");
                writer.WriteLine("|style=\"text-align:center;\"|{{TitledItem|" + ingredient + "}}");
                writer.WriteLine("|style=\"text-align:center;font-size:20px;font-weight:bold;\"|" + equipMtrlSetParam.quantity[index]);
                index++;
            }
            writer.WriteLine("|}");
        }
        private void WriteIngredientTableFromRow(List<ShopLineupParam_RecipeRow> recipeRows)
        {
            writer.WriteLine("{|class=\"article-table\" style=\"text-align:center;\")");
            writer.WriteLine("!style=\"text-align:center;\"|Quantity");
            writer.WriteLine("!style=\"width:200px;text-align:center;\"|Required item");
            writer.WriteLine("!style=\"text-align:center;\"|Amount crafted");
            // Write Row
            foreach (ShopLineupParam_RecipeRow recipeRow in recipeRows)
            {
                writer.WriteLine("|-");
                writer.WriteLine("|style=\"text-align:center;font-size:20px;font-weight:bold;\"|" + recipeRow.equipMtrlSetParam.quantity[0] + "X");
                writer.WriteLine("|style=\"text-align:center;\"|{{TitledItem|" + recipeRow.equipMtrlSetParam.materialName[0] + "}}");
                writer.WriteLine("|style=\"text-align:center;font-size:20px;font-weight:bold;\"|" + "X" + recipeRow.quantity);
            }
            writer.WriteLine("|}");
        }
        private List<ShopLineupParam_RecipeRow> ExtractAndRemoveRangeFromListByID(ref List<ShopLineupParam_RecipeRow> filterList, int minValue, int maxValue)
        {
            List<ShopLineupParam_RecipeRow> filteredList = filterList.Where(shopLineupParam_Recipe => shopLineupParam_Recipe.ID >= minValue && shopLineupParam_Recipe.ID <= maxValue).ToList();
            filterList.RemoveAll(shopLineupParam_Recipe => shopLineupParam_Recipe.ID >= minValue && shopLineupParam_Recipe.ID <= maxValue);
            filterList.RemoveAll(shopLineupParam_Recipe => shopLineupParam_Recipe.ID >= minValue && shopLineupParam_Recipe.ID <= maxValue);
            return (filteredList);
        }
        private List<ShopLineupParam_RecipeRow> ExtractAndRemoveFromListByName(ref List<ShopLineupParam_RecipeRow> filterList, string filterString)
        {
            List<ShopLineupParam_RecipeRow> filteredList = filterList.Where(shopLineupParam_Recipe => shopLineupParam_Recipe.Name.Contains(filterString)).ToList();
            filterList.RemoveAll(shopLineupParam_Recipe => shopLineupParam_Recipe.Name.Contains(filterString));
            return (filteredList);
        }
        private void RemoveRangeFromListByID(ref List<ShopLineupParam_RecipeRow> filterList, int[] removeIDs)
        {
            foreach (int removeID in removeIDs)
            {
                filterList.RemoveAll(shopLineupParam_Recipe => shopLineupParam_Recipe.ID.Equals(removeID));
            }
        }
        private DataTable KeepColumns(DataTable data, string[] columnsToKeep)
        {
            DataTable newTable = new DataTable();
            foreach (string columnName in columnsToKeep)
            {
                newTable.Columns.Add(columnName, data.Columns[columnName].DataType);
            }
            foreach (DataRow row in data.Rows)
            {
                // Create a new row in the new table
                DataRow newRow = newTable.NewRow();

                // Copy data from each selected column
                foreach (string columnName in columnsToKeep)
                {
                    newRow[columnName] = row[columnName];
                }
                // Add the new row to the new table
                newTable.Rows.Add(newRow);
            }

            return newTable;
        }
        public class NamedShopLineupParam_RecipeSet
        {
            public string Name { get; }
            public List<ShopLineupParam_RecipeRow> RecipeSet { get; }
            public NamedShopLineupParam_RecipeSet(string newName, List<ShopLineupParam_RecipeRow> newRecipeSet)
            {
                this.Name = newName;
                this.RecipeSet = newRecipeSet;
            }
        }
        public class ShopLineupParam_RecipeRow
        {
            public int ID;
            public string Name;
            public int quantity;
            public EquipMtrlSetParamRow equipMtrlSetParam;
            public ShopLineupParam_RecipeRow(int ID)
            {
                this.ID = ID;
                this.Name = "UNK";
                this.quantity = 1;
                this.equipMtrlSetParam = null;
            }
        }
        public class EquipMtrlSetParamRow
        {
            public int ID;
            public List<string> materialName;// { get; }
            public List<int> quantity;// { get; }

            public EquipMtrlSetParamRow(int ID)
            {
                this.ID = ID;
                this.materialName = new List<string>();
                this.quantity = new List<int>();
            }
        }

        public class FmgWrapper
        {
            public int ID { get; set; }
            public Fmg Fmg { get; set; }
        }

        public class Fmg
        {
            public List<Entry> Entries { get; set; }
        }

        public class Entry
        {
            public int ID { get; set; }
            public string Text { get; set; }
        }

        public class Root
        {
            public string Name { get; set; }
            public List<FmgWrapper> FmgWrappers { get; set; }
        }

        public class FmgContainer
        {
            public int FmgID { get; set; }
            public List<Item> Entries { get; set; }
        }

        public class Item
        {
            public string Text { get; set; }
            public List<int> IDList { get; set; }
        }

        private DataTable ReadCsv(string filePath)
        {
            DataTable dataTable = new DataTable();
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string[] headers = reader.ReadLine().Split(',');
                    foreach (string header in headers)
                    {
                        dataTable.Columns.Add(header);
                    }

                    while (!reader.EndOfStream)
                    {
                        string[] values = reader.ReadLine().Split(',');
                        dataTable.Rows.Add(values);
                    }
                }
                return dataTable;
            }
            catch (IOException)
            {
                return null;
            }
        }

        static FmgContainer ConvertToFmgContainer(Root root)
        {
            FmgContainer container = new FmgContainer
            {
                FmgID = root.FmgWrappers.FirstOrDefault()?.ID ?? 0,
                Entries = root.FmgWrappers
                    .SelectMany(wrapper => wrapper.Fmg.Entries.Select(entry => new Item
                    {
                        Text = entry.Text,
                        IDList = new List<int> { entry.ID }
                    }))
                    .ToList()
            };

            return container;
        }
    }
}
