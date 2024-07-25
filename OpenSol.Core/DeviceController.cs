
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OpenSol.Core
{
    public class DeviceController
    {
        private HttpClient _httpClient;
        private CookieContainer _cookieContainer;
        private Action<string> _logger;

        // Dictionary of configured devices (ID -> DeviceConfig)
        private Dictionary<string, DeviceConfig> _devices = new Dictionary<string, DeviceConfig>();

        public DeviceController(Action<string> logger)
        {
            _logger = logger;
            _cookieContainer = new CookieContainer();
            // No proxy for fast local calls, dedicated handler
            var handler = new HttpClientHandler { CookieContainer = _cookieContainer, UseProxy = false };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Configures devices from a configuration list
        /// </summary>
        public void ConfigureDevices(List<DeviceConfig> devices)
        {
            _devices.Clear();
            foreach (var device in devices)
            {
                if (!string.IsNullOrEmpty(device.Id))
                {
                    _devices[device.Id.ToLower()] = device;
                }
            }
            _logger($"Configured {_devices.Count} devices.");
        }

        public async Task<bool> OpenGate(string deviceId)
        {
            DeviceConfig? device = null;
            string normalizedId = deviceId.ToLower();

            // Search in configured dictionary
            if (_devices.Count > 0 && _devices.ContainsKey(normalizedId))
            {
                device = _devices[normalizedId];
            }

            if (device == null || string.IsNullOrEmpty(device.Ip))
            {
                _logger($"Device '{deviceId}' not found or IP missing.");
                return false;
            }

            // Login to device
            string loginUrl = $"http://{device.Ip}/goform/UserLogin";
            string postDataLogin = $"username={device.Username}&pwd={device.Password}&Submit=Login";

            _logger($"Attempting login on {device.Ip} (ID: {deviceId})...");
            string? resp1 = await SendPostRequest(loginUrl, postDataLogin);
            if (resp1 == null) return false;

            // Output command
            string outputUrl = $"http://{device.Ip}/goform/con_SetOutput";
            string postDataOutput = $"secondi={device.EffectiveDuration}&decimi=3&outputNumber={device.OutputNum}&Select=Test%20Output%20Locale";

            _logger($"Sending command to {device.Ip} (Out: {device.OutputNum})...");
            string? resp2 = await SendPostRequest(outputUrl, postDataOutput);

            if (resp2 != null)
            {
                _logger("Command sent successfully.");
                return true;
            }
            return false;
        }

        private async Task<string?> SendPostRequest(string url, string postData)
        {
            try
            {
                var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
                HttpResponseMessage response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger($"HTTP error to {url}: {ex.Message}");
                return null;
            }
        }
    }
}
