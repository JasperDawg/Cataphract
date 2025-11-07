using Terraria.ModLoader;
using Terraria.ID;
using Terraria.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Cataphract.Core;
using System.Diagnostics;
using Cataphract.Common.Rendering;
using Terraria.GameContent;
using System;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using System.Collections.Generic;
using Terraria.ModLoader.IO;
using Terraria.Localization;
using Terraria.GameContent.ItemDropRules;
using System.Linq;
using Cataphract.Common;

namespace Cataphract.Content.Items
{
    public class UnstableRecallium : ModItem
    {
        public override string Texture => Assets.Images.Content.Items.Potions.UnstableRecallium.KEY;
        public override void SetStaticDefaults()
        {
            GameShaders.Armor.BindShader(Item.type,
            new ArmorShaderData(Assets.Shaders.Misc.TestArmorShader.Asset, "ArmorBasic") // Be sure to update the effect path and pass name here.
            );
            base.SetStaticDefaults();
        }
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 180;
            Item.useTime = 180;
            Item.consumable = true;
            Item.healLife = 100;
            Item.UseSound = Assets.Audio.Misc.UnstableRecall_Channel.Asset;
            Item.rare = ItemRarityID.Orange;
        }
        ref WrapperShaderData<Assets.Shaders.Misc.InversePulse.Parameters>? pulse => ref UnstableRecalliumEffectLoader._inversePulseShader;
        ref WrapperShaderData<Assets.Shaders.Misc.WarpBloom.Parameters>? bloom => ref UnstableRecalliumEffectLoader._bloomShader;
        void DrawPotion(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale, bool inWorld = false, int effectScale = 2)
        {
            Debug.Assert(pulse is not null);
            Debug.Assert(bloom is not null);


            var noiseSize = new Vector2(Assets.Images.Noise.Noise1.Asset.Width(), Assets.Images.Noise.Noise1.Asset.Height());

            pulse.Parameters.uTime = Main.GlobalTimeWrappedHourly;
            pulse.Parameters.uScale = 2f;
            pulse.Parameters.uSource = new Vector4(frame.Width, frame.Height, 0, 0);
            pulse.Apply();

            spriteBatch.End(out var ss);
            bloom.Parameters.uTime = Main.GlobalTimeWrappedHourly;
            bloom.Parameters.uSource = new Vector4(frame.Width, frame.Height, 0, 0);
            bloom.Apply();
            Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.Default,
            RasterizerState.CullNone,
            bloom.Shader,
            inWorld ? Main.GameViewMatrix.EffectMatrix : Main.UIScaleMatrix
            );

            spriteBatch.Draw(TextureAssets.Item[Item.type].Value, new Vector2(position.X, position.Y), frame, drawColor, 0f + MathF.Sin(Main.GlobalTimeWrappedHourly) * (MathHelper.Pi / 16), origin, scale, SpriteEffects.None, 0f);

            spriteBatch.Restart(ss);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            DrawPotion(spriteBatch, position + new Vector2(0, 2), frame, drawColor, itemColor, origin, scale, effectScale: 3);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Main.GetItemDrawFrame(Item.type, out var itemTexture, out var itemFrame);
            Vector2 drawOrigin = itemFrame.Size() / 2f;
            Vector2 drawPosition = Item.Bottom - Main.screenPosition - new Vector2(0, drawOrigin.Y);
            DrawPotion(spriteBatch, drawPosition, itemFrame, lightColor, alphaColor, drawOrigin, scale, inWorld: true, effectScale: 8);
            return false;
        }

        public override bool? UseItem(Player player)
        {
            player.GetModPlayer<UnstableRecallPlayer>().teleportin = 180;
            return true;
        }
    }

    public class UnstableRecallPlayer : ModPlayer
    {
        public int teleportin;
        bool teleporting;

        const int teleportMax = 180;
        const int maxDist = 1000;
        const int potionsToAdd = 3;
        public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
        {
            if (mediumCoreDeath) return Enumerable.Empty<Item>();
            
            var items = new List<Item>();

            for (int i = 0; i < potionsToAdd; i++)
            {
                items.Add(new Item(ModContent.ItemType<UnstableRecallium>()));
            }

            return items;
        }
        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (teleportin > 0 && teleporting)
            {
                var color = UnstableRecalliumEffectLoader.RecolorGreyscale(new Vector3(1f, 0.5f, 0.2f));
                var colorMultiplied = new Color(color.X, color.Y, color.Z) * 1;
                var dust = Dust.NewDustDirect(Player.position + Main.rand.NextVector2Circular(60f, 60f), Player.width, Player.height, DustID.FireworksRGB, 0f, 0f, 100, colorMultiplied, 1.5f);
                var spawn = new Vector2(Main.spawnTileX * 16 + 8, Main.spawnTileY * 16 - Player.height);
                var dist = 1.0f - Math.Clamp(maxDist / Vector2.Distance(spawn, Player.Center), 0, 1);
                dust.velocity = Player.DirectionTo(new Vector2(Main.spawnTileX * 16 + 8, Main.spawnTileY * 16 - Player.height)) * (teleportMax - teleportin) * dist;

                r /= color.X;
                g /= color.Y;
                b /= color.Z;
                Lighting.AddLight(Player.Center, color * 0.5f);
            }
            base.DrawEffects(drawInfo, ref r, ref g, ref b, ref a, ref fullBright);
        }
        public override void PreUpdate()
        {
            if (teleportin > 0) teleporting = true;

            if (teleportin-- == 0 && teleporting)
            {
                Vector2 spawn = new Vector2(Main.spawnTileX * 16 + 8, Main.spawnTileY * 16 - Player.height);
                if (Vector2.Distance(spawn, Player.Center) > maxDist)
                {
                    Item.NewItem(Player.GetSource_Misc("UnstableRecallium"), spawn, Player.width, Player.height, ModContent.ItemType<WarpGeode>());
                    Main.NewText(Language.GetTextValue(LocalizationReferences.Mods.Cataphract.Items.UnstableRecallium.WarpFar.KEY), Color.OrangeRed);
                }
                Player.Teleport(spawn, TeleportationStyleID.DebugTeleport);
                teleporting = false;
                for (int i = 0; i < 20; i++)
                {
                    Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.FireworksRGB, 0f, 0f, 100, Color.Pink, 1.5f);
                }
                SoundEngine.PlaySound(Assets.Audio.Misc.UnstableRecall_Arrive.Asset, Player.Center);
            }
            base.PreUpdate();
        }
    }

    public class WarpGeode : ModItem
    {
        public override string Texture => Assets.Images.Content.Items.Potions.WarpGeode.KEY;
        bool Initialized = false;
        public uint type;
        private static readonly RenderTargetPool EffectPool = new();

        public override void SetStaticDefaults()
        {
            ItemID.Sets.OpenableBag[Item.type] = true;
            base.SetStaticDefaults();
        }
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.rare = ItemRarityID.Orange;
        }

        public override bool CanRightClick()
        {
            return true;
        }

        public class CustomCondition : IItemDropRuleCondition
        {
            private readonly string _key;
            private readonly Func<bool> _condition;

            public CustomCondition(string key, Func<bool> condition)
            {
                _key = key;
                _condition = condition;
            }

            public bool CanDrop(DropAttemptInfo info)
            {
                bool cond = _condition();
                return cond;
            }

            public bool CanShowItemDropInUI()
            {
                return true;
            }

            public string GetConditionDescription()
            {
                return Language.GetTextValue(_key);
            }
        }

        enum GeodeType
        {
            Shiny,
            Resourceful,
            Enlightening
        }
        public override void RightClick(Player player)
        {
            switch ((GeodeType)type)
            {
                case GeodeType.Shiny:
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int choice = Main.rand.NextFromList(new int[] { ItemID.GoldBar, ItemID.PlatinumBar, ItemID.MeteoriteBar });
                            int amount = Main.rand.Next(5, 20);
                            player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), choice, amount);
                        }
                        break;
                    }
                case GeodeType.Resourceful:
                    {
                        int choice = Main.rand.NextFromList(new int[] { ItemID.ManaCrystal, ItemID.LifeCrystal });
                        player.QuickSpawnItem(player.GetSource_OpenItem(Item.type), choice, 1);
                        break;
                    }
                case GeodeType.Enlightening:
                    {
                        var amount = (int)TimeSpan.FromMinutes(5).TotalSeconds * 60;
                        player.AddBuff(BuffID.Shine, amount);
                        player.AddBuff(BuffID.Spelunker, amount);
                        player.AddBuff(BuffID.NightOwl, amount);
                        break;
                    }
            }
            SoundEngine.TryGetActiveSound(SoundEngine.PlaySound(Assets.Audio.Misc.UnstableRecall_Arrive.Asset), out var sound);
            sound.Pitch += Main.rand.NextFloat(-0.2f, 0.2f);
            base.RightClick(player);
        }

        public override void SaveData(TagCompound tag)
        {
            tag.Add("initialized", Initialized);
            tag.Add("type", type);
            base.SaveData(tag);
        }

        public override void LoadData(TagCompound tag)
        {
            Initialized = tag.Get<bool>("initialized");
            type = tag.Get<uint>("type");
            base.LoadData(tag);
        }

        public override void OnCreated(ItemCreationContext context)
        {
            if (!Initialized)
            {
                type = (uint)Main.rand.Next(3);
                Initialized = true;
            }
            base.OnCreated(context);
        }

        public override bool OnPickup(Player player)
        {
            if (!Initialized)
            {
                type = (uint)Main.rand.Next(3);
                Initialized = true;
            }

            return base.OnPickup(player);
        }

        public override void UpdateInventory(Player player)
        {
            // in case of emergencies
            if (!Initialized)
            {
                type = (uint)Main.rand.Next(3);
                Initialized = true;
            }

            base.UpdateInventory(player);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine("Geode Type", Language.GetTextValue($"Mods.Cataphract.Items.WarpGeode.Types.Type{type}")));
            base.ModifyTooltips(tooltips);
        }
        ref WrapperShaderData<Assets.Shaders.Misc.InversePulse.Parameters>? pulse => ref UnstableRecalliumEffectLoader._inversePulseShader;
        ref WrapperShaderData<Assets.Shaders.Misc.WarpBloom.Parameters>? bloom => ref UnstableRecalliumEffectLoader._bloomShader;
        void DrawGeode(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale, bool inWorld = false, int effectScale = 2)
        {
            Debug.Assert(pulse is not null);
            Debug.Assert(bloom is not null);

            var device = Main.graphics.GraphicsDevice;
            spriteBatch.End(out var ss);

            float time = Main.GlobalTimeWrappedHourly;

            pulse.Parameters.uTime = time;
            pulse.Parameters.uScale = 2f;
            pulse.Parameters.uSource = new Vector4(frame.Width, frame.Height, Main.screenPosition.X, Main.screenPosition.Y);

            Point noiseTargetSize = new(frame.Width * effectScale, frame.Height * effectScale);
                        bloom.Parameters.uTime = time;
            bloom.Parameters.uSource = new Vector4(frame.Width, frame.Height, 0f, 0f);

            int itemTargetWidth = Math.Max(1, (int)MathF.Ceiling(frame.Width * scale));
            int itemTargetHeight = Math.Max(1, (int)MathF.Ceiling(frame.Height * scale));
            Point itemTargetSize = new(itemTargetWidth, itemTargetHeight);
            Vector2 itemCenter = new(itemTargetWidth * 0.5f, itemTargetHeight * 0.5f);
            float itemRotation = MathF.Sin(time * MathHelper.PiOver4);
            using var noiseLease = Main.spriteBatch.DrawWithEffects(
                device,
                EffectPool,
                noiseTargetSize,
                batch =>
                {
                    batch.Draw(
                        Assets.Images.Noise.Noise1.Asset.Value,
                        new Rectangle(0, 0, noiseTargetSize.X, noiseTargetSize.Y),
                        new Rectangle(0, 0, Assets.Images.Noise.Noise1.Asset.Width(), Assets.Images.Noise.Noise1.Asset.Height()),
                        drawColor);
                        
                },
                new[]
                {
                    new EffectChainEntry(
                        pulse.Shader,
                        BlendState.AlphaBlend,
                        SamplerState.PointClamp,
                        DepthStencilState.Default,
                        RasterizerState.CullNone),
                    new EffectChainEntry(
                        bloom.Shader,
                        BlendState.AlphaBlend,
                        SamplerState.PointClamp,
                        DepthStencilState.Default,
                        RasterizerState.CullNone)
                },
                clear: true,
                clearColor: Color.Transparent);



            using var itemLease = Main.spriteBatch.DrawWithEffects(
                device,
                EffectPool,
                itemTargetSize,
                batch =>
                {
                    batch.Draw(
                        TextureAssets.Item[Item.type].Value,
                        itemCenter,
                        frame,
                        Color.White,
                        itemRotation,
                        origin,
                        scale,
                        SpriteEffects.None,
                        0f);
                },
                new[]
                {
                    new EffectChainEntry(
                        bloom.Shader,
                        BlendState.AlphaBlend,
                        SamplerState.PointClamp,
                        DepthStencilState.Default,
                        RasterizerState.CullNone)
                },
                clear: true,
                clearColor: Color.Transparent);

            Matrix transform = inWorld ? Main.GameViewMatrix.EffectMatrix : Main.UIScaleMatrix;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, transform);

            Vector2 offset = new Vector2(noiseTargetSize.X * 0.5f + itemTargetSize.X / effectScale, noiseTargetSize.Y * 0.5f);
            var noiseDestination = new Rectangle((int)position.X - frame.Width * effectScale / 2, (int)position.Y - frame.Height * effectScale / 2, frame.Width * effectScale, frame.Height * effectScale);
            Main.spriteBatch.Draw(noiseLease.Target, noiseDestination, Color.White);
            //spriteBatch.Draw(noiseLease.Target, new Rectangle((int)position.X - frame.Width * effectScale / 2, (int)position.Y - frame.Height * effectScale / 2, frame.Width * effectScale, frame.Height * effectScale), new Rectangle(0, 0, frame.Width, frame.Height / 2), drawColor, 0f, origin, SpriteEffects.None, 0f);

            Vector2 itemOffset = new(MathF.Sin(time) * 5f, MathF.Cos(time * 2f) * 3f);
            Vector2 itemPosition = position + itemOffset - new Vector2(itemTargetSize.X, itemTargetSize.Y) * 0.5f;
            Main.spriteBatch.Draw(itemLease.Target, itemPosition, Color.White);


            spriteBatch.Restart(ss);
        }
        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            DrawGeode(spriteBatch, position + new Vector2(0, 2), frame, drawColor, itemColor, origin, scale, effectScale: 3);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Main.GetItemDrawFrame(Item.type, out var itemTexture, out var itemFrame);
            Vector2 drawOrigin = itemFrame.Size() / 2f;
            Vector2 drawPosition = Item.Bottom - Main.screenPosition - new Vector2(0, drawOrigin.Y);
            DrawGeode(spriteBatch, drawPosition, itemFrame, lightColor, alphaColor, drawOrigin, scale, inWorld: true, effectScale: 8);
            return false;
        }

        public override void PostUpdate()
        {
            Lighting.AddLight(Item.Center, UnstableRecalliumEffectLoader.RecolorGreyscale(new Vector3(1f, 0.5f, 0.2f)) * 0.5f);
            base.PostUpdate();
        }
    }

    file class UnstableRecalliumEffectLoader : ILoadable
    {
        public static WrapperShaderData<Assets.Shaders.Misc.InversePulse.Parameters>? _inversePulseShader;
        public static WrapperShaderData<Assets.Shaders.Misc.WarpBloom.Parameters>? _bloomShader;

        private static Vector3 Pal(float t, Vector3 brightness, Vector3 contrast, Vector3 osc, Vector3 phase)
        {
            return brightness + contrast * new Vector3(
            MathF.Cos(MathHelper.TwoPi * (osc.X * t + phase.X)),
            MathF.Cos(MathHelper.TwoPi * (osc.Y * t + phase.Y)),
            MathF.Cos(MathHelper.TwoPi * (osc.Z * t + phase.Z))
            );
        }

        public static Vector3 RecolorGreyscale(Vector3 rgb)
        {
            float t = MathF.Sin(rgb.X + Main.GlobalTimeWrappedHourly);
            Vector3 brightness = rgb;
            Vector3 contrast = Vector3.Lerp(new Vector3(0.5f), new Vector3(0.7f), (MathF.Sin(Main.GlobalTimeWrappedHourly) + 1f) / 2f);
            Vector3 osc = new Vector3(0.4f, (MathF.Sin(Main.GlobalTimeWrappedHourly) + 1f) / 2f, 0.2f);
            Vector3 phase = new Vector3(0.7f, 0.4f, 0.1f);
            return Pal(t, brightness, contrast, osc, phase);
        }

        void ILoadable.Load(Mod mod)
        {
            _inversePulseShader = Assets.Shaders.Misc.InversePulse.CreateInversePulseShader();
            _bloomShader = Assets.Shaders.Misc.WarpBloom.CreateBloomShader();
            // todo: generalize this to work with anything arbitrarily
            Terraria.DataStructures.On_PlayerDrawLayers.DrawPlayer_RenderAllLayers += static (On_PlayerDrawLayers.orig_DrawPlayer_RenderAllLayers orig, ref PlayerDrawSet drawInfo) =>
            {
                for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
                {
                    if (TextureAssets.Item[ModContent.ItemType<UnstableRecallium>()].Value == drawInfo.DrawDataCache[i].texture)
                    {
                        var drawData = drawInfo.DrawDataCache[i];
                        drawData.shader = GameShaders.Armor.GetShaderIdFromItemId(ModContent.ItemType<UnstableRecallium>());
                        if (drawData.texture != null)
                        {
                            drawInfo.DrawDataCache[i] = drawData;
                        }
                    }
                }
                orig(ref drawInfo);
            };
        }

        void ILoadable.Unload()
        {
            _inversePulseShader = null;
        }
    }
}