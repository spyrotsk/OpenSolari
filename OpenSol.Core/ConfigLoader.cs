
using System;
using System.IO;
using System.Text.Json;

namespace OpenSol.Core
{
    /// <summary>
    /// Utility for loading and saving JSON configurations.
    /// </summary>
    public static class ConfigLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = OpenSolContext.Default
        };

        /// <summary>
        /// Loads device configuration from JSON file
        /// </summary>
        public static DevicesConfig LoadDevices(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new DevicesConfig();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize(json, OpenSolContext.Default.DevicesConfig) ?? new DevicesConfig();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading devices from {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves device configuration to JSON file
        /// </summary>
        public static void SaveDevices(string filePath, DevicesConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, OpenSolContext.Default.DevicesConfig);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving devices to {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads buttons configuration from JSON file
        /// </summary>
        public static ButtonsConfig LoadButtons(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new ButtonsConfig();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize(json, OpenSolContext.Default.ButtonsConfig) ?? new ButtonsConfig();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading buttons from {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves buttons configuration to JSON file
        /// </summary>
        public static void SaveButtons(string filePath, ButtonsConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, OpenSolContext.Default.ButtonsConfig);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving buttons to {filePath}: {ex.Message}", ex);
            }
        }
    }
}
