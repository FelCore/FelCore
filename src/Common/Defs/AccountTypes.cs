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
}
