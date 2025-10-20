using Terraria.Localization;

namespace Cataphract.Core;

// ReSharper disable MemberHidesStaticFromOuterClass
internal static class LocalizationReferences
{
    public static class Mods
    {
        public const string KEY = "Mods";

        public static LocalizedText GetChildText(string childKey)
        {
            return Language.GetText(KEY + '.' + childKey);
        }

        public static string GetChildTextValue(string childKey, params object?[] values)
        {
            return Language.GetTextValue(KEY + '.' + childKey, values);
        }

        public static class Cataphract
        {
            public const string KEY = "Mods.Cataphract";

            public static LocalizedText GetChildText(string childKey)
            {
                return Language.GetText(KEY + '.' + childKey);
            }

            public static string GetChildTextValue(string childKey, params object?[] values)
            {
                return Language.GetTextValue(KEY + '.' + childKey, values);
            }

            public static class Items
            {
                public const string KEY = "Mods.Cataphract.Items";

                public static LocalizedText GetChildText(string childKey)
                {
                    return Language.GetText(KEY + '.' + childKey);
                }

                public static string GetChildTextValue(string childKey, params object?[] values)
                {
                    return Language.GetTextValue(KEY + '.' + childKey, values);
                }

                public static class UnstableRecallium
                {
                    public const string KEY = "Mods.Cataphract.Items.UnstableRecallium";

                    public static LocalizedText GetChildText(string childKey)
                    {
                        return Language.GetText(KEY + '.' + childKey);
                    }

                    public static string GetChildTextValue(string childKey, params object?[] values)
                    {
                        return Language.GetTextValue(KEY + '.' + childKey, values);
                    }

                    public static class DisplayName
                    {
                        public const string KEY = "Mods.Cataphract.Items.UnstableRecallium.DisplayName";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }

                    public static class Tooltip
                    {
                        public const string KEY = "Mods.Cataphract.Items.UnstableRecallium.Tooltip";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }

                    public static class WarpFar
                    {
                        public const string KEY = "Mods.Cataphract.Items.UnstableRecallium.WarpFar";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }
                }

                public static class WarpGeode
                {
                    public const string KEY = "Mods.Cataphract.Items.WarpGeode";

                    public static LocalizedText GetChildText(string childKey)
                    {
                        return Language.GetText(KEY + '.' + childKey);
                    }

                    public static string GetChildTextValue(string childKey, params object?[] values)
                    {
                        return Language.GetTextValue(KEY + '.' + childKey, values);
                    }

                    public static class DisplayName
                    {
                        public const string KEY = "Mods.Cataphract.Items.WarpGeode.DisplayName";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }

                    public static class Tooltip
                    {
                        public const string KEY = "Mods.Cataphract.Items.WarpGeode.Tooltip";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }

                    public static class Types
                    {
                        public const string KEY = "Mods.Cataphract.Items.WarpGeode.Types";

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }

                        public static class Type0
                        {
                            public const string KEY = "Mods.Cataphract.Items.WarpGeode.Types.Type0";
                            public const int ARG_COUNT = 0;

                            public static LocalizedText GetText()
                            {
                                return Language.GetText(KEY);
                            }

                            public static string GetTextValue()
                            {
                                return Language.GetTextValue(KEY);
                            }

                            public static LocalizedText GetChildText(string childKey)
                            {
                                return Language.GetText(KEY + '.' + childKey);
                            }

                            public static string GetChildTextValue(string childKey, params object?[] values)
                            {
                                return Language.GetTextValue(KEY + '.' + childKey, values);
                            }
                        }

                        public static class Type1
                        {
                            public const string KEY = "Mods.Cataphract.Items.WarpGeode.Types.Type1";
                            public const int ARG_COUNT = 0;

                            public static LocalizedText GetText()
                            {
                                return Language.GetText(KEY);
                            }

                            public static string GetTextValue()
                            {
                                return Language.GetTextValue(KEY);
                            }

                            public static LocalizedText GetChildText(string childKey)
                            {
                                return Language.GetText(KEY + '.' + childKey);
                            }

                            public static string GetChildTextValue(string childKey, params object?[] values)
                            {
                                return Language.GetTextValue(KEY + '.' + childKey, values);
                            }
                        }

                        public static class Type2
                        {
                            public const string KEY = "Mods.Cataphract.Items.WarpGeode.Types.Type2";
                            public const int ARG_COUNT = 0;

                            public static LocalizedText GetText()
                            {
                                return Language.GetText(KEY);
                            }

                            public static string GetTextValue()
                            {
                                return Language.GetTextValue(KEY);
                            }

                            public static LocalizedText GetChildText(string childKey)
                            {
                                return Language.GetText(KEY + '.' + childKey);
                            }

                            public static string GetChildTextValue(string childKey, params object?[] values)
                            {
                                return Language.GetTextValue(KEY + '.' + childKey, values);
                            }
                        }
                    }
                }

                public static class NeanderthalBlaster
                {
                    public const string KEY = "Mods.Cataphract.Items.NeanderthalBlaster";

                    public static LocalizedText GetChildText(string childKey)
                    {
                        return Language.GetText(KEY + '.' + childKey);
                    }

                    public static string GetChildTextValue(string childKey, params object?[] values)
                    {
                        return Language.GetTextValue(KEY + '.' + childKey, values);
                    }

                    public static class DisplayName
                    {
                        public const string KEY = "Mods.Cataphract.Items.NeanderthalBlaster.DisplayName";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }

                    public static class Tooltip
                    {
                        public const string KEY = "Mods.Cataphract.Items.NeanderthalBlaster.Tooltip";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }
                }

                public static class ShinyRockSceptor
                {
                    public const string KEY = "Mods.Cataphract.Items.ShinyRockSceptor";

                    public static LocalizedText GetChildText(string childKey)
                    {
                        return Language.GetText(KEY + '.' + childKey);
                    }

                    public static string GetChildTextValue(string childKey, params object?[] values)
                    {
                        return Language.GetTextValue(KEY + '.' + childKey, values);
                    }

                    public static class DisplayName
                    {
                        public const string KEY = "Mods.Cataphract.Items.ShinyRockSceptor.DisplayName";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }

                    public static class Tooltip
                    {
                        public const string KEY = "Mods.Cataphract.Items.ShinyRockSceptor.Tooltip";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }
                }
            }

            public static class Projectiles
            {
                public const string KEY = "Mods.Cataphract.Projectiles";

                public static LocalizedText GetChildText(string childKey)
                {
                    return Language.GetText(KEY + '.' + childKey);
                }

                public static string GetChildTextValue(string childKey, params object?[] values)
                {
                    return Language.GetTextValue(KEY + '.' + childKey, values);
                }

                public static class NeanderthalBlasterProjectile
                {
                    public const string KEY = "Mods.Cataphract.Projectiles.NeanderthalBlasterProjectile";

                    public static LocalizedText GetChildText(string childKey)
                    {
                        return Language.GetText(KEY + '.' + childKey);
                    }

                    public static string GetChildTextValue(string childKey, params object?[] values)
                    {
                        return Language.GetTextValue(KEY + '.' + childKey, values);
                    }

                    public static class DisplayName
                    {
                        public const string KEY = "Mods.Cataphract.Projectiles.NeanderthalBlasterProjectile.DisplayName";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }
                }

                public static class ShinyRockSceptor_Projectile
                {
                    public const string KEY = "Mods.Cataphract.Projectiles.ShinyRockSceptor_Projectile";

                    public static LocalizedText GetChildText(string childKey)
                    {
                        return Language.GetText(KEY + '.' + childKey);
                    }

                    public static string GetChildTextValue(string childKey, params object?[] values)
                    {
                        return Language.GetTextValue(KEY + '.' + childKey, values);
                    }

                    public static class DisplayName
                    {
                        public const string KEY = "Mods.Cataphract.Projectiles.ShinyRockSceptor_Projectile.DisplayName";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }
                }

                public static class ShinyRockSceptor_Hitscan
                {
                    public const string KEY = "Mods.Cataphract.Projectiles.ShinyRockSceptor_Hitscan";

                    public static LocalizedText GetChildText(string childKey)
                    {
                        return Language.GetText(KEY + '.' + childKey);
                    }

                    public static string GetChildTextValue(string childKey, params object?[] values)
                    {
                        return Language.GetTextValue(KEY + '.' + childKey, values);
                    }

                    public static class DisplayName
                    {
                        public const string KEY = "Mods.Cataphract.Projectiles.ShinyRockSceptor_Hitscan.DisplayName";
                        public const int ARG_COUNT = 0;

                        public static LocalizedText GetText()
                        {
                            return Language.GetText(KEY);
                        }

                        public static string GetTextValue()
                        {
                            return Language.GetTextValue(KEY);
                        }

                        public static LocalizedText GetChildText(string childKey)
                        {
                            return Language.GetText(KEY + '.' + childKey);
                        }

                        public static string GetChildTextValue(string childKey, params object?[] values)
                        {
                            return Language.GetTextValue(KEY + '.' + childKey, values);
                        }
                    }
                }
            }
        }
    }
}