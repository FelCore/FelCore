// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

namespace Common
{
    public static class CommonDefs
    {
        public const LocaleConstant DEFAULT_LOCALE = LocaleConstant.LOCALE_enUS;
        public const int MAX_ACCOUNT_TUTORIAL_VALUES = 8;

        public const double M_PI = 3.14159265358979323846;
        public const double M_PI_4 = 0.785398163397448309616;

        private static string[] localeNames = new string[9] {
            "enUS",
            "koKR",
            "frFR",
            "deDE",
            "zhCN",
            "zhTW",
            "esES",
            "esMX",
            "ruRU"
        };

        public static LocaleConstant GetLocaleByName(string name)
        {
            for(uint i = 0; i < (uint)LocaleConstant.TOTAL_LOCALES; ++i)
                if(name == localeNames[i])
                    return (LocaleConstant)i;

            return DEFAULT_LOCALE;                                     // including enGB case
        }
    }
}
