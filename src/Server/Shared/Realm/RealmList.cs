// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using Server.Database;
using Common;
using static Common.Util;
using static Common.Log;
using static Common.Errors;
using static Common.AccountTypes;
using static Server.Database.LoginStatements;
using static Server.Shared.RealmType;

namespace Server.Shared
{
    public class RealmBuildInfo
    {
        public const int HashSize = 20;

        public uint Build;
        public uint MajorVersion;
        public uint MinorVersion;
        public uint BugfixVersion;
        public string HotfixVersion = string.Empty;
        public byte[]? WindowsHash; // Len 20
        public byte[]? MacHash; // Len 20
    }

    public class RealmList
    {
        static RealmList? _instance;

        public static RealmList sRealmList
        {
            get
            {
                if (_instance == null)
                    _instance = new();

                return _instance;
            }
        }

        private RealmList() {}

        List<RealmBuildInfo> _builds = new List<RealmBuildInfo>();

        SortedDictionary<RealmHandle, Realm> _realms = new();
        object _realmsLock = new object();

        int _updateInterval;
        Timer? _updateTimer;

        private void LoadBuildInfo()
        {
            var result = DB.LoginDatabase.Query("SELECT majorVersion, minorVersion, bugfixVersion, hotfixVersion, build, winChecksumSeed, macChecksumSeed FROM build_info ORDER BY build ASC");
            if (result != null)
            {
                do
                {
                    var fields = result.Fetch();
                    var build = new RealmBuildInfo();

                    build.MajorVersion = fields.GetUInt32(0);
                    build.MinorVersion = fields.GetUInt32(1);
                    build.BugfixVersion = fields.GetUInt32(2);
                    build.HotfixVersion = fields.GetString(3);

                    build.Build = fields.GetUInt32(4);
                    var windowsHash = fields.GetString(5);
                    if (windowsHash.Length == RealmBuildInfo.HashSize * 2)
                        build.WindowsHash = HexStrToByteArray(windowsHash);

                    var macHash = fields.GetString(6);
                    if (macHash.Length == RealmBuildInfo.HashSize * 2)
                        build.MacHash = HexStrToByteArray(macHash);

                    _builds.Add(build);

                } while (result.NextRow());
            }
        }
        public void Initialize(int updateInterval)
        {
            Assert(updateInterval > 0);

            _updateInterval = updateInterval * 1000;

            LoadBuildInfo();
            UpdateRealms();

            _updateTimer = new Timer((s) =>
            {
                lock (_realmsLock)
                    UpdateRealms();

                if (_updateTimer != null)
                    _updateTimer.Change(_updateInterval, Timeout.Infinite);
            }, null, _updateInterval, Timeout.Infinite);
        }

        public void Close()
        {
            if (_updateTimer != null)
            {
                _updateTimer.Dispose();
                _updateTimer = null;
            }
        }

        public SortedDictionary<RealmHandle, Realm> GetRealms() { return _realms; }

        public void LockRealms() { Monitor.Enter(_realmsLock); }
        public void UnlockRealms() { Monitor.Exit(_realmsLock); }

        public Realm? GetRealm(RealmHandle id)
        {
            lock (_realmsLock)
            {
                if (_realms.TryGetValue(id, out var ret))
                    return ret;

                return null;
            }
        }

        public RealmBuildInfo? GetBuildInfo(uint build)
        {
            foreach (var clientBuild in _builds)
                if (clientBuild.Build == build)
                    return clientBuild;

            return null;
        }

        void UpdateRealms()
        {
            FEL_LOG_DEBUG("server.authserver", "Updating Realm List...");

            var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_SEL_REALMLIST);
            var result = DB.LoginDatabase.Query(stmt);

            Dictionary<RealmHandle, string> existingRealms = new();
            foreach (var p in _realms)
                existingRealms[p.Key] = p.Value.Name!;

            _realms.Clear();

            // Circle through results and add them to the realm map
            if (result != null)
            {
                do
                {
                    try
                    {
                        var fields = result.Fetch();
                        int realmId = fields.GetInt32(0);
                        string name = fields.GetString(1);
                        string externalAddressString = fields.GetString(2);
                        string localAddressString = fields.GetString(3);
                        string localSubmaskString = fields.GetString(4);

                        var externalAddress = Util.ResolveIPAddress(externalAddressString);
                        if (externalAddress == null)
                        {
                            FEL_LOG_ERROR("server.authserver", "Could not resolve address {0} for realm \"{1}\" id {2}", externalAddressString, name, realmId);
                            continue;
                        }

                        var localAddress = Util.ResolveIPAddress(localAddressString);
                        if (localAddress == null)
                        {
                            FEL_LOG_ERROR("server.authserver", "Could not resolve localAddress {0} for realm \"{1}\" id %u", localAddressString, name, realmId);
                            continue;
                        }

                        IPAddress? localSubmask = null;
                        IPAddress.TryParse(localSubmaskString, out localSubmask);
                        if (localSubmask == null)
                        {
                            FEL_LOG_ERROR("server.authserver", "Could not resolve localSubnetMask {0} for realm \"{1}\" id {2}", localSubmaskString, name, realmId);
                            continue;
                        }

                        ushort port = fields.GetUInt16(5);
                        byte icon = fields.GetByte(6);
                        if (icon == (byte)REALM_TYPE_FFA_PVP)
                            icon = (byte)REALM_TYPE_PVP;
                        if (icon >= (byte)MAX_CLIENT_REALM_TYPE)
                            icon = (byte)REALM_TYPE_NORMAL;
                        RealmFlags flag = (RealmFlags)(fields.GetByte(7));
                        byte timezone = fields.GetByte(8);
                        byte allowedSecurityLevel = fields.GetByte(9);
                        float pop = fields.GetFloat(10);
                        uint build = fields.GetUInt32(11);

                        RealmHandle id = new RealmHandle(realmId);

                        UpdateRealm(id, build, name, externalAddress, localAddress, localSubmask, port, icon, flag,
                            timezone, (allowedSecurityLevel <= (byte)SEC_ADMINISTRATOR ? (AccountTypes)allowedSecurityLevel : SEC_ADMINISTRATOR), pop);

                        if (!existingRealms.TryGetValue(id, out _))
                            FEL_LOG_INFO("server.authserver", "Added realm \"{0}\" at {1}:{2}.", name, externalAddressString, port);
                        else
                            FEL_LOG_DEBUG("server.authserver", "Updating realm \"{0}\" at {1}:{2}.", name, externalAddressString, port);

                        existingRealms.Remove(id);
                    }
                    catch (Exception ex)
                    {
                        FEL_LOG_ERROR("server.authserver", "Realmlist::UpdateRealms has thrown an exception: {0}", ex);
                        Environment.FailFast(null);
                    }
                }
                while (result.NextRow());
            }

            foreach (var item in existingRealms)
                FEL_LOG_INFO("server.authserver", "Removed realm \"{0}\".", item.Value);
        }

        void UpdateRealm(RealmHandle id, uint build, string name, IPAddress address, IPAddress localAddr, IPAddress localSubmask,
            ushort port, byte icon, RealmFlags flag, byte timezone, AccountTypes allowedSecurityLevel, float population)
        {
            // Create new if not exist or update existed
            Realm realm;
            if (!_realms.TryGetValue(id, out realm))
                realm = new Realm();


            realm.Id = id;
            realm.Build = build;
            realm.Name = name;
            realm.Type = icon;
            realm.Flags = flag;
            realm.Timezone = timezone;
            realm.AllowedSecurityLevel = allowedSecurityLevel;
            realm.PopulationLevel = population;
            if (realm.ExternalAddress == null || realm.ExternalAddress != address)
                realm.ExternalAddress = address;
            if (realm.LocalAddress == null || realm.LocalAddress != localAddr)
                realm.LocalAddress = localAddr;
            if (realm.LocalSubnetMask == null || realm.LocalSubnetMask != localSubmask)
                realm.LocalSubnetMask = localSubmask;
            realm.Port = port;

            // Set new value back
            _realms[id] = realm;
        }
    }
}
