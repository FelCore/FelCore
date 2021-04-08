// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Numerics;
using System.Text;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Common;
using Common.Extensions;
using static Common.Log;
using static Common.ConfigMgr;
using static Common.Errors;
using static Common.CommonDefs;
using Server.Database;
using static Server.Database.LoginStatements;
using Server.Shared;
using static Server.Shared.RealmList;
using static Common.RandomEngine;

namespace Server.AuthServer
{
    using static eAuthCmd;
    using static AuthStatus;
    using static AuthResult;
    using static ExpansionFlags;
    using static RealmFlags;

    public enum eAuthCmd : byte
    {
        AUTH_LOGON_CHALLENGE = 0x00,
        AUTH_LOGON_PROOF = 0x01,
        AUTH_RECONNECT_CHALLENGE = 0x02,
        AUTH_RECONNECT_PROOF = 0x03,
        REALM_LIST = 0x10,
        XFER_INITIATE = 0x30,
        XFER_DATA = 0x31,
        XFER_ACCEPT = 0x32,
        XFER_RESUME = 0x33,
        XFER_CANCEL = 0x34
    }

    public enum AuthStatus
    {
        STATUS_CHALLENGE = 0,
        STATUS_LOGON_PROOF,
        STATUS_RECONNECT_PROOF,
        STATUS_AUTHED,
        STATUS_WAITING_FOR_REALM_LIST,
        STATUS_CLOSED
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct AuthLogonChallenge_C
    {
        public byte cmd;
        public byte error;
        public ushort size;
        public fixed byte gamename[4];
        public byte version1;
        public byte version2;
        public byte version3;
        public ushort build;
        public fixed byte platform[4];
        public fixed byte os[4];
        public fixed byte country[4];
        public uint timezone_bias;
        public uint ip;
        public byte I_len;
        public fixed byte I[1];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct AuthLogonProof_C
    {
        public byte cmd;
        public fixed byte A[32];
        public fixed byte clientM[20];
        public fixed byte crc_hash[20];
        public byte number_of_keys;
        public byte securityFlags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct AuthLogonProof_S
    {
        public byte cmd;
        public byte error;
        public fixed byte M2[20];
        public uint AccountFlags;
        public uint SurveyId;
        public ushort LoginFlags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct AuthLogonProof_S_Old
    {
        public byte cmd;
        public byte error;
        public fixed byte M2[20];
        public uint unk2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct AuthReconnectProof_C
    {
        public byte cmd;
        public fixed byte R1[16];
        public fixed byte R2[20];
        public fixed byte R3[20];
        public byte number_of_keys;
    }

    public unsafe struct AuthHandler
    {
        public AuthStatus Status;
        public int PacketSize;
        public Func<AuthSession, bool> Handler;
    }

    public struct AccountInfo
    {
        public void LoadResult(ReadOnlySpan<Field> fields)
        {
            //          0           1         2               3          4                5                                                             6
            //SELECT a.id, a.username, a.locked, a.lock_country, a.last_ip, a.failed_logins, ab.unbandate > UNIX_TIMESTAMP() OR ab.unbandate = ab.bandate,
            //                               7                 8
            //       ab.unbandate = ab.bandate, aa.SecurityLevel (, more query-specific fields)
            //FROM account a LEFT JOIN account_access aa ON a.id = aa.AccountID LEFT JOIN account_banned ab ON ab.id = a.id AND ab.active = 1 WHERE a.username = ?

            Id = fields[0].GetUInt32();
            Login = fields[1].GetString().ToUpperInvariant();
            IsLockedToIP = fields[2].GetBool();
            LockCountry = fields[3].GetString();
            LastIP = fields[4].GetString();
            FailedLogins = fields[5].GetUInt32();
            IsBanned = fields[6].GetUInt64() != 0;
            IsPermanenetlyBanned = fields[7].GetUInt64() != 0;
            SecurityLevel = (AccountTypes)(fields[8].GetUInt8());
        }

        public uint Id;
        public string Login;
        public bool IsLockedToIP;
        public string LockCountry;
        public string LastIP;
        public uint FailedLogins;
        public bool IsBanned;
        public bool IsPermanenetlyBanned;
        public AccountTypes SecurityLevel;
        public string TokenKey;
    }

    public unsafe class AuthSession : SocketBase
    {
        public static byte[] VersionChallenge = new byte[16] { 0xBA, 0xA3, 0x1E, 0x99, 0xA0, 0x0B, 0x21, 0x57, 0xFC, 0x37, 0x3F, 0xB3, 0x69, 0xCD, 0xD2, 0xF1 };

        public static readonly int MAX_ACCEPTED_CHALLENGE_SIZE = sizeof(AuthLogonChallenge_C) + 16;

        public const int AUTH_LOGON_CHALLENGE_INITIAL_SIZE = 4;
        public const int REALM_LIST_PACKET_SIZE = 5;

        public static readonly Dictionary<eAuthCmd, AuthHandler> Handlers = new();

        AuthStatus _status;
        AccountInfo _accountInfo = default;
        string? _totpSecret;
        string? _localizationName;
        string? _os;
        string? _ipCountry = null;
        ushort _build;
        ExpansionFlags _expversion;

        SRP6? _srp6;
        byte[]? _sessionKey; // Len 40
        byte[]? _reconnectProof; // Len 16

        SHA1Hash _sha1;

        static AuthSession()
        {
            Handlers[AUTH_LOGON_CHALLENGE] = new AuthHandler { Status = STATUS_CHALLENGE, PacketSize = AUTH_LOGON_CHALLENGE_INITIAL_SIZE, Handler = (s) => s.HandleLogonChallenge() };
            Handlers[AUTH_LOGON_PROOF] = new AuthHandler { Status = STATUS_LOGON_PROOF, PacketSize = sizeof(AuthLogonProof_C), Handler = (s) => s.HandleLogonProof() };
            Handlers[AUTH_RECONNECT_CHALLENGE] = new AuthHandler { Status = STATUS_CHALLENGE, PacketSize = AUTH_LOGON_CHALLENGE_INITIAL_SIZE, Handler = (s) => s.HandleReconnectChallenge() };
            Handlers[AUTH_RECONNECT_PROOF] = new AuthHandler { Status = STATUS_RECONNECT_PROOF, PacketSize = sizeof(AuthReconnectProof_C), Handler = (s) => s.HandleReconnectProof() };
            Handlers[REALM_LIST] = new AuthHandler { Status = STATUS_AUTHED, PacketSize = REALM_LIST_PACKET_SIZE, Handler = (s) => s.HandleRealmList() };
        }

        AsyncCallbackProcessor<QueryCallback> _queryProcessor;

        public AuthSession(Socket socket) : base(socket)
        {
            _queryProcessor = new AsyncCallbackProcessor<QueryCallback>();
            _status = AuthStatus.STATUS_CHALLENGE;
            _sha1 = new SHA1Hash();
        }

        ~AuthSession()
        {
            Dispose(false);
        }

        public void SendPacket(ByteBuffer packet)
        {
            if (!IsOpen())
                return;

            if (packet.Wpos() > 0)
            {
                MessageBuffer buffer = new(packet.Wpos());
                buffer.Write(packet.WriteSpan);
                QueuePacket(buffer);
            }
        }

        public override void Start()
        {
            var ipAddress = RemoteAddress.ToString();
            FEL_LOG_TRACE("session", "Accepted connection from {0}", ipAddress);

            var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_SEL_IP_INFO);
            stmt.Parameters[0] = ipAddress;

            _queryProcessor.AddCallback(DB.LoginDatabase.AsyncQuery(stmt).WithPreparedCallback(CheckIpCallback));
        }

        void CheckIpCallback(PreparedQueryResult? result)
        {
            if (result != null)
            {
                bool banned = false;
                do
                {
                    var fields = result.Fetch();
                    if (fields[0].GetUInt64() != 0)
                        banned = true;

                } while (result.NextRow());

                if (banned)
                {
                    ByteBuffer pkt = new ByteBuffer();
                    pkt.Append((byte)AUTH_LOGON_CHALLENGE);
                    pkt.Append((byte)0);
                    pkt.Append((byte)WOW_FAIL_BANNED);
                    SendPacket(pkt);
                    FEL_LOG_DEBUG("session", "[AuthSession::CheckIpCallback] Banned ip '{0}:{1}' tries to login!", RemoteAddress.ToString(), RemotePort);
                    return;
                }
            }

            AsyncRead();
        }

        protected override void ReadHandler()
        {
            var packet = GetReadBuffer();
            while (packet.GetActiveSize() > 0)
            {
                var cmd = (eAuthCmd)packet.ReadSpan[0];

                AuthHandler handler;

                if (!Handlers.TryGetValue(cmd, out handler))
                {
                    // well we dont handle this, lets just ignore it
                    packet.Reset();
                    break;
                }

                if (_status != handler.Status)
                {
                    CloseSocket();
                    return;
                }

                ushort size = (ushort)handler.PacketSize;
                if (packet.GetActiveSize() < size)
                    break;

                if (cmd == AUTH_LOGON_CHALLENGE || cmd == AUTH_RECONNECT_CHALLENGE)
                {
                    ref readonly var challenge = ref MemoryMarshal.AsRef<AuthLogonChallenge_C>(packet.ReadSpan);
                    size += challenge.size;

                    if (size > MAX_ACCEPTED_CHALLENGE_SIZE)
                    {
                        CloseSocket();
                        return;
                    }
                }

                if (packet.GetActiveSize() < size)
                    break;

                if (!handler.Handler(this))
                {
                    CloseSocket();
                    return;
                }

                packet.ReadCompleted(size);
            }

            AsyncRead();
        }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            _queryProcessor.ProcessReadyCallbacks();

            return true;
        }

        bool HandleLogonChallenge()
        {
            _status = STATUS_CLOSED;

            ref readonly var challenge = ref MemoryMarshal.AsRef<AuthLogonChallenge_C>(GetReadBuffer().ReadSpan);

            if (challenge.size - (sizeof(AuthLogonChallenge_C) - AUTH_LOGON_CHALLENGE_INITIAL_SIZE - 1) != challenge.I_len)
                return false;

            string login = string.Empty;
            fixed (byte* ptr = challenge.I)
            {
                login = Encoding.UTF8.GetString(ptr, challenge.I_len);
                FEL_LOG_DEBUG("server.authserver", "[AuthChallenge] '{0}'", login);
            }

            _build = challenge.build;
            _expversion = AuthHelper.IsPostBCAcceptedClientBuild(_build) ? POST_BC_EXP_FLAG : (AuthHelper.IsPreBCAcceptedClientBuild(_build) ? PRE_BC_EXP_FLAG : NO_VALID_EXP_FLAG);

            fixed (byte* ptr = challenge.os)
                _os = Encoding.UTF8.GetString(ptr, 4).TrimEnd('\0');

            // Restore string order as its byte order is reversed
            _os = _os.Reverse();

            fixed (byte* ptr = challenge.country)
                _localizationName = Encoding.UTF8.GetString(ptr, 4).TrimEnd('\0');
            _localizationName = _localizationName.Reverse();

            // Get the account details from the account table
            var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_SEL_LOGONCHALLENGE);
            stmt.Parameters[0] = login;

            _queryProcessor.AddCallback(DB.LoginDatabase.AsyncQuery(stmt).WithPreparedCallback(LogonChallengeCallback));
            return true;
        }

        void LogonChallengeCallback(PreparedQueryResult? result)
        {
            ByteBuffer pkt = new ByteBuffer();
            pkt.Append((byte)AUTH_LOGON_CHALLENGE);
            pkt.Append((byte)0);

            if (result == null)
            {
                pkt.Append((byte)WOW_FAIL_UNKNOWN_ACCOUNT);
                SendPacket(pkt);
                return;
            }

            var fields = result.Fetch();

            _accountInfo.LoadResult(fields);

            var ipAddress = RemoteAddress.ToString();
            var port = RemotePort;

            // If the IP is 'locked', check that the player comes indeed from the correct IP address
            if (_accountInfo.IsLockedToIP)
            {
                FEL_LOG_DEBUG("server.authserver", "[AuthChallenge] Account '{0}' is locked to IP - '{1}' is logging in from '{2}'", _accountInfo.Login, _accountInfo.LastIP, ipAddress);
                if (_accountInfo.LastIP != ipAddress)
                {
                    pkt.Append((byte)WOW_FAIL_LOCKED_ENFORCED);
                    SendPacket(pkt);
                    return;
                }
            }
            else
            {
                FEL_LOG_DEBUG("server.authserver", "[AuthChallenge] Account '{0}' is not locked to ip", _accountInfo.Login);
                if (string.IsNullOrEmpty(_accountInfo.LockCountry) || _accountInfo.LockCountry == "00")
                    FEL_LOG_DEBUG("server.authserver", "[AuthChallenge] Account '{0}' is not locked to country", _accountInfo.Login);
                else if (!string.IsNullOrEmpty(_accountInfo.LockCountry) && !string.IsNullOrEmpty(_ipCountry))
                {
                    FEL_LOG_DEBUG("server.authserver", "[AuthChallenge] Account '{0}' is locked to country: '{1}' Player country is '{2}'", _accountInfo.Login, _accountInfo.LockCountry, _ipCountry);
                    if (_ipCountry != _accountInfo.LockCountry)
                    {
                        pkt.Append((byte)WOW_FAIL_UNLOCKABLE_LOCK);
                        SendPacket(pkt);
                        return;
                    }
                }
            }

            // If the account is banned, reject the logon attempt
            if (_accountInfo.IsBanned)
            {
                if (_accountInfo.IsPermanenetlyBanned)
                {
                    pkt.Append((byte)WOW_FAIL_BANNED);
                    SendPacket(pkt);
                    FEL_LOG_DEBUG("server.authserver", "'{0}:{1}' [AuthChallenge] Banned account {2} tried to login!", ipAddress, port, _accountInfo.Login);
                    return;
                }
                else
                {
                    pkt.Append((byte)WOW_FAIL_SUSPENDED);
                    SendPacket(pkt);
                    FEL_LOG_DEBUG("server.authserver", "'{0}:{1}' [AuthChallenge] Temporarily banned account {2} tried to login!", ipAddress, port, _accountInfo.Login);
                    return;
                }
            }

            byte securityFlags = 0;
            // Check if a TOTP token is needed
            _totpSecret = fields[9].GetString();
            if (!string.IsNullOrEmpty(_totpSecret))
                securityFlags = 4;

            _srp6 = new SRP6(_accountInfo.Login, fields[10].GetBinary(32), fields[11].GetBinary(32), _sha1);

            // Fill the response packet with the result
            if (AuthHelper.IsAcceptedClientBuild(_build))
            {
                pkt.Append((byte)WOW_SUCCESS);

                pkt.Append(_srp6.B);
                pkt.Append((byte)1);
                pkt.Append(SRP6.g);
                pkt.Append((byte)32);
                pkt.Append(SRP6.N);
                pkt.Append(_srp6.Salt);
                pkt.Append(VersionChallenge);

                pkt.Append((byte)securityFlags);        // security flags (0x0...0x04)

                if ((securityFlags & 0x01) != 0)        // PIN input
                {
                    pkt.Append(0u);
                    pkt.Append(0ul);
                    pkt.Append(0ul);                    // 16 bytes hash?
                }

                if ((securityFlags & 0x02) != 0)        // Matrix input
                {
                    pkt.Append((byte)0);
                    pkt.Append((byte)0);
                    pkt.Append((byte)0);
                    pkt.Append((byte)0);
                    pkt.Append((ulong)0);
                }

                if ((securityFlags & 0x04) != 0)               // Security token input
                    pkt.Append((byte)1);

                FEL_LOG_DEBUG("server.authserver", "'{0}:{1}' [AuthChallenge] account {2} is using '{3}' locale ({4})",
                    ipAddress, port, _accountInfo.Login, _localizationName, GetLocaleByName(_localizationName!));

                _status = STATUS_LOGON_PROOF;
            }
            else
                pkt.Append((byte)WOW_FAIL_VERSION_INVALID);

            SendPacket(pkt);
        }

        bool HandleLogonProof()
        {
            FEL_LOG_DEBUG("server.authserver", "Entering _HandleLogonProof");
            _status = STATUS_CLOSED;

            // Read the packet
            ref readonly var logonProof = ref MemoryMarshal.AsRef<AuthLogonProof_C>(GetReadBuffer().ReadSpan);

            // If the client has no valid version
            if (_expversion == NO_VALID_EXP_FLAG)
            {
                // Check if we have the appropriate patch on the disk
                FEL_LOG_DEBUG("network", "Client with invalid version, patching is not implemented");
                return false;
            }

            // Check if SRP6 results match (password is correct), else send an error
            fixed (byte* ptrA = logonProof.A, ptrClientM = logonProof.clientM)
                _sessionKey = _srp6!.VerifyChallengeResponse(new ReadOnlySpan<byte>(ptrA, 32), new ReadOnlySpan<byte>(ptrClientM, 20));

            if (_sessionKey != null)
            {
                // Check auth token
                if ((logonProof.securityFlags & 0x04) != 0 || !string.IsNullOrEmpty(_totpSecret))
                {
                    byte size = MemoryMarshal.Read<byte>(GetReadBuffer().ReadSpan.Slice(sizeof(AuthLogonProof_C)));
                    var token = Encoding.UTF8.GetString(GetReadBuffer().ReadSpan.Slice(sizeof(AuthLogonProof_C) + 1, size));

                    GetReadBuffer().ReadCompleted(1 + size);
                    var validToken = TOTP.GenerateToken(_totpSecret!);
                    _totpSecret = null;

                    int incomingToken = 0;
                    if (!int.TryParse(token, out incomingToken) || validToken != incomingToken)
                    {
                        ByteBuffer pkt = new();
                        pkt.Append((byte)AUTH_LOGON_PROOF);
                        pkt.Append((byte)WOW_FAIL_UNKNOWN_ACCOUNT);
                        pkt.Append((ushort)0); // LoginFlags, 1 has account message
                        SendPacket(pkt);
                        return true;
                    }
                }

                fixed (byte* ptrA = logonProof.A, ptrCrcHash = logonProof.crc_hash)
                {
                    if (!VerifyVersion(new ReadOnlySpan<byte>(ptrA, 32), new ReadOnlySpan<byte>(ptrCrcHash, 20), false))
                    {
                        ByteBuffer pkt = new();
                        pkt.Append((byte)AUTH_LOGON_PROOF);
                        pkt.Append((byte)WOW_FAIL_VERSION_INVALID);
                        SendPacket(pkt);
                        return true;
                    }
                }

                FEL_LOG_DEBUG("server.authserver", "'{0}:{1}' User '{2}' successfully authenticated", RemoteAddress, RemotePort, _accountInfo.Login);

                // Update the sessionkey, last_ip, last login time and reset number of failed logins in the account table for this account
                // No SQL injection (escaped user name) and IP address as received by socket

                var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_UPD_LOGONPROOF);
                stmt.Parameters[0] = _sessionKey;
                stmt.Parameters[1] = RemoteAddress.ToString();
                stmt.Parameters[2] = (byte)GetLocaleByName(_localizationName!);
                stmt.Parameters[3] = _os;
                stmt.Parameters[4] = _accountInfo.Login;
                DB.LoginDatabase.DirectExecute(stmt);

                // Finish SRP6 and send the final result to the client
                ByteBuffer packet;
                if ((_expversion & POST_BC_EXP_FLAG) != 0)                 // 2.x and 3.x clients
                {
                    packet = new(sizeof(AuthLogonProof_S));

                    AuthLogonProof_S proof = new();
                    fixed (byte* ptrA = logonProof.A, ptrClientM = logonProof.clientM)
                        SRP6.GetSessionVerifier(new ReadOnlySpan<byte>(ptrA, 32), new ReadOnlySpan<byte>(ptrClientM, 20), _sessionKey, new Span<byte>(proof.M2, 20));
                    proof.cmd = (byte)AUTH_LOGON_PROOF;
                    proof.error = 0;
                    proof.AccountFlags = 0x00800000;    // 0x01 = GM, 0x08 = Trial, 0x00800000 = Pro pass (arena tournament)
                    proof.SurveyId = 0;
                    proof.LoginFlags = 0;               // 0x1 = has account message

                    packet.Append(proof);
                }
                else
                {
                    packet = new(sizeof(AuthLogonProof_S_Old));

                    AuthLogonProof_S_Old proof = new();
                    fixed (byte* ptrA = logonProof.A, ptrClientM = logonProof.clientM)
                        SRP6.GetSessionVerifier(new ReadOnlySpan<byte>(ptrA, 32), new ReadOnlySpan<byte>(ptrClientM, 20), _sessionKey, new Span<byte>(proof.M2, 20));
                    proof.cmd = (byte)AUTH_LOGON_PROOF;
                    proof.error = 0;
                    proof.unk2 = 0x00;

                    packet.Append(proof);
                }

                SendPacket(packet);
                _status = STATUS_AUTHED;
            }
            else // Auth failed
            {
                ByteBuffer packet = new();
                packet.Append((byte)AUTH_LOGON_PROOF);
                packet.Append((byte)WOW_FAIL_UNKNOWN_ACCOUNT);
                packet.Append((ushort)0);    // LoginFlags, 1 has account message
                SendPacket(packet);

                FEL_LOG_INFO("server.authserver.hack", "'{0}:{1}' [AuthChallenge] account {2} tried to login with invalid password!",
                    RemoteAddress, RemotePort, _accountInfo.Login);

                var MaxWrongPassCount = sConfigMgr.GetIntDefault("WrongPass.MaxCount", 0);

                // We can not include the failed account login hook. However, this is a workaround to still log this.
                if (sConfigMgr.GetBoolDefault("WrongPass.Logging", false))
                {
                    var logstmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_INS_FALP_IP_LOGGING);
                    logstmt.Parameters[0] = _accountInfo.Id;
                    logstmt.Parameters[1] = RemoteAddress.ToString();
                    logstmt.Parameters[2] = "Login to WoW Failed - Incorrect Password";

                    DB.LoginDatabase.Execute(logstmt);
                }

                if (MaxWrongPassCount > 0)
                {
                    //Increment number of failed logins by one and if it reaches the limit temporarily ban that account or IP
                    var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_UPD_FAILEDLOGINS);
                    stmt.Parameters[0] = _accountInfo.Login;
                    DB.LoginDatabase.Execute(stmt);

                    if (++_accountInfo.FailedLogins >= MaxWrongPassCount)
                    {
                        var WrongPassBanTime = sConfigMgr.GetIntDefault("WrongPass.BanTime", 600);
                        bool WrongPassBanType = sConfigMgr.GetBoolDefault("WrongPass.BanType", false);

                        if (WrongPassBanType)
                        {
                            stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_INS_ACCOUNT_AUTO_BANNED);
                            stmt.Parameters[0] = _accountInfo.Id;
                            stmt.Parameters[1] = WrongPassBanTime;
                            DB.LoginDatabase.Execute(stmt);

                            FEL_LOG_DEBUG("server.authserver", "'{0}:{1}' [AuthChallenge] account {2} got banned for '{3}' seconds because it failed to authenticate '{4}' times",
                                RemoteAddress, RemotePort, _accountInfo.Login, WrongPassBanTime, _accountInfo.FailedLogins);
                        }
                        else
                        {
                            stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_INS_IP_AUTO_BANNED);
                            stmt.Parameters[0] = RemoteAddress.ToString();
                            stmt.Parameters[1] = WrongPassBanTime;
                            DB.LoginDatabase.Execute(stmt);

                            FEL_LOG_DEBUG("server.authserver", "'{0}:{1}' [AuthChallenge] IP got banned for '{2}' seconds because account {3} failed to authenticate '{4}' times",
                                RemoteAddress.ToString(), RemotePort, WrongPassBanTime, _accountInfo.Login, _accountInfo.FailedLogins);
                        }
                    }
                }
            }

            return true;
        }

        bool HandleReconnectChallenge()
        {
            _status = STATUS_CLOSED;

            ref readonly var challenge = ref MemoryMarshal.AsRef<AuthLogonChallenge_C>(GetReadBuffer().ReadSpan);

            if (challenge.size - (sizeof(AuthLogonChallenge_C) - AUTH_LOGON_CHALLENGE_INITIAL_SIZE - 1) != challenge.I_len)
                return false;

            string login = string.Empty;
            fixed (byte* ptr = challenge.I)
            {
                login = Encoding.UTF8.GetString(ptr, challenge.I_len);
            }

            FEL_LOG_DEBUG("server.authserver", "[ReconnectChallenge] '{0}'", login);

            _build = challenge.build;
            _expversion = AuthHelper.IsPostBCAcceptedClientBuild(_build) ? POST_BC_EXP_FLAG : (AuthHelper.IsPreBCAcceptedClientBuild(_build) ? PRE_BC_EXP_FLAG : NO_VALID_EXP_FLAG);

            fixed (byte* ptr = challenge.os)
                _os = Encoding.UTF8.GetString(ptr, 4).TrimEnd('\0');

            // Restore string order as its byte order is reversed
            _os = _os.Reverse();

            fixed (byte* ptr = challenge.country)
                _localizationName = Encoding.UTF8.GetString(ptr, 4).TrimEnd('\0');
            _localizationName = _localizationName.Reverse();

            // Get the account details from the account table
            var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_SEL_RECONNECTCHALLENGE);
            stmt.Parameters[0] = login;

            _queryProcessor.AddCallback(DB.LoginDatabase.AsyncQuery(stmt).WithPreparedCallback(ReconnectChallengeCallback));
            return true;
        }

        void ReconnectChallengeCallback(PreparedQueryResult? result)
        {
            ByteBuffer pkt = new();
            pkt.Append((byte)AUTH_RECONNECT_CHALLENGE);

            if (result == null)
            {
                pkt.Append((byte)WOW_FAIL_UNKNOWN_ACCOUNT);
                SendPacket(pkt);
                return;
            }

            var fields = result.Fetch();

            _accountInfo.LoadResult(fields);
            _sessionKey = fields[9].GetBinary(SRP6.SESSION_KEY_LENGTH);

            _reconnectProof = new byte[16];

            GetRandomBytes(_reconnectProof);
            _status = STATUS_RECONNECT_PROOF;

            pkt.Append((byte)WOW_SUCCESS);
            pkt.Append(_reconnectProof);
            pkt.Append(VersionChallenge);

            SendPacket(pkt);
        }

        bool HandleReconnectProof()
        {
            FEL_LOG_DEBUG("server.authserver", "Entering _HandleReconnectProof");
            _status = STATUS_CLOSED;

            ref readonly var reconnectProof = ref MemoryMarshal.AsRef<AuthReconnectProof_C>(GetReadBuffer().ReadSpan);

            if (string.IsNullOrEmpty(_accountInfo.Login))
                return false;

            BigInteger t1;
            fixed(byte* r1Ptr = reconnectProof.R1)
                t1 = new(new ReadOnlySpan<byte>(r1Ptr, 16), true);

            _sha1.Initialize();
            _sha1.UpdateData(_accountInfo.Login);
            _sha1.UpdateData(t1);
            _sha1.UpdateData(_reconnectProof!);
            _sha1.UpdateData(_sessionKey!);
            _sha1.Finish();

            bool r2Match = false;

            fixed(byte* r2Ptr = reconnectProof.R2)
                r2Match = new ReadOnlySpan<byte>(r2Ptr, 20).SequenceEqual(_sha1.Digest);

            if (r2Match)
            {
                bool versionValid = false;
                fixed (byte* ptrR1 = reconnectProof.R1, ptrR3 = reconnectProof.R3)
                    versionValid = VerifyVersion(new ReadOnlySpan<byte>(ptrR1, 16), new ReadOnlySpan<byte>(ptrR3, 20), true);

                if (!versionValid)
                {
                    ByteBuffer packet = new();
                    packet.Append((byte)AUTH_RECONNECT_PROOF);
                    packet.Append((byte)WOW_FAIL_VERSION_INVALID);
                    SendPacket(packet);
                    return true;
                }

                // Sending response
                ByteBuffer pkt = new();
                pkt.Append((byte)AUTH_RECONNECT_PROOF);
                pkt.Append((byte)WOW_SUCCESS);
                pkt.Append((ushort)0); // LoginFlags, 1 has account message
                SendPacket(pkt);
                _status = STATUS_AUTHED;
                return true;
            }
            else
            {
                FEL_LOG_ERROR("server.authserver.hack", "'{0}:{1}' [ERROR] user {2} tried to login, but session is invalid.", RemoteAddress.ToString(),
                    RemotePort, _accountInfo.Login);
                return false;
            }
        }

        bool HandleRealmList()
        {
            FEL_LOG_DEBUG("server.authserver", "Entering _HandleRealmList");

            var stmt = DB.LoginDatabase.GetPreparedStatement(LOGIN_SEL_REALM_CHARACTER_COUNTS);
            stmt.Parameters[0] = _accountInfo.Id;

            _queryProcessor.AddCallback(DB.LoginDatabase.AsyncQuery(stmt).WithPreparedCallback(RealmListCallback));
            _status = STATUS_WAITING_FOR_REALM_LIST;
            return true;
        }

        void RealmListCallback(PreparedQueryResult? result)
        {
            Dictionary<int, byte> characterCounts = new();
            if (result != null)
            {
                do
                {
                    var fields = result.Fetch();
                    characterCounts[fields[0].GetInt32()] = fields[1].GetUInt8();
                } while (result.NextRow());
            }

            // Circle through realms in the RealmList and construct the return packet (including # of user characters in each realm)
            ByteBuffer pkt = new();

            int RealmListSize = 0;

            sRealmList.LockRealms(); // Realms would be updated in another thread.
            foreach (var item in sRealmList.GetRealms())
            {
                Realm realm = item.Value;
                // don't work with realms which not compatible with the client
                bool okBuild = ((_expversion & POST_BC_EXP_FLAG) != 0 && realm.Build == _build) || ((_expversion & PRE_BC_EXP_FLAG) != 0 && !AuthHelper.IsPreBCAcceptedClientBuild(realm.Build));

                // No SQL injection. id of realm is controlled by the database.
                var flag = realm.Flags;
                var buildInfo = sRealmList.GetBuildInfo(realm.Build);
                if (!okBuild)
                {
                    if (buildInfo == null)
                        continue;

                    flag |= REALM_FLAG_OFFLINE | REALM_FLAG_SPECIFYBUILD;   // tell the client what build the realm is for
                }

                if (buildInfo == null)
                    flag &= ~REALM_FLAG_SPECIFYBUILD;

                var name = realm.Name!;
                if ((_expversion & PRE_BC_EXP_FLAG) != 0 && (flag & REALM_FLAG_SPECIFYBUILD) != 0)
                {
                    var sb = new StringBuilder();
                    sb.Append(name).Append(" (").Append(buildInfo!.MajorVersion).Append('.')
                        .Append(buildInfo!.MinorVersion).Append('.').Append(buildInfo!.BugfixVersion).Append(')');
                    name = sb.ToString();
                }

                byte lock_ = realm.AllowedSecurityLevel > _accountInfo.SecurityLevel ? (byte)1 : (byte)0;

                pkt.Append((byte)realm.Type);                           // realm type
                if ((_expversion & POST_BC_EXP_FLAG) != 0)              // only 2.x and 3.x clients
                    pkt.Append(lock_);                                  // if 1, then realm locked
                pkt.Append((byte)flag);                                 // RealmFlags
                pkt.Append(name);
                pkt.Append(realm.GetAddressForClient(RemoteAddress).ToString());
                pkt.Append(realm.PopulationLevel);
                pkt.Append((byte)characterCounts[realm.Id.Realm]);
                pkt.Append((byte)realm.Timezone);                       // realm category
                if ((_expversion & POST_BC_EXP_FLAG) != 0)              // 2.x and 3.x clients
                    pkt.Append((byte)realm.Id.Realm);
                else
                    pkt.Append((byte)0);                                // 1.12.1 and 1.12.2 clients

                if ((_expversion & POST_BC_EXP_FLAG) != 0 && (flag & REALM_FLAG_SPECIFYBUILD) != 0)
                {
                    pkt.Append((byte)buildInfo!.MajorVersion);
                    pkt.Append((byte)buildInfo!.MinorVersion);
                    pkt.Append((byte)buildInfo!.BugfixVersion);
                    pkt.Append((ushort)buildInfo!.Build);
                }

                ++RealmListSize;
            }
            sRealmList.UnlockRealms();

            if ((_expversion & POST_BC_EXP_FLAG) != 0)                  // 2.x and 3.x clients
            {
                pkt.Append((byte)0x10);
                pkt.Append((byte)0x00);
            }
            else                                                        // 1.12.1 and 1.12.2 clients
            {
                pkt.Append((byte)0x00);
                pkt.Append((byte)0x02);
            }

            // make a ByteBuffer which stores the RealmList's size
            ByteBuffer RealmListSizeBuffer = new();
            RealmListSizeBuffer.Append((uint)0);
            if ((_expversion & POST_BC_EXP_FLAG) != 0)                  // only 2.x and 3.x clients
                RealmListSizeBuffer.Append((ushort)RealmListSize);
            else
                RealmListSizeBuffer.Append((uint)RealmListSize);

            ByteBuffer hdr = new(1 + 2 + pkt.Wpos() + RealmListSizeBuffer.Wpos());
            hdr.Append((byte)REALM_LIST);
            hdr.Append((ushort)(pkt.Wpos() + RealmListSizeBuffer.Wpos()));
            hdr.Append(RealmListSizeBuffer);                            // append RealmList's size buffer
            hdr.Append(pkt);                                            // append realms in the realmlist
            SendPacket(hdr);

            _status = STATUS_AUTHED;
        }

        bool VerifyVersion(ReadOnlySpan<byte> a, ReadOnlySpan<byte> versionProof, bool isReconnect)
        {
            if (!sConfigMgr.GetBoolDefault("StrictVersionCheck", false))
                return true;

            Span<byte> zeros = stackalloc byte[20];
            Span<byte> versionHash = stackalloc byte[20];
            if (!isReconnect)
            {
                var buildInfo = sRealmList.GetBuildInfo(_build);
                if (buildInfo == null)
                    return false;

                if (_os == "Win")
                    versionHash = buildInfo.WindowsHash;
                else if (_os == "OSX")
                    versionHash = buildInfo.MacHash;

                if (versionHash.IsEmpty)
                    return false;

                if (zeros.SequenceEqual(versionHash))
                    return true;                                                            // not filled serverside
            }

            Span<byte> data = stackalloc byte[a.Length + versionHash.Length];
            a.CopyTo(data);
            versionHash.CopyTo(data.Slice(a.Length));

            Span<byte> hash = stackalloc byte[SHA1Hash.SHA1_DIGEST_LENGTH];
            _sha1.Initialize();
            _sha1.ComputeHash(data, hash, out _);

            return versionProof.SequenceEqual(hash);
        }

        protected override void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    _sha1.Dispose();
                    _srp6?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}

