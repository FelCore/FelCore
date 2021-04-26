// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;

namespace Tools.MapExtractor
{
    public unsafe static class Utils
    {
        public static void flipcc(byte* fcc)
        {
            var tmp = fcc[0];
            fcc[0] = fcc[3];
            fcc[3] = tmp;

            tmp = fcc[1];
            fcc[1] = fcc[2];
            fcc[2] = tmp;
        }
    }
}
