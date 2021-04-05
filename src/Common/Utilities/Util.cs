// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Net;
using static Common.Log;
using static Common.Errors;

namespace Common
{
    public static class Util
    {
        public const int MaxStackLimit = 1024;

        public static string ByteArrayToHexStr(byte[] bytes, bool reverse = false)
        {
            int arrayLen = bytes.Length;
            int init = 0;
            int end = arrayLen;
            sbyte op = 1;

            if (reverse)
            {
                init = arrayLen - 1;
                end = -1;
                op = -1;
            }

            var sb = new StringBuilder(arrayLen * 2);

            for (int i = init; i != end; i += op)
                sb.Append(bytes[i].ToString("X2"));

            return sb.ToString();
        }

        public static byte[] HexStrToByteArray(string str, bool reverse = false)
        {
            Assert(str.Length % 2 == 0);

            var ret = new byte[str.Length / 2];

            int init = 0;
            int end = str.Length;
            sbyte op = 1;

            if (reverse)
            {
                init = str.Length - 2;
                end = -2;
                op = -1;
            }

            int j = 0;
            for (int i = init; i != end; i += 2 * op)
            {
                var buffer = str.Substring(i, 2);
                ret[j++] = Convert.ToByte(buffer, 16);
            }

            return ret;
        }

        public static byte[] DigestSHA1(string str)
        {
            return DigestSHA1(Encoding.UTF8.GetBytes(str));
        }

        public static byte[] DigestSHA1(byte[] bytes)
        {
            using (var sha1 = new SHA1Hash())
            {
                sha1.UpdateData(bytes, bytes.Length);
                sha1.Finish();
                return sha1.Digest!;
            }
        }

        public static byte[] DigestSHA1(params byte[][] pack)
        {
            using (var sha1 = new SHA1Hash())
            {
                foreach(var data in pack)
                    sha1.UpdateData(data);

                sha1.Finish();
                return sha1.Digest!;
            }
        }

        public static int StartProcess(string executable, string args, string logger, string inputFile, bool secure)
        {
            if (!secure)
                FEL_LOG_TRACE(logger, "Starting process \"{0}\" with arguments: \"{1}\".", executable, args);

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardInputEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            var LogResult = new Action(() =>
            {
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderror = proc.StandardError.ReadToEnd();

                FEL_LOG_INFO(logger, "{0}", stdout);
                FEL_LOG_ERROR(logger, "{0}", stderror);

                if (!secure)
                {
                    FEL_LOG_TRACE(logger, ">> Process \"{0}\" finished with return value {1}.", executable, proc.ExitCode);
                }
            });


            try
            {
                proc.Start();

                if (!string.IsNullOrEmpty(inputFile))
                {
                    var stdin = proc.StandardInput;
                    stdin.Write(File.ReadAllText(inputFile));

                    stdin.Close();
                }

                proc.WaitForExit();

                LogResult();
            }
            catch (IOException)
            {
                LogResult();
            }
            catch
            {
                return 1;
            }

            return proc.ExitCode;
        }

        public static int CreatePIDFile(string filename)
        {
            var pid = GetPID();

            try
            {
                File.WriteAllText(filename, pid.ToString());
            }
            catch
            {
                return 0;
            }

            return pid;
        }

        public static int GetPID()
        {
            return Process.GetCurrentProcess().Id;
        }

        public static bool IPv4InNetwork(IPAddress address, IPAddress subnetMask, IPAddress network)
        {
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // IPv4
            {
                Span<byte> addressOctets = stackalloc byte[4];
                address.TryWriteBytes(addressOctets, out var bytesCount);

                Span<byte> subnetOctets = stackalloc byte[4];
                subnetMask.TryWriteBytes(subnetOctets, out bytesCount);

                Span<byte> networkOctets = stackalloc byte[4];
                network.TryWriteBytes(networkOctets, out bytesCount);
                networkOctets[3] = 0; // Force to a sub network address

                return
                    (networkOctets[0] == (addressOctets[0] & subnetOctets[0])) &&
                    (networkOctets[1] == (addressOctets[1] & subnetOctets[1])) &&
                    (networkOctets[2] == (addressOctets[2] & subnetOctets[2])) &&
                    (networkOctets[3] == (addressOctets[3] & subnetOctets[3]));
            }
            else // IPv6
            {
                return false;
            }
        }

        public static IPAddress? ResolveIPAddress(string hostnameOrIp)
        {
            try
            {
                IPHostEntry host = Dns.GetHostEntry(hostnameOrIp);
                if (host.AddressList.Length == 0)
                    return null;

                foreach(var addr in host.AddressList)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return addr;
                }
            }
            catch
            {
                if (IPAddress.TryParse(hostnameOrIp, out var address))
                    return address;
            }

            return null;
        }
    }
}
