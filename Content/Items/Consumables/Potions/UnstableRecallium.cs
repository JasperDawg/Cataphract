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
            Item.useTime = 15;
            Item.consumable = true;
            Item.healLife = 100;
            Item.UseSound = Assets.Audio.Misc.UnstableRecall_Channel.Asset;
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
    }

    public class UnstableRecallPlayer : ModPlayer
    {
        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
        {
            for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
            {
                if (drawInfo.DrawDataCache[i].texture == TextureAssets.Item[ModContent.ItemType<UnstableRecallium>()].Value)
                {
                    var drawData = drawInfo.DrawDataCache[i];
                    drawData.shader = GameShaders.Armor.GetShaderIdFromItemId(ModContent.ItemType<UnstableRecallium>());
                    drawInfo.DrawDataCache[i] = drawData;
                }
            }
            base.ModifyDrawInfo(ref drawInfo);
        }

        public override void ModifyDrawLayerOrdering(IDictionary<PlayerDrawLayer, PlayerDrawLayer.Position> positions)
        {


            base.ModifyDrawLayerOrdering(positions);
        }
    }

    public class WarpGeode : ModItem
    {
        public override string Texture => Assets.Images.Content.Items.Potions.WarpGeode.KEY;
        bool Initialized = false;
        public uint type;
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 32;
            Item.useTime = 15;
            Item.consumable = true;
            Item.healMana = 100;
            Item.UseSound = Assets.Audio.Misc.UnstableRecall_Arrive.Asset;
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


            var noiseSize = new Vector2(Assets.Images.Noise.Noise1.Asset.Width(), Assets.Images.Noise.Noise1.Asset.Height());

            pulse.Parameters.uTime = Main.GlobalTimeWrappedHourly;
            pulse.Parameters.uScale = 2f;
            pulse.Parameters.uSource = new Vector4(frame.Width, frame.Height, 0, 0);
            pulse.Apply();

            spriteBatch.End(out var ss);
            Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.Default,
            RasterizerState.CullNone,
            pulse.Shader,
            inWorld ? Main.GameViewMatrix.EffectMatrix : Main.UIScaleMatrix
            );

            int newscale = effectScale;
            spriteBatch.Draw(Assets.Images.Noise.Noise1.Asset.Value, new Rectangle((int)position.X - frame.Width * newscale / 2, (int)position.Y - frame.Height * newscale / 2, frame.Width * newscale, frame.Height * newscale), new Rectangle(0, 0, (int)noiseSize.X, (int)noiseSize.Y), drawColor, 0f, origin, SpriteEffects.None, 0f);
            spriteBatch.End();

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

            spriteBatch.Draw(TextureAssets.Item[Item.type].Value, new Vector2(position.X + MathF.Sin(Main.GlobalTimeWrappedHourly * 1f) * 5f, position.Y + MathF.Cos(Main.GlobalTimeWrappedHourly * 2f) * 3f), frame, drawColor, 0f + MathF.Sin(Main.GlobalTimeWrappedHourly * MathHelper.PiOver4), origin, scale, SpriteEffects.None, 0f);

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