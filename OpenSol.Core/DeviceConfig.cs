
namespace OpenSol.Core
{
    /// <summary>
    /// Configuration for a single controllable device.
    /// </summary>
    public class DeviceConfig
    {
        /// <summary>
        /// Unique identifier for the device (e.g., "1", "2", "gate", etc.)
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// IP address of the device
        /// </summary>
        public string Ip { get; set; } = string.Empty;

        /// <summary>
        /// Output number to activate
        /// </summary>
        public string OutputNum { get; set; } = "1";

        /// <summary>
        /// Output activation duration in seconds
        /// </summary>
        public string? Duration { get; set; }

        /// <summary>
        /// [DEPRECATED] Output activation duration in seconds. Use 'Duration' instead.
        /// This property will be removed by end of 2026.
        /// </summary>
        public string? Secondi { get; set; }

        /// <summary>
        /// Gets the effective duration value, preferring Duration over Secondi for backward compatibility
        /// </summary>
        public string EffectiveDuration => Duration ?? Secondi ?? "0";

        /// <summary>
        /// Username for device login
        /// </summary>
        public string Username { get; set; } = "webterm";

        /// <summary>
        /// Password for device login
        /// </summary>
        public string Password { get; set; } = "webterm";
    }

    /// <summary>
    /// Container for device list
    /// </summary>
    public class DevicesConfig
    {
        public List<DeviceConfig> Devices { get; set; } = new List<DeviceConfig>();
    }
}
