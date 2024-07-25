
using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OpenSol.Core
{
    public class LogData
    {
        public DateTime Timestamp { get; set; }
        public string? User { get; set; }
        public string Ip { get; set; } = "";
        public string RequestUrl { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsError { get; set; }
    }

    public class HttpServer
    {
        private HttpListener? _listener;
        private readonly Func<HttpListenerContext, Task<string?>> _requestHandler;
        public bool IsRunning { get; private set; }
        public ServerConfig? GeoConfig { get; set; }

        public HttpServer(Func<HttpListenerContext, Task<string?>> requestHandler)
        {
            _requestHandler = requestHandler;
        }

        public void Start(int port, bool useHttps = false)
        {
            if (IsRunning) return;

            _listener = new HttpListener();
            string scheme = useHttps ? "https" : "http";
            _listener.Prefixes.Add($"{scheme}://*:{port}/");
            try
            {
                _listener.Start();
                IsRunning = true;
                Task.Run(ListenLoop);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                throw new Exception($"Unable to start server {scheme} on port {port}. Make sure to run as administrator or that the port is free.", ex);
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            _listener?.Stop();
            _listener?.Close();
        }

        private async Task ListenLoop()
        {
            while (IsRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch (HttpListenerException)
                {
                    // Listener stopped or internal error
                }
                catch (ObjectDisposedException)
                {
                    // Listener closed
                }
            }
        }

        public Action<LogData>? Logger { get; set; }

        private async void ProcessRequest(HttpListenerContext context)
        {
            string? responseString = null;

            try
            {
                if (context.Request.ContentLength64 > 1024 * 1024)
                {
                    context.Response.Abort();
                    return;
                }

                string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                string requestUrl = context.Request.Url?.ToString() ?? "Unknown URL";

                // Verification Authentication
                string? clientToken = context.Request.Headers["X-Auth-Token"];
                
                if (string.IsNullOrEmpty(clientToken))
                {
                        Logger?.Invoke(new LogData { Timestamp = DateTime.Now, Ip = clientIp, RequestUrl = requestUrl, Message = "Command failed (No Token)", IsError = true });
                        context.Response.Abort();
                        return;
                }

                var user = AccessControlManager.Authenticate(clientToken);

                if (user == null)
                {
                        Logger?.Invoke(new LogData { Timestamp = DateTime.Now, Ip = clientIp, RequestUrl = requestUrl, Message = "Command failed (Invalid Token)", IsError = true });
                        context.Response.Abort();
                        return;
                }
                
                string userName = user.Username;

                // Access Control Check
                if (!AccessControlManager.IsAccessAllowed(userName, DateTime.Now))
                {
                        Logger?.Invoke(new LogData { Timestamp = DateTime.Now, User = userName, Ip = clientIp, RequestUrl = requestUrl, Message = "Command failed (Schedule Restriction)", IsError = true });
                        context.Response.Abort();
                        return;
                }

                // Geolock Check (only for Android clients)
                if (GeoConfig != null && GeoConfig.EnableGeolock && !user.IgnoreGeolock)
                {
                    string? clientType = context.Request.Headers["X-Client-Type"];
                    if (string.Equals(clientType, "Android", StringComparison.OrdinalIgnoreCase))
                    {
                        string? geoLatStr = context.Request.Headers["X-Geo-Lat"];
                        string? geoLonStr = context.Request.Headers["X-Geo-Lon"];

                        if (string.IsNullOrEmpty(geoLatStr) || string.IsNullOrEmpty(geoLonStr))
                        {
                            Logger?.Invoke(new LogData { Timestamp = DateTime.Now, User = userName, Ip = clientIp, RequestUrl = requestUrl, Message = "Command failed (No GPS data from Android)", IsError = true });
                            context.Response.Abort();
                            return;
                        }

                        if (double.TryParse(geoLatStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double clientLat) &&
                            double.TryParse(geoLonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double clientLon))
                        {
                            double distance = GeoUtils.CalculateDistance(GeoConfig.GeoLatitude, GeoConfig.GeoLongitude, clientLat, clientLon);
                            if (distance > GeoConfig.MaxDistance)
                            {
                                Logger?.Invoke(new LogData { Timestamp = DateTime.Now, User = userName, Ip = clientIp, RequestUrl = requestUrl, Message = $"Command failed (Geolock: {distance:F0}m > {GeoConfig.MaxDistance}m)", IsError = true });
                                context.Response.Abort();
                                return;
                            }
                        }
                        else
                        {
                            Logger?.Invoke(new LogData { Timestamp = DateTime.Now, User = userName, Ip = clientIp, RequestUrl = requestUrl, Message = "Command failed (Invalid GPS data)", IsError = true });
                            context.Response.Abort();
                            return;
                        }
                    }
                    // Non-Android clients (Windows/Linux) skip geolock check
                }

                Logger?.Invoke(new LogData { Timestamp = DateTime.Now, User = userName, Ip = clientIp, RequestUrl = requestUrl, Message = "OK", IsError = false });

                responseString = await _requestHandler(context);
                
                if (responseString == null)
                {
                    context.Response.Abort();
                    return;
                }

                context.Response.StatusCode = 200;
            }
            catch (Exception ex)
            {
                Logger?.Invoke(new LogData { Timestamp = DateTime.Now, Ip = "Server", Message = $"Error: {ex.Message}", IsError = true });
                try { context.Response.Abort(); } catch { }
                return;
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            try
            {
                using var output = context.Response.OutputStream;
                await output.WriteAsync(buffer, 0, buffer.Length);
            }
            catch { }
            finally
            {
                try { context.Response.Close(); } catch { }
            }
        }
    }
}
