// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.InteropServices;

namespace Tools.MapExtractor
{
    using static ADT_File;

    public enum AdtLiquidType
    {
        ADT_LIQUID_TYPE_WATER = 0,
        ADT_LIQUID_TYPE_OCEAN = 1,
        ADT_LIQUID_TYPE_MAGMA = 2,
        ADT_LIQUID_TYPE_SLIME = 3,
        //6 = slime from ?
        ADT_LIQUID_TYPE_WATER_SUNWELL = 7,
    }

    //
    // Adt file height map chunk
    //
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct adt_MCVT
    {
        public u_map_fcc_rev map;
        public uint size;
        public fixed float height_map[(ADT_CELL_SIZE+1)*(ADT_CELL_SIZE+1)+ADT_CELL_SIZE*ADT_CELL_SIZE];

        public bool PrepareLoadedData()
        {
            if (map.fcc != MCVTMagic.fcc)
                return false;

            if (size != sizeof(adt_MCVT) - 8)
                return false;

            fixed (adt_MCVT* ptr = &this)
            {
                var ptr2 = ((byte*)ptr) + 10;
            }

            return true;
        }
    }

    //
    // Adt file liquid map chunk (old)
    //
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct adt_MCLQ
    {
        public u_map_fcc_rev map;
        public uint size;
        public float height1;
        public float height2;

        public struct liquid_data{
            public uint light;
            public float height;
        }

        public fixed byte liquid[ADT_CELL_SIZE+1 * ADT_CELL_SIZE+1];

        // 1<<0 - ochen
        // 1<<1 - lava/slime
        // 1<<2 - water
        // 1<<6 - all water
        // 1<<7 - dark water
        // == 0x0F - not show liquid
        public fixed byte flags[ADT_CELL_SIZE * ADT_CELL_SIZE];
        public fixed byte data[84];
        public bool PrepareLoadedData()
        {
            if (map.fcc != MCLQMagic.fcc)
                return false;

            return true;
        }
    }

    public unsafe class ADT_File : FileLoader
    {
        public const float TILESIZE = 533.33333f;
        public const float CHUNKSIZE = TILESIZE / 16.0f;
        public const float UNITSIZE = CHUNKSIZE / 8.0f;

        public const int ADT_CELLS_PER_GRID = 16;
        public const int ADT_CELL_SIZE = 8;
        public const int ADT_GRID_SIZE = ADT_CELLS_PER_GRID * ADT_CELL_SIZE;

        public static readonly u_map_fcc MHDRMagic;
        public static readonly u_map_fcc MCINMagic;
        public static readonly u_map_fcc MH2OMagic;
        public static readonly u_map_fcc MCNKMagic;
        public static readonly u_map_fcc MCVTMagic;
        public static readonly u_map_fcc MCLQMagic;
        public static readonly u_map_fcc MFBOMagic;

        static ADT_File()
        {
            MHDRMagic.fcc_txt[0] = (byte)'R';
            MHDRMagic.fcc_txt[1] = (byte)'D';
            MHDRMagic.fcc_txt[2] = (byte)'H';
            MHDRMagic.fcc_txt[3] = (byte)'M';

            MCINMagic.fcc_txt[0] = (byte)'N';
            MCINMagic.fcc_txt[1] = (byte)'I';
            MCINMagic.fcc_txt[2] = (byte)'C';
            MCINMagic.fcc_txt[3] = (byte)'M';

            MH2OMagic.fcc_txt[0] = (byte)'O';
            MH2OMagic.fcc_txt[1] = (byte)'2';
            MH2OMagic.fcc_txt[2] = (byte)'H';
            MH2OMagic.fcc_txt[3] = (byte)'M';

            MCNKMagic.fcc_txt[0] = (byte)'K';
            MCNKMagic.fcc_txt[1] = (byte)'N';
            MCNKMagic.fcc_txt[2] = (byte)'C';
            MCNKMagic.fcc_txt[3] = (byte)'M';

            MCVTMagic.fcc_txt[0] = (byte)'T';
            MCVTMagic.fcc_txt[1] = (byte)'V';
            MCVTMagic.fcc_txt[2] = (byte)'C';
            MCVTMagic.fcc_txt[3] = (byte)'M';

            MCLQMagic.fcc_txt[0] = (byte)'Q';
            MCLQMagic.fcc_txt[1] = (byte)'L';
            MCLQMagic.fcc_txt[2] = (byte)'C';
            MCLQMagic.fcc_txt[3] = (byte)'M';

            MFBOMagic.fcc_txt[0] = (byte)'O';
            MFBOMagic.fcc_txt[1] = (byte)'B';
            MFBOMagic.fcc_txt[2] = (byte)'F';
            MFBOMagic.fcc_txt[3] = (byte)'M';
        }
    }
}
