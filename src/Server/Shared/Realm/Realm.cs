// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Net;
using System.Net.Sockets;
using Common;
using static Common.Errors;

namespace Server.Shared
{
    public enum RealmFlags
    {
        REALM_FLAG_NONE             = 0x00,
        REALM_FLAG_VERSION_MISMATCH = 0x01,
        REALM_FLAG_OFFLINE          = 0x02,
        REALM_FLAG_SPECIFYBUILD     = 0x04,
        REALM_FLAG_UNK1             = 0x08,
        REALM_FLAG_UNK2             = 0x10,
        REALM_FLAG_RECOMMENDED      = 0x20,
        REALM_FLAG_NEW              = 0x40,
        REALM_FLAG_FULL             = 0x80
    }

    public struct RealmHandle : IComparable<RealmHandle>
    {
        public readonly int Realm; // primary key in `realmlist` table

        public RealmHandle(int realm)
        {
            Realm = realm;
        }

        public int CompareTo(RealmHandle other)
        {
            return Realm.CompareTo(other.Realm);
        }
    }

    /// Type of server, this is values from second column of Cfg_Configs.dbc
    public enum RealmType
    {
        REALM_TYPE_NORMAL = 0,
        REALM_TYPE_PVP = 1,
        REALM_TYPE_NORMAL2 = 4,
        REALM_TYPE_RP = 6,
        REALM_TYPE_RPPVP = 8,

        MAX_CLIENT_REALM_TYPE = 14,

        REALM_TYPE_FFA_PVP = 16                     // custom, free for all pvp mode like arena PvP in all zones except rest activated places and sanctuaries
                                                    // replaced by REALM_PVP in realm list
    }

    // Storage object for a realm
    public struct Realm
    {
        public RealmHandle Id;
        public uint Build;
        public IPAddress? ExternalAddress;
        public IPAddress? LocalAddress;
        public IPAddress? LocalSubnetMask;
        public ushort Port;
        public string? Name;
        public byte Type; // icon
        public RealmFlags Flags;
        public byte Timezone;
        public AccountTypes AllowedSecurityLevel;
        public float PopulationLevel;

        public IPEndPoint GetAddressForClient(IPAddress clientAddr)
        {
            Assert(ExternalAddress != null);
            Assert(LocalAddress != null);
            Assert(LocalSubnetMask != null);
            IPAddress realmIp;

            // Attempt to send best address for client
            if (IPAddress.IsLoopback(clientAddr))
            {
                // Try guessing if realm is also connected locally
                if (IPAddress.IsLoopback(LocalAddress!) || IPAddress.IsLoopback(ExternalAddress!))
                    realmIp = clientAddr;
                else
                {
                    // Assume that user connecting from the machine that bnetserver is located on
                    // has all realms available in his local network
                    realmIp = LocalAddress!;
                }
            }
            else
            {
                if (clientAddr.AddressFamily == AddressFamily.InterNetwork && Util.IPv4InNetwork(clientAddr, LocalSubnetMask!, LocalAddress!))
                    realmIp = LocalAddress!;
                else
                    realmIp = ExternalAddress!;
            }

            // Return external IP
            return new IPEndPoint(realmIp, Port);
        }
    }
}
