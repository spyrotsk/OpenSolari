using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenSol.Core
{
    public static class AppLogger
    {
        private static bool _enabled = false;
        private static int _retentionDays = 30; // Default retention
        private static string _logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private static object _lock = new object();

        public static void Initialize(bool enabled, int retentionDays)
        {
            _enabled = enabled;
            _retentionDays = retentionDays;

            if (_enabled)
            {
                try
                {
                    if (!Directory.Exists(_logsDirectory))
                    {
                        Directory.CreateDirectory(_logsDirectory);
                    }
                    CleanOldLogs();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing logger: {ex.Message}");
                }
            }
        }

        public static void Log(string message)
        {
            if (!_enabled) return;

            lock (_lock)
            {
                try
                {
                    string fileName = $"log_{DateTime.Now:yyyy-MM-dd}.txt";
                    string filePath = Path.Combine(_logsDirectory, fileName);
                    string logLine = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";

                    File.AppendAllText(filePath, logLine, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing log: {ex.Message}");
                }
            }
        }

        public static void CleanOldLogs()
        {
            if (!_enabled || _retentionDays <= 0) return;

            try
            {
                var directory = new DirectoryInfo(_logsDirectory);
                var files = directory.GetFiles("log_*.txt");
                var cleanupDate = DateTime.Now.Date.AddDays(-_retentionDays);

                foreach (var file in files)
                {
                    // Formato atteso: log_yyyy-MM-dd.txt
                    string name = Path.GetFileNameWithoutExtension(file.Name);
                    string datePart = name.Replace("log_", "");

                    if (DateTime.TryParse(datePart, out DateTime fileDate))
                    {
                        if (fileDate < cleanupDate)
                        {
                            file.Delete();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning old logs: {ex.Message}");
            }
        }
    }
}
