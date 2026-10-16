
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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

        // Web pages that carry the CSRF tokens on newer firmwares (e.g. fs22 2.3.x)
        private const string SignInPage = "/html/signin.asp";
        private const string OutputPage = "/html/diagnostic/output.asp";
        private const string OutputFormId = "SetOutputForm";

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

            string baseUrl = $"http://{device.Ip}";

            // Newer firmwares require the CSRF tokens published in the sign-in page.
            // Older firmwares have no such tokens: in that case the legacy request is sent unchanged.
            var signInTokens = ParseCsrfTokens(await GetPage(baseUrl + SignInPage, baseUrl));
            bool useCsrf = signInTokens.Count > 0;
            if (useCsrf)
            {
                // Cookie set by the web interface when the cookie banner is accepted
                _cookieContainer.Add(new Uri(baseUrl), new Cookie("cookie-policy-consent", "true", "/"));
            }

            // Login to device
            string loginUrl = $"{baseUrl}/goform/UserLogin";
            string? resp1;
            if (useCsrf)
            {
                _logger($"Attempting login on {device.Ip} (ID: {deviceId}, CSRF)...");
                // Same body as the web interface: username, pwd and the sign-in page tokens, URL-encoded once
                resp1 = await SendPostRequest(loginUrl, BuildCsrfLogin(device, signInTokens), baseUrl);
            }
            else
            {
                var loginFields = new List<KeyValuePair<string, string>>
                {
                    new("username", device.Username),
                    new("pwd", device.Password),
                    new("Submit", "Login")
                };
                _logger($"Attempting login on {device.Ip} (ID: {deviceId})...");
                resp1 = await SendPostRequest(loginUrl, loginFields);
            }
            if (resp1 == null) return false;

            // Output command
            string outputUrl = $"{baseUrl}/goform/con_SetOutput";
            var outputFields = new List<KeyValuePair<string, string>>
            {
                new("secondi", device.EffectiveDuration),
                new("decimi", "3"),
                new("outputNumber", device.OutputNum)
            };
            if (useCsrf)
            {
                // The output page token is bound to the session just opened, so it is read after the login
                string? outputPage = await GetPage(baseUrl + OutputPage, baseUrl);
                outputFields.AddRange(ParseCsrfTokens(outputPage));
                string? formAction = ParseFormAction(outputPage, OutputFormId);
                if (!string.IsNullOrEmpty(formAction))
                {
                    outputUrl = formAction.StartsWith("http") ? formAction : baseUrl + "/" + formAction.TrimStart('/');
                }
                // Head selector of SetOutputForm: empty value = MAIN (the terminal itself)
                outputFields.Add(new("testa", ""));
                outputFields.Add(new("comando", "Output"));
            }
            else
            {
                outputFields.Add(new("Select", "Test Output Locale"));
            }

            _logger($"Sending command to {device.Ip} (Out: {device.OutputNum})...");
            string? resp2 = await SendPostRequest(outputUrl, outputFields, useCsrf ? baseUrl : null);

            if (resp2 != null)
            {
                _logger("Command sent successfully.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Downloads a page of the device web interface. Returns null if it is not available.
        /// </summary>
        private async Task<string?> GetPage(string url, string? browserOrigin = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddBrowserHeaders(request, browserOrigin);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                LogSetCookie(url, response);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger($"HTTP error to {url}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Logs the cookies a device sets (names only), to check that the session cookie is kept.
        /// </summary>
        private void LogSetCookie(string url, HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
            var names = values.Select(v => v.Split('=')[0].Trim());
            _logger($"{url} set cookies: {string.Join(", ", names)} (stored: {_cookieContainer.GetCookies(new Uri(url)).Count})");
        }

        private static List<KeyValuePair<string, string>> BuildCsrfLogin(DeviceConfig device, List<KeyValuePair<string, string>> tokens)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new("username", device.Username),
                new("pwd", device.Password)
            };
            fields.AddRange(tokens);
            return fields;
        }

        /// <summary>
        /// Headers sent by the web interface (jQuery ajax). Newer firmwares may check them as part of the CSRF protection.
        /// </summary>
        private static void AddBrowserHeaders(HttpRequestMessage request, string? browserOrigin)
        {
            if (browserOrigin == null) return;
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("Referer", browserOrigin + "/");
            if (request.Method == HttpMethod.Post)
            {
                request.Headers.TryAddWithoutValidation("Origin", browserOrigin);
            }
        }

        /// <summary>
        /// Extracts the hidden CSRF fields (csrftokensource, csrftoken_*) from a page.
        /// </summary>
        private static List<KeyValuePair<string, string>> ParseCsrfTokens(string? html)
        {
            var tokens = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(html)) return tokens;

            foreach (Match input in Regex.Matches(html, @"<input\b[^>]*>", RegexOptions.IgnoreCase))
            {
                string? name = GetAttribute(input.Value, "name") ?? GetAttribute(input.Value, "id");
                if (name == null || !name.StartsWith("csrftoken", StringComparison.OrdinalIgnoreCase)) continue;
                if (tokens.Exists(t => t.Key == name)) continue;
                tokens.Add(new(name, WebUtility.HtmlDecode(GetAttribute(input.Value, "value") ?? string.Empty)));
            }
            return tokens;
        }

        /// <summary>
        /// Reads the "formaction" attribute of the given form, as the web interface does.
        /// </summary>
        private static string? ParseFormAction(string? html, string formId)
        {
            if (string.IsNullOrEmpty(html)) return null;
            foreach (Match form in Regex.Matches(html, @"<form\b[^>]*>", RegexOptions.IgnoreCase))
            {
                if (GetAttribute(form.Value, "id") == formId)
                {
                    return GetAttribute(form.Value, "formaction") ?? GetAttribute(form.Value, "action");
                }
            }
            return null;
        }

        private static string? GetAttribute(string tag, string attribute)
        {
            var match = Regex.Match(tag, $@"(?<![\w-]){attribute}\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
            if (!match.Success) return null;
            return match.Groups[1].Success ? match.Groups[1].Value
                 : match.Groups[2].Success ? match.Groups[2].Value
                 : match.Groups[3].Value;
        }

        private async Task<string?> SendPostRequest(string url, List<KeyValuePair<string, string>> fields, string? browserOrigin = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                AddBrowserHeaders(request, browserOrigin);
                request.Content = new FormUrlEncodedContent(fields);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                LogSetCookie(url, response);
                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (body.Length > 200) body = body.Substring(0, 200);
                    var sent = fields.Select(f => f.Key == "pwd" ? "pwd=***" : $"{f.Key}({f.Value.Length})");
                    int cookies = _cookieContainer.GetCookies(new Uri(url)).Count;
                    _logger($"HTTP error to {url}: {(int)response.StatusCode} ({response.ReasonPhrase}) - fields: {string.Join(", ", sent)} - cookies: {cookies} - body: {body.Trim()}");
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
