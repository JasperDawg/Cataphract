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

            public static class Particles
            {
                public static class Circular
                {
                    public const string KEY = "Cataphract/Assets/Images/Particles/Circular";

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

                    public static class Weapons
                    {
                        public static class Misc
                        {
                            public static class StupidGun
                            {
                                public const string KEY = "Cataphract/Assets/Images/Content/Items/Weapons/Misc/StupidGun";

                                public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

                                private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
                            }

                            public static class StupidGun_Projectiles
                            {
                                public const string KEY = "Cataphract/Assets/Images/Content/Items/Weapons/Misc/StupidGun_Projectiles";

                                public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

                                private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
                            }

                            public static class StupidGun_Critter
                            {
                                public const string KEY = "Cataphract/Assets/Images/Content/Items/Weapons/Misc/StupidGun_Critter";

                                public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> Asset => lazy.Value;

                                private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(KEY));
                            }
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

        public static class Shaders
        {
            public static class Misc
            {
                public static class GaussianBloom
                {
                    public sealed class Parameters : IShaderParameters
                    {
                        public Microsoft.Xna.Framework.Graphics.Texture2D? uImage0 { get; set; }

                        public float uTime { get; set; }

                        public float uHoverIntensity { get; set; }

                        public float uPixel { get; set; }

                        public float uColorResolution { get; set; }

                        public float uGrayness { get; set; }

                        public float uSpeed { get; set; }

                        public float passes { get; set; }

                        public Microsoft.Xna.Framework.Vector4 uSource { get; set; }

                        public Microsoft.Xna.Framework.Vector3 uInColor { get; set; }

                        public void Apply(Microsoft.Xna.Framework.Graphics.EffectParameterCollection parameters)
                        {
                            parameters["uImage0"]?.SetValue(uImage0);
                            parameters["uTime"]?.SetValue(Terraria.Main.GlobalTimeWrappedHourly);
                            parameters["uHoverIntensity"]?.SetValue(uHoverIntensity);
                            parameters["uPixel"]?.SetValue(uPixel);
                            parameters["uColorResolution"]?.SetValue(uColorResolution);
                            parameters["uGrayness"]?.SetValue(uGrayness);
                            parameters["uSpeed"]?.SetValue(uSpeed);
                            parameters["passes"]?.SetValue(passes);
                            parameters["uSource"]?.SetValue(uSource);
                            parameters["uInColor"]?.SetValue(uInColor);
                        }
                    }

                    public const string KEY = "Cataphract/Assets/Shaders/Misc/GaussianBloom";

                    public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect> Asset => lazy.Value;

                    private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Effect>(KEY, ReLogic.Content.AssetRequestMode.ImmediateLoad));

                    public static WrapperShaderData<Parameters> CreateBloomShader()
                    {
                        return new WrapperShaderData<Parameters>(Asset, "BloomShader");
                    }
                }

                public static class InversePulse
                {
                    public sealed class Parameters : IShaderParameters
                    {
                        public Microsoft.Xna.Framework.Graphics.Texture2D? uImage0 { get; set; }

                        public float uTime { get; set; }

                        public float uHoverIntensity { get; set; }

                        public float uScale { get; set; }

                        public float uPixel { get; set; }

                        public float uColorResolution { get; set; }

                        public float uGrayness { get; set; }

                        public float uSpeed { get; set; }

                        public Microsoft.Xna.Framework.Vector4 uSource { get; set; }

                        public Microsoft.Xna.Framework.Vector3 uInColor { get; set; }

                        public void Apply(Microsoft.Xna.Framework.Graphics.EffectParameterCollection parameters)
                        {
                            parameters["uImage0"]?.SetValue(uImage0);
                            parameters["uTime"]?.SetValue(Terraria.Main.GlobalTimeWrappedHourly);
                            parameters["uHoverIntensity"]?.SetValue(uHoverIntensity);
                            parameters["uScale"]?.SetValue(uScale);
                            parameters["uPixel"]?.SetValue(uPixel);
                            parameters["uColorResolution"]?.SetValue(uColorResolution);
                            parameters["uGrayness"]?.SetValue(uGrayness);
                            parameters["uSpeed"]?.SetValue(uSpeed);
                            parameters["uSource"]?.SetValue(uSource);
                            parameters["uInColor"]?.SetValue(uInColor);
                        }
                    }

                    public const string KEY = "Cataphract/Assets/Shaders/Misc/InversePulse";

                    public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect> Asset => lazy.Value;

                    private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Effect>(KEY, ReLogic.Content.AssetRequestMode.ImmediateLoad));

                    public static WrapperShaderData<Parameters> CreateInversePulseShader()
                    {
                        return new WrapperShaderData<Parameters>(Asset, "InversePulseShader");
                    }
                }

                public static class WarpBloom
                {
                    public sealed class Parameters : IShaderParameters
                    {
                        public Microsoft.Xna.Framework.Graphics.Texture2D? uImage0 { get; set; }

                        public float uTime { get; set; }

                        public float uHoverIntensity { get; set; }

                        public float uPixel { get; set; }

                        public float uColorResolution { get; set; }

                        public float uGrayness { get; set; }

                        public float uSpeed { get; set; }

                        public Microsoft.Xna.Framework.Vector4 uSource { get; set; }

                        public Microsoft.Xna.Framework.Vector3 uInColor { get; set; }

                        public void Apply(Microsoft.Xna.Framework.Graphics.EffectParameterCollection parameters)
                        {
                            parameters["uImage0"]?.SetValue(uImage0);
                            parameters["uTime"]?.SetValue(Terraria.Main.GlobalTimeWrappedHourly);
                            parameters["uHoverIntensity"]?.SetValue(uHoverIntensity);
                            parameters["uPixel"]?.SetValue(uPixel);
                            parameters["uColorResolution"]?.SetValue(uColorResolution);
                            parameters["uGrayness"]?.SetValue(uGrayness);
                            parameters["uSpeed"]?.SetValue(uSpeed);
                            parameters["uSource"]?.SetValue(uSource);
                            parameters["uInColor"]?.SetValue(uInColor);
                        }
                    }

                    public const string KEY = "Cataphract/Assets/Shaders/Misc/WarpBloom";

                    public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect> Asset => lazy.Value;

                    private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Effect>(KEY, ReLogic.Content.AssetRequestMode.ImmediateLoad));

                    public static WrapperShaderData<Parameters> CreateBloomShader()
                    {
                        return new WrapperShaderData<Parameters>(Asset, "BloomShader");
                    }
                }

                public static class TestArmorShader
                {
                    public sealed class Parameters : IShaderParameters
                    {
                        public Microsoft.Xna.Framework.Graphics.Texture2D? uImage0 { get; set; }

                        public Microsoft.Xna.Framework.Graphics.Texture2D? uImage1 { get; set; }

                        public Microsoft.Xna.Framework.Vector3 uColor { get; set; }

                        public Microsoft.Xna.Framework.Vector3 uSecondaryColor { get; set; }

                        public float uOpacity { get; set; }

                        public Microsoft.Xna.Framework.Vector2 uTargetPosition { get; set; }

                        public float uSaturation { get; set; }

                        public float uRotation { get; set; }

                        public float uTime { get; set; }

                        public Microsoft.Xna.Framework.Vector4 uSourceRect { get; set; }

                        public Microsoft.Xna.Framework.Vector2 uWorldPosition { get; set; }

                        public float uDirection { get; set; }

                        public Microsoft.Xna.Framework.Vector3 uLightSource { get; set; }

                        public Microsoft.Xna.Framework.Vector2 uImageSize0 { get; set; }

                        public Microsoft.Xna.Framework.Vector2 uImageSize1 { get; set; }

                        public Microsoft.Xna.Framework.Vector4 uLegacyArmorSourceRect { get; set; }

                        public Microsoft.Xna.Framework.Vector2 uLegacyArmorSheetSize { get; set; }

                        public void Apply(Microsoft.Xna.Framework.Graphics.EffectParameterCollection parameters)
                        {
                            parameters["uImage0"]?.SetValue(uImage0);
                            parameters["uImage1"]?.SetValue(uImage1);
                            parameters["uColor"]?.SetValue(uColor);
                            parameters["uSecondaryColor"]?.SetValue(uSecondaryColor);
                            parameters["uOpacity"]?.SetValue(uOpacity);
                            parameters["uTargetPosition"]?.SetValue(uTargetPosition);
                            parameters["uSaturation"]?.SetValue(uSaturation);
                            parameters["uRotation"]?.SetValue(uRotation);
                            parameters["uTime"]?.SetValue(Terraria.Main.GlobalTimeWrappedHourly);
                            parameters["uSourceRect"]?.SetValue(uSourceRect);
                            parameters["uWorldPosition"]?.SetValue(uWorldPosition);
                            parameters["uDirection"]?.SetValue(uDirection);
                            parameters["uLightSource"]?.SetValue(uLightSource);
                            parameters["uImageSize0"]?.SetValue(uImageSize0);
                            parameters["uImageSize1"]?.SetValue(uImageSize1);
                            parameters["uLegacyArmorSourceRect"]?.SetValue(uLegacyArmorSourceRect);
                            parameters["uLegacyArmorSheetSize"]?.SetValue(uLegacyArmorSheetSize);
                        }
                    }

                    public const string KEY = "Cataphract/Assets/Shaders/Misc/TestArmorShader";

                    public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect> Asset => lazy.Value;

                    private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Effect>(KEY, ReLogic.Content.AssetRequestMode.ImmediateLoad));

                    public static WrapperShaderData<Parameters> CreateArmorBasic()
                    {
                        return new WrapperShaderData<Parameters>(Asset, "ArmorBasic");
                    }
                }

                public static class BlasterSDF
                {
                    public sealed class Parameters : IShaderParameters
                    {
                        public Microsoft.Xna.Framework.Graphics.Texture2D? uImage0 { get; set; }

                        public float uTime { get; set; }

                        public float uHoverIntensity { get; set; }

                        public float uPixel { get; set; }

                        public float uColorResolution { get; set; }

                        public float uGrayness { get; set; }

                        public float uSpeed { get; set; }

                        public Microsoft.Xna.Framework.Vector4 uSource { get; set; }

                        public Microsoft.Xna.Framework.Vector3 uInColor { get; set; }

                        public Microsoft.Xna.Framework.Vector4[]? Particles { get; set; }

                        public Microsoft.Xna.Framework.Vector3[]? ParticleColors { get; set; }

                        public void Apply(Microsoft.Xna.Framework.Graphics.EffectParameterCollection parameters)
                        {
                            parameters["uImage0"]?.SetValue(uImage0);
                            parameters["uTime"]?.SetValue(Terraria.Main.GlobalTimeWrappedHourly);
                            parameters["uHoverIntensity"]?.SetValue(uHoverIntensity);
                            parameters["uPixel"]?.SetValue(uPixel);
                            parameters["uColorResolution"]?.SetValue(uColorResolution);
                            parameters["uGrayness"]?.SetValue(uGrayness);
                            parameters["uSpeed"]?.SetValue(uSpeed);
                            parameters["uSource"]?.SetValue(uSource);
                            parameters["uInColor"]?.SetValue(uInColor);
                            parameters["Particles"]?.SetValue(Particles);
                            parameters["ParticleColors"]?.SetValue(ParticleColors);
                        }
                    }

                    public const string KEY = "Cataphract/Assets/Shaders/Misc/BlasterSDF";

                    public static ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect> Asset => lazy.Value;

                    private static readonly System.Lazy<ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Effect>> lazy = new(() => Terraria.ModLoader.ModContent.Request<Microsoft.Xna.Framework.Graphics.Effect>(KEY, ReLogic.Content.AssetRequestMode.ImmediateLoad));

                    public static WrapperShaderData<Parameters> CreateSDFShader()
                    {
                        return new WrapperShaderData<Parameters>(Asset, "SDFShader");
                    }
                }
            }
        }
    }
}