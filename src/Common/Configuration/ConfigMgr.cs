// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Collections.Generic;
using SharpConfig;
using static Common.Log;

namespace Common
{
    public class ConfigMgr
    {
        static ConfigMgr? _instance;

        public static ConfigMgr sConfigMgr
        {
            get
            {
                if (_instance == null)
                    _instance = new ConfigMgr();

                return _instance;
            }
        }

        string? _filename;
        public string? Filename
        {
            get
            {
                lock (_configLock)
                {
                    return _filename;
                }
            }
        }

        List<string> _additonalFiles = new List<string>();

        List<string> _args = new List<string>();
        public List<string> Arguments => _args;

        Section? _config;
        object _configLock = new object();

        private ConfigMgr() { }

        private static bool LoadFile(string file, out Configuration? config, ref string error)
        {
            try
            {
                var ret = Configuration.LoadFromFile(file);

                if (ret.SectionCount < 2)
                {
                    error = "empty file (" + file + ")";

                    config = null;
                    return false;
                }

                config = ret;
            }
            catch (System.IO.FileNotFoundException)
            {
                error = $"Configuration file {file} not found!";
                config = null;

                return false;
            }
            catch (ParserException e)
            {
                if (e.Line == 0)
                    error = e.Message + " (" + file + ")";
                else
                    error = e.Message + " (" + file + ":" + e.Line.ToString() + ")";

                config = null;
                return false;
            }

            return true;
        }

        /// Method used only for loading main configuration files (authserver.conf and worldserver.conf)
        public bool LoadInitial(string file, List<string> args, ref string error)
        {
            lock (_configLock)
            {
                _filename = file;
                _args = args;

                Configuration? config;
                if (!LoadFile(_filename, out config, ref error) || config == null)
                    return false;

                // Since we're using only one section per config file, we skip the section and have direct property access
                _config = config[1];

                return true;
            }
        }
        public bool LoadAdditionalFile(string file, bool keepOnReload, ref string error)
        {
            if (_config == null) return false;

            Configuration? config;
            if (!LoadFile(file, out config, ref error) || config == null)
                return false;

            foreach (var setting in config[1])
                _config.Add(setting);

            if (keepOnReload)
                _additonalFiles.Add(file);

            return true;
        }

        public bool Reload(List<string> errors)
        {
            if (_filename == null) return false;

            string error = string.Empty;
            if (!LoadInitial(_filename, _args, ref error))
                errors.Add(new string(error));

            foreach (var additionalFile in _additonalFiles)
                if (!LoadAdditionalFile(additionalFile, false, ref error))
                    errors.Add(new string(error));

            return errors.Count == 0;
        }

        public string GetStringDefault(string name, string def, bool quiet = false)
        {
            var val = GetValueDefault(name, def, quiet);
            return val.Replace("\"", string.Empty);
        }

        public bool GetBoolDefault(string name, bool def, bool quiet = false)
        {
            string val = GetValueDefault(name, def ? "true" : "false", quiet);
            val = val.Replace("\"", string.Empty);

            if (val == "1")
                val = "true";
            else if (val == "0")
                val = "false";

            if (bool.TryParse(val, out var result))
                return result;
            else
            {
                if (val == "true")
                    return true;
                else if (val == "false")
                    return false;

                FEL_LOG_ERROR("server.loading", "Bad value defined for name {0} in config file {1}, going to use '{2}' instead",
                    name, _filename, def ? "true" : "false");
                return def;
            }
        }

        public int GetIntDefault(string name, int def, bool quiet = false)
        {
            return GetValueDefault(name, def, quiet);
        }

        public float GetFloatDefault(string name, float def, bool quiet = false)
        {
            return GetValueDefault(name, def, quiet);
        }

        public List<string> GetKeysByString(string name)
        {
            List<string> keys = new List<string>();
            if (_config == null) return keys;

            lock (_configLock)
            {
                foreach (var child in _config)
                    if (child.Name.StartsWith(name))
                        keys.Add(child.Name);

                return keys;
            }
        }

        T GetValueDefault<T>(string name, T def, bool quiet)
        {
            if (_config == null)
                throw new ApplicationException("ConfigMgr._config is null!");

            try
            {
                return _config[name].GetValueOrDefault<T>(def, true);
            }
            catch (SettingValueCastException)
            {
                if (!quiet)
                {
                    FEL_LOG_WARN("server.loading", "Missing name %s in config file %s, add \"%s = %s\" to this file",
                        name, _filename, name, def == null ? "" : def.ToString());
                }
            }

            return def;
        }
    }
}
