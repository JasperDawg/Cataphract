using Terraria.ModLoader;
using Terraria.ID;
using Terraria.Audio;

namespace Cataphract.Content.Items
{
    public class UnstableRecallium : ModItem
    {
        public override string Texture => Core.AssetReferences.Assets.Images.Content.Items.Potions.UnstableRecallium.KEY;
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.consumable = true;
            Item.healLife = 100;
            Item.UseSound = Core.AssetReferences.Assets.Audio.Misc.UnstableRecall_Channel.Asset;
        }
    }

    public class WarpGeode : ModItem
    {
        public override string Texture => Core.AssetReferences.Assets.Images.Content.Items.Potions.WarpGeode.KEY;
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.consumable = true;
            Item.healMana = 100;
            Item.UseSound = Core.AssetReferences.Assets.Audio.Misc.UnstableRecall_Arrive.Asset;
        }
    }
}