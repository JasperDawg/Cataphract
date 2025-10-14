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

    public static class assets
    {
        public static class ModLogo
        {
            public const string KEY = "Cataphract/assets/ModLogo";

            public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

            private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
        }
    }
}