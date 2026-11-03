using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenSol.Core
{
    public class ServerConfig
    {
        public int Port { get; set; } = 9999;
        public bool UseHttps { get; set; } = false; // Default false
        public bool LogsEnabled { get; set; } = false;
        public int LogsRetentionDays { get; set; } = 30;
        public bool EnableGeolock { get; set; } = false;
        public double GeoLatitude { get; set; } = 0;
        public double GeoLongitude { get; set; } = 0;
        public int MaxDistance { get; set; } = 300; // meters
    }

    public static class ConfigManager
    {
        private const string ConfigFileName = "server.config";

        // Keys of server.config. Older versions also kept users in this file (Name=EncryptedCode),
        // so these names must never be read as users.
        private static readonly string[] SettingKeys =
        {
            "Port", "UseHttps", "logs", "logsretention", "EnableGeolock", "GeoLatitude", "GeoLongitude", "MaxDistance", "Language"
        };

        public static bool IsSettingKey(string key)
        {
            return SettingKeys.Any(k => k.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static ServerConfig LoadConfig()
        {
            var config = new ServerConfig();

            if (!File.Exists(ConfigFileName)) return config;

            try
            {
                var lines = File.ReadAllLines(ConfigFileName);
                foreach (var line in lines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length != 2) continue;

                    string key = parts[0].Trim();
                    string val = parts[1].Trim();

                    if (key.Equals("Port", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int port)) config.Port = port;
                    }
                    else if (key.Equals("UseHttps", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.TryParse(val, out bool useHttps)) config.UseHttps = useHttps;
                    }
                    else if (key.Equals("logs", StringComparison.OrdinalIgnoreCase))
                    {
                         // Supporta yes/no come richiesto
                         config.LogsEnabled = val.Trim().ToLower() == "yes";
                    }
                    else if (key.Equals("logsretention", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int days)) config.LogsRetentionDays = days;
                    }
                    else if (key.Equals("EnableGeolock", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.TryParse(val, out bool geo)) config.EnableGeolock = geo;
                    }
                    else if (key.Equals("GeoLatitude", StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) config.GeoLatitude = lat;
                    }
                    else if (key.Equals("GeoLongitude", StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) config.GeoLongitude = lon;
                    }
                    else if (key.Equals("MaxDistance", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(val, out int dist)) config.MaxDistance = dist;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
            }

            return config;
        }

        public static void SaveConfig(ServerConfig config)
        {
            try
            {
                var lines = new List<string>();
                
                lines.Add($"Port={config.Port}");
                lines.Add($"UseHttps={config.UseHttps}");
                lines.Add($"logs={(config.LogsEnabled ? "yes" : "no")}");
                lines.Add($"logsretention={config.LogsRetentionDays}");
                lines.Add($"EnableGeolock={config.EnableGeolock}");
                lines.Add($"GeoLatitude={config.GeoLatitude.ToString(CultureInfo.InvariantCulture)}");
                lines.Add($"GeoLongitude={config.GeoLongitude.ToString(CultureInfo.InvariantCulture)}");
                lines.Add($"MaxDistance={config.MaxDistance}");

                SecureFile.WriteAllText(ConfigFileName, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }
    }
}
