// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

namespace Common
{
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
}
