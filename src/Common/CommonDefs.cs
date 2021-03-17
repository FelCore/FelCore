// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

namespace Common
{
    public enum AccountTypes
    {
        SEC_PLAYER         = 0,
        SEC_GAMEMASTER1    = 1,
        SEC_GAMEMASTER2    = 2,
        SEC_GAMEMASTER3    = 3,
        SEC_ADMINISTRATOR  = 4,
        SEC_SUPERADMIN     = 5,
        SEC_CONSOLE        = 6, // must be always last in list, accounts must have less security level always also
    }

    public enum LocaleConstant : byte
    {
        LOCALE_enUS = 0,
        LOCALE_koKR = 1,
        LOCALE_frFR = 2,
        LOCALE_deDE = 3,
        LOCALE_zhCN = 4,
        LOCALE_zhTW = 5,
        LOCALE_esES = 6,
        LOCALE_esMX = 7,
        LOCALE_ruRU = 8,

        TOTAL_LOCALES
    }

    public enum TimeConstants
    {
        SECOND          = 1,
        MINUTE          = 60,
        HOUR            = MINUTE*60,
        DAY             = HOUR*24,
        WEEK            = DAY*7,
        MONTH           = DAY*30,
        YEAR            = MONTH*12,
        IN_MILLISECONDS = 1000
    }

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
