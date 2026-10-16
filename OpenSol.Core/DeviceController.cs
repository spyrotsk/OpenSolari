
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
            // No proxy for fast local calls, dedicated handler.
            // Credentials are used only if the device answers with an HTTP auth challenge (401 + WWW-Authenticate),
            // as some terminal firmwares protect /goform with Basic/Digest auth instead of the form login.
            var handler = new HttpClientHandler { CookieContainer = _cookieContainer, Credentials = new DeviceCredentials(this), PreAuthenticate = true, UseProxy = false };
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
            string postDataLogin = $"username={WebUtility.UrlEncode(device.Username)}&pwd={WebUtility.UrlEncode(device.Password)}&Submit=Login";

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

        /// <summary>
        /// Provides the configured device username/password to HttpClient when a device
        /// answers with an HTTP authentication challenge (Basic or Digest).
        /// </summary>
        private class DeviceCredentials : ICredentials
        {
            private readonly DeviceController _owner;

            public DeviceCredentials(DeviceController owner) => _owner = owner;

            public NetworkCredential? GetCredential(Uri uri, string authType)
            {
                foreach (var device in _owner._devices.Values)
                {
                    if (string.Equals(device.Ip, uri.Authority, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(device.Ip, uri.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        return new NetworkCredential(device.Username, device.Password);
                    }
                }
                return null;
            }
        }

        private async Task<string?> SendPostRequest(string url, string postData)
        {
            try
            {
                // Plain form content type, without "; charset=utf-8": some embedded web servers reject it
                var content = new StringContent(postData, Encoding.ASCII);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
                HttpResponseMessage response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    string authHeader = string.Join(", ", response.Headers.WwwAuthenticate.Select(h => h.ToString()));
                    _logger($"HTTP error to {url}: {(int)response.StatusCode} ({response.ReasonPhrase})" +
                            (string.IsNullOrEmpty(authHeader) ? "" : $" - WWW-Authenticate: {authHeader}"));
                    return null;
                }
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
