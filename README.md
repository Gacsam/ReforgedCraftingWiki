Reads TitleGoods.fmg.json and and TitleWeapons.fmg.json
Reads EquipMtrlSetParam.csv and ShopLineupParamRecipe.csv
Matches EquipMtrlSetParam which contain item recipes to ShopLineupParamRecipe which contains craftable items.
Matches TitleGoods/TitleWeapons to ShopLineupParamRecipe to assign names to items within ShopLineupParamRecipe.
Assigns categories within code, allowing separation of items, such as Offensive or Defensive.
Creates a Wiki table code ready to insert into the https://err.fandom.com/wiki/Crafting#Item_Crafting page.

TODO:
- Allow filtering through txt or json files to stop editing code for it
- Automate creating item categories to allow usage within other mods/vanilla
