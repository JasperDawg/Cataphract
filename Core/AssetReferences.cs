#pragma warning disable CS8981

namespace Cataphract.Core;

// ReSharper disable InconsistentNaming
internal static class AssetReferences
{
    public static class icon_small
    {
        public const string KEY = "Cataphract/icon_small";

        public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

        private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
    }

    public static class icon
    {
        public const string KEY = "Cataphract/icon";

        public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

        private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
    }

    public static class Assets
    {
        public static class ModLogo
        {
            public const string KEY = "Cataphract/Assets/ModLogo";

            public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

            private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
        }

        public static class Shaders
        {
            public static class LiquidMask
            {
                public sealed class Parameters : IShaderParameters
                {
                    public void Apply(Microsoft.Xna.Framework.Graphics.EffectParameterCollection parameters)
                    {
                    }
                }

                public const string KEY = "Cataphract/Assets/Shaders/LiquidMask";

                public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect> Asset => lazy.Value;

                private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Effect>(KEY, ReLogic.Content.AssetRequestMode.ImmediateLoad));

                public static WrapperShaderData<Parameters> CreatePanelShader()
                {
                    return new WrapperShaderData<Parameters>(Asset, "PanelShader");
                }
            }
        }

        public static class Images
        {
            public static class Noise
            {
                public static class Noise1
                {
                    public const string KEY = "Cataphract/Assets/Images/Noise/Noise1";

                    public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

                    private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
                }
            }

            public static class Content
            {
                public static class Items
                {
                    public static class Potions
                    {
                        public static class UnstableRecallium
                        {
                            public const string KEY = "Cataphract/Assets/Images/Content/Items/Potions/UnstableRecallium";

                            public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

                            private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
                        }

                        public static class WarpGeode
                        {
                            public const string KEY = "Cataphract/Assets/Images/Content/Items/Potions/WarpGeode";

                            public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

                            private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
                        }
                    }
                }
            }
        }

        public static class Audio
        {
            public static class Misc
            {
                public static class UnstableRecall_Arrive
                {
                    public const string KEY = "Cataphract/Assets/Audio/Misc/UnstableRecall_Arrive";

                    public static Terraria.Audio.SoundStyle Asset => new Terraria.Audio.SoundStyle("Cataphract/Assets/Audio/Misc/UnstableRecall_Arrive");
                }

                public static class UnstableRecall_Channel
                {
                    public const string KEY = "Cataphract/Assets/Audio/Misc/UnstableRecall_Channel";

                    public static Terraria.Audio.SoundStyle Asset => new Terraria.Audio.SoundStyle("Cataphract/Assets/Audio/Misc/UnstableRecall_Channel");
                }
            }
        }
    }
}