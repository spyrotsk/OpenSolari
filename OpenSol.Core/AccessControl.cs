using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenSol.Core
{
    public class UserAccessRule
    {
        public string Username { get; set; } = "";
        
        // Salted hash of the user's code (see SecurityUtils.HashSecret)
        public string Token { get; set; } = "";

        public bool IgnoreSchedule { get; set; } = false;
        public bool IgnoreGeolock { get; set; } = false;

        // Key: DayOfWeek (English, e.g. "Monday"), Value: "HH:mm-HH:mm"
        public Dictionary<string, string> Schedule { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Device IDs (from devices_config.json) this user may open.
        // null (field missing) = all devices (backward compatibility), "*" = all devices, [] = no devices.
        public List<string>? AllowedDevices { get; set; }

        public bool IsDeviceAllowed(string deviceId)
        {
            if (AllowedDevices == null) return true;
            return AllowedDevices.Any(d => d == "*" || d.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
        }
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

        // Codes already verified, so PBKDF2 runs once per code. Value: username and the hash it matched.
        private static readonly ConcurrentDictionary<string, (string Username, string Hash)> _verifiedTokenCache = new ConcurrentDictionary<string, (string Username, string Hash)>();

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
                   return _cachedConfig;
                }
            }

            try
            {
                string json = File.ReadAllText(ConfigFileName);
                _cachedConfig = JsonSerializer.Deserialize(json, OpenSolContext.Default.AccessControlConfig) ?? new AccessControlConfig();
                _lastLoadTime = DateTime.Now;

                // Migrate tokens from the old encrypted (or plain) format to salted hashes
                bool migrated = false;
                foreach (var user in _cachedConfig.Users)
                {
                    if (!string.IsNullOrEmpty(user.Token) && !SecurityUtils.IsHashedSecret(user.Token))
                    {
                        user.Token = SecurityUtils.HashSecret(SecurityUtils.Decrypt(user.Token));
                        migrated = true;
                    }
                }

                if (migrated)
                {
                    SecureFile.WriteAllText(ConfigFileName, JsonSerializer.Serialize(_cachedConfig, OpenSolContext.Default.AccessControlConfig));
                }
            }
            catch (Exception)
            {
                _cachedConfig = new AccessControlConfig();
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
                SecureFile.WriteAllText(ConfigFileName, json);
                
                // Force reload to update cache
                _lastLoadTime = DateTime.MinValue;
                LoadConfig();
            }
            catch (Exception) { }
        }

        // An empty code keeps the existing user's code
        public static void AddUser(string username, string? code, Dictionary<string, string> schedule, bool ignoreSchedule = false, bool ignoreGeolock = false, List<string>? allowedDevices = null)
        {
            var config = LoadConfig();
            var existing = config.Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(code) && code.Length < SecurityUtils.MinCodeLength)
                throw new ArgumentException($"The code must be at least {SecurityUtils.MinCodeLength} characters long.", nameof(code));

            string? tokenHash = string.IsNullOrEmpty(code) ? null : SecurityUtils.HashSecret(code);
            
            if (existing != null)
            {
                if (tokenHash != null) existing.Token = tokenHash;
                existing.Schedule = schedule;
                existing.IgnoreSchedule = ignoreSchedule;
                existing.IgnoreGeolock = ignoreGeolock;
                existing.AllowedDevices = allowedDevices;
            }
            else
            {
                if (tokenHash == null) throw new ArgumentException("A code is required for a new user.", nameof(code));

                config.Users.Add(new UserAccessRule 
                { 
                    Username = username, 
                    Token = tokenHash, 
                    Schedule = schedule,
                    IgnoreSchedule = ignoreSchedule,
                    IgnoreGeolock = ignoreGeolock,
                    AllowedDevices = allowedDevices
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
            var config = LoadConfig();

            if (_verifiedTokenCache.TryGetValue(token, out var cached))
            {
                // Still valid only if the user still has the same hash
                var cachedUser = config.Users.FirstOrDefault(u => u.Username == cached.Username && u.Token == cached.Hash);
                if (cachedUser != null) return cachedUser;
                _verifiedTokenCache.TryRemove(token, out _);
            }

            foreach (var user in config.Users)
            {
                if (SecurityUtils.VerifySecret(token, user.Token))
                {
                    _verifiedTokenCache[token] = (user.Username, user.Token);
                    return user;
                }
            }
            return null;
        }

        public static bool IsAccessAllowed(string username, DateTime verifyTime)
        {
            var config = LoadConfig();
            var rule = config.Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            return IsAccessAllowed(rule, verifyTime);
        }

        /// <summary>
        /// Checks the schedule of the user returned by Authenticate.
        /// A missing user (e.g. removed in the meantime) is denied.
        /// </summary>
        public static bool IsAccessAllowed(UserAccessRule? rule, DateTime verifyTime)
        {
            if (rule == null) return false;

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
