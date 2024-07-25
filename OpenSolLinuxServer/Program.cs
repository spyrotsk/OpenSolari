using System;
using System.Net;
using System.Threading.Tasks;
using OpenSol.Core;

namespace OpenSolLinuxServer
{
    class Program
    {
        private static HttpServer? _server;
        private static DeviceController? _deviceController;

        static async Task Main(string[] args)
        {
            Console.WriteLine("OpenSol Linux Server");

            // 1. Load Server Configuration
            var config = ConfigManager.LoadConfig();

            // Initialize File Logger
            AppLogger.Initialize(config.LogsEnabled, config.LogsRetentionDays);

            // CLI Arguments Management
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--port" && i + 1 < args.Length)
                {
                     if (int.TryParse(args[++i], out int p)) config.Port = p;
                     ConfigManager.SaveConfig(config); // Save port change immediately
                }
                else if (args[i] == "--add-user" && i + 1 < args.Length)
                {
                    // Required Format: --add-user Name --auth Code [--schedule "Mon:08-18,..."]
                    string name = args[++i];
                    string code = "";
                    string scheduleStr = "";
                    bool ignoreGeolock = false;

                    // Parse other args
                    while (i + 1 < args.Length)
                    {
                        if (args[i + 1] == "--auth")
                        {
                            i++;
                            code = args[++i];
                        }
                        else if (args[i + 1] == "--schedule")
                        {
                            i++;
                            scheduleStr = args[++i];
                        }
                        else if (args[i + 1] == "--ignore-geolock")
                        {
                            i++;
                            ignoreGeolock = true;
                        }
                        else if (bfs_IsCommand(args[i+1])) // Stop if another command starts (though usually we execute one command at a time)
                        {
                            break;
                        }
                        else
                        {
                            break; // Unknown arg
                        }
                    }

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(code))
                    {
                        var schedule = ParseSchedule(scheduleStr);
                        // If user provided a schedule, we use it (ignoreSchedule = false).
                        // If NO schedule provided:
                        // - If it's a NEW user -> Full Access (IgnoreSchedule = true) to match old behavior or straightforward CLI usage?
                        // - In Windows UI we asked. Here we assume Full Access if no schedule is provided for simplicity of CLI.
                        bool ignoreSchedule = (schedule.Count == 0);

                        string encryptedToken = SecurityUtils.Encrypt(code);
                        AccessControlManager.AddUser(name, encryptedToken, schedule, ignoreSchedule, ignoreGeolock);
                        
                        Console.WriteLine($"User '{name}' added/updated successfully.");
                        if (ignoreSchedule) Console.WriteLine("- Access: FULL (24/7)");
                        else Console.WriteLine($"- Access: Limited ({schedule.Count} schedule rules)");
                        
                        if (ignoreGeolock) Console.WriteLine("- Geolock: BYPASSED");
                        
                        return; // Exit after command
                    }
                    else
                    {
                        Console.WriteLine("Error: Incomplete format. Use: --add-user Name --auth Code [--schedule \"...\"]");
                        return;
                    }
                }
                else if (args[i] == "--remove-user" && i + 1 < args.Length)
                {
                    string nameToRemove = args[++i];
                    AccessControlManager.RemoveUser(nameToRemove);
                    Console.WriteLine($"User '{nameToRemove}' removed (if existed).");
                    return; 
                }
                else if (args[i] == "--list-users")
                {
                    var acConfig = AccessControlManager.LoadConfig();
                    Console.WriteLine("Registered Users:");
                    foreach (var u in acConfig.Users)
                    {
                        string info = u.IgnoreSchedule ? "(Admin/Full)" : "(Schedule)";
                        if (u.IgnoreGeolock) info += " (No Geolock)";
                        Console.WriteLine($"- {u.Username} {info}");
                    }
                    return; 
                }
            }

            // Save config mainly for port changes if not already saved
            ConfigManager.SaveConfig(config);

            _deviceController = new DeviceController(Console.WriteLine);
            
            // 2. Load Devices from JSON
            string devicesPath = "devices_config.json";
            if (System.IO.File.Exists(devicesPath))
            {
                try
                {
                    var devicesConfig = ConfigLoader.LoadDevices(devicesPath);
                    _deviceController.ConfigureDevices(devicesConfig.Devices);
                    Console.WriteLine($"Loaded {devicesConfig.Devices.Count} devices from {devicesPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error configuring devices: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"WARNING: File {devicesPath} not found. No devices configured.");
            }

            // 3. Start Server
            // HttpServer internally uses AccessControlManager for Auth, so we don't need to pass a list of users manually.
            _server = new HttpServer(ctx => HandleRequest(ctx));
            _server.Logger = LogToConsole;
            _server.GeoConfig = config;
            
            Console.WriteLine($"Starting server on port {config.Port} (HTTPS: {config.UseHttps})...");

            try
            {
                _server.Start(config.Port, config.UseHttps);
                Console.WriteLine("Server started. Press Ctrl+C to exit.");
                
                // Infinite keep alive
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal Error: {ex.Message}");
            }
        }
        
        private static bool bfs_IsCommand(string arg)
        {
            return arg.StartsWith("--");
        }

        private static Dictionary<string, string> ParseSchedule(string input)
        {
            var schedule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(input)) return schedule;

            // Format: "Day:Start-End,Day:Start-End"
            var parts = input.Split(',');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length == 2)
                {
                    string key = kv[0].Trim(); // Day Name
                    string val = kv[1].Trim(); // Time Range
                    schedule[key] = val;
                }
            }
            return schedule;
        }

        private static void LogToConsole(LogData log)
        {
            var defaultColor = Console.ForegroundColor;
            
            // "Date/Time, User and IP"
            string baseUserInfo = log.User != null ? $"{log.User} ({log.Ip})" : $"ip {log.Ip}";
            string userInfo = $"[{log.Timestamp:HH:mm:ss}] {baseUserInfo}";

            if (log.IsError)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(userInfo);
                
                Console.ForegroundColor = defaultColor;
                Console.WriteLine($", {log.Message} Request: {log.RequestUrl}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(userInfo);
                
                Console.ForegroundColor = defaultColor;
                Console.WriteLine($": {log.RequestUrl}");
            }
            Console.ForegroundColor = defaultColor;

            // Log to file
            string fileMsg = log.IsError
               ? $"{baseUserInfo}, {log.Message} Request: {log.RequestUrl}"
               : $"{baseUserInfo}: {log.RequestUrl}";
            AppLogger.Log(fileMsg);
        }

        private static async Task<string?> HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            // Logging handles by HttpServer
            
            if (request.Url?.AbsolutePath == "/open")
            {
                string gateType = request.QueryString["gate"] ?? "";
                if (string.IsNullOrEmpty(gateType)) return null;

                bool success = await _deviceController!.OpenGate(gateType);
                return success ? "OK" : "ERROR";
            }

            return null;
        }
    }
}
