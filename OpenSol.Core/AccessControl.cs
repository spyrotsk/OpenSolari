using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenSol.Core
{
    public class UserAccessRule
    {
        public string Username { get; set; } = "";
        
        // Encrypted Token
        public string Token { get; set; } = "";

        public bool IgnoreSchedule { get; set; } = false;
        public bool IgnoreGeolock { get; set; } = false;

        // Key: DayOfWeek (English, e.g. "Monday"), Value: "HH:mm-HH:mm"
        public Dictionary<string, string> Schedule { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public class AccessControlConfig
    {
        public List<UserAccessRule> Users { get; set; } = new List<UserAccessRule>();
    }

    public static class AccessControlManager
    {
        private const string ConfigFileName = "access_control.json";
        private static AccessControlConfig _cachedConfig;
        private static DateTime _lastLoadTime = DateTime.MinValue;

        private static Dictionary<string, UserAccessRule> _decryptedTokenCache = new Dictionary<string, UserAccessRule>();

        public static AccessControlConfig LoadConfig()
        {
            if (_cachedConfig != null && (DateTime.Now - _lastLoadTime).TotalSeconds < 10)
            {
                return _cachedConfig;
            }

            if (!File.Exists(ConfigFileName))
            {
                // Try Migration
                MigrateLegacyUsers();

                if (!File.Exists(ConfigFileName))
                {
                   _cachedConfig = new AccessControlConfig();
                   _decryptedTokenCache.Clear();
                   return _cachedConfig;
                }
            }

            try
            {
                string json = File.ReadAllText(ConfigFileName);
                _cachedConfig = JsonSerializer.Deserialize(json, OpenSolContext.Default.AccessControlConfig) ?? new AccessControlConfig();
                _lastLoadTime = DateTime.Now;

                // Rebuild Cache
                _decryptedTokenCache.Clear();
                foreach (var user in _cachedConfig.Users)
                {
                    if (!string.IsNullOrEmpty(user.Token))
                    {
                        string decrypted = SecurityUtils.Decrypt(user.Token);
                        if (!string.IsNullOrEmpty(decrypted) && !_decryptedTokenCache.ContainsKey(decrypted))
                        {
                            _decryptedTokenCache[decrypted] = user;
                        }
                    }
                }
            }
            catch (Exception)
            {
                _cachedConfig = new AccessControlConfig();
                _decryptedTokenCache.Clear();
            }

            return _cachedConfig;
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = OpenSolContext.Default
        };        
        private static void MigrateLegacyUsers()
        {
            try
            {
                string legacyConfigFile = "server.config";
                if (!File.Exists(legacyConfigFile)) return;

                var config = new AccessControlConfig();
                var lines = File.ReadAllLines(legacyConfigFile);
                
                bool foundUsers = false;

                foreach (var line in lines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length != 2) continue;

                    string key = parts[0].Trim();
                    string val = parts[1].Trim();

                    // Skip system keys
                    if (key.Equals("Port", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("UseHttps", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("logs", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("logsretention", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // It's a user!
                    // Old format: Name = EncryptedToken (Token was encrypted with SecurityUtils)
                    // New format needs: Name and Token (Encrypted)
                    
                    // Val is already encrypted in file. AccessControl expects encrypted token.
                    // So we just copy it.
                    
                    config.Users.Add(new UserAccessRule 
                    { 
                        // In old config: Code=Name or Name=Code? 
                        // Key was NAME, Val was Encrypted CODE.
                                        
                        Username = key,
                        Token = val, // Already encrypted
                        IgnoreSchedule = true, // Backward compatibility
                        IgnoreGeolock = true, // Backward compatibility
                        Schedule = new Dictionary<string, string>()
                    });
                    foundUsers = true;
                }

                if (foundUsers)
                {
                    SaveConfig(config);
                    // Force cache reload handled by SaveConfig calling LoadConfig... wait, recursive?
                    // SaveConfig calls LoadConfig... 
                    // LoadConfig checks File.Exists(ConfigFileName) -> True.
                    // So it won't call MigrateLegacyUsers again. Safe.
                }
            }
            catch (Exception) { }
        }

        public static void SaveConfig(AccessControlConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, OpenSolContext.Default.AccessControlConfig);
                File.WriteAllText(ConfigFileName, json);
                
                // Force reload to update cache
                _lastLoadTime = DateTime.MinValue;
                LoadConfig();
            }
            catch (Exception) { }
        }

        public static void AddUser(string username, string encryptedToken, Dictionary<string, string> schedule, bool ignoreSchedule = false, bool ignoreGeolock = false)
        {
            var config = LoadConfig();
            var existing = config.Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            
            if (existing != null)
            {
                existing.Token = encryptedToken;
                existing.Schedule = schedule;
                existing.IgnoreSchedule = ignoreSchedule;
                existing.IgnoreGeolock = ignoreGeolock;
            }
            else
            {
                config.Users.Add(new UserAccessRule 
                { 
                    Username = username, 
                    Token = encryptedToken, 
                    Schedule = schedule,
                    IgnoreSchedule = ignoreSchedule,
                    IgnoreGeolock = ignoreGeolock
                });
            }
            SaveConfig(config);
        }

        public static void RemoveUser(string username)
        {
            var config = LoadConfig();
            var existing = config.Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                config.Users.Remove(existing);
                SaveConfig(config);
            }
        }

        public static UserAccessRule? Authenticate(string token)
        {
            LoadConfig(); // Ensure cache is populated
            if (_decryptedTokenCache.TryGetValue(token, out var user))
            {
                return user;
            }
            return null;
        }

        public static bool IsAccessAllowed(string username, DateTime verifyTime)
        {
            var config = LoadConfig();

            // Find rule for this user
            var rule = config.Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (rule == null)
            {
                // User not in access control list -> ALLOW (Backward compatibility)
                return true;
            }

            // Admin Override
            if (rule.IgnoreSchedule) return true;

            // User is in list, check strict rules
            string dayName = verifyTime.DayOfWeek.ToString(); // "Monday", "Tuesday"...
            
            if (!rule.Schedule.ContainsKey(dayName))
            {
                // No schedule for today -> DENY
                return false;
            }

            string timeRange = rule.Schedule[dayName];
            // Expected format "HH:mm-HH:mm"
            return IsTimeInRange(verifyTime.TimeOfDay, timeRange);
        }

        private static bool IsTimeInRange(TimeSpan currentTime, string range)
        {
            if (string.IsNullOrWhiteSpace(range)) return false;

            var parts = range.Split('-');
            if (parts.Length != 2) return false; // Invalid format -> Deny

            if (!TimeSpan.TryParse(parts[0], out TimeSpan start)) return false;
            if (!TimeSpan.TryParse(parts[1], out TimeSpan end)) return false;

            // Handle normal range (10:00-22:00)
            if (start <= end)
            {
                return currentTime >= start && currentTime <= end;
            }
            else
            {
                // Handle crossing midnight (22:00-02:00)
                // In range if >= 22:00 OR <= 02:00
                return currentTime >= start || currentTime <= end;
            }
        }
    }
}
