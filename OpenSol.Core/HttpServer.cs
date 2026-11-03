
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

        // Wrong tokens per IP: 5 in a minute block the address for 15 minutes
        private readonly FailedAuthLimiter _authLimiter = new FailedAuthLimiter();

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
                    // Each request runs on its own task: a slow one (e.g. a wrong token, checked with PBKDF2) does not hold up the others
                    _ = Task.Run(() => ProcessRequest(context));
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

        private async Task ProcessRequest(HttpListenerContext context)
        {
            string? responseString = null;

            try
            {
                if (context.Request.ContentLength64 > 1024 * 1024)
                {
                    context.Response.Abort();
                    return;
                }

                string clientIp = GetClientIp(context.Request);
                string requestUrl = context.Request.Url?.ToString() ?? "Unknown URL";

                if (_authLimiter.IsBlocked(clientIp))
                {
                        context.Response.Abort();
                        return;
                }

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
                        if (_authLimiter.RegisterFailure(clientIp))
                        {
                            Logger?.Invoke(new LogData { Timestamp = DateTime.Now, Ip = clientIp, RequestUrl = requestUrl, Message = $"IP blocked for {_authLimiter.BlockDuration.TotalMinutes:F0} minutes after {_authLimiter.MaxFailures} invalid tokens", IsError = true });
                        }
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

                // Geolock Check: applies to every client, whatever X-Client-Type says (that header is set by the client).
                // Clients without GPS (Windows/Linux) are rejected: their users need IgnoreGeolock.
                if (GeoConfig != null && GeoConfig.EnableGeolock && !user.IgnoreGeolock)
                {
                    string? geoLatStr = context.Request.Headers["X-Geo-Lat"];
                    string? geoLonStr = context.Request.Headers["X-Geo-Lon"];

                    if (string.IsNullOrEmpty(geoLatStr) || string.IsNullOrEmpty(geoLonStr))
                    {
                        Logger?.Invoke(new LogData { Timestamp = DateTime.Now, User = userName, Ip = clientIp, RequestUrl = requestUrl, Message = "Command failed (Geolock: no GPS data; desktop users need Ignore Geolock)", IsError = true });
                        context.Response.Abort();
                        return;
                    }

                    if (double.TryParse(geoLatStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double clientLat) &&
                        double.TryParse(geoLonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double clientLon) &&
                        double.IsFinite(clientLat) && double.IsFinite(clientLon))
                    {
                        double distance = GeoUtils.CalculateDistance(GeoConfig.GeoLatitude, GeoConfig.GeoLongitude, clientLat, clientLon);
                        if (!(distance <= GeoConfig.MaxDistance))
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

                // Device Restriction Check
                if (context.Request.Url?.AbsolutePath == "/open")
                {
                    string gate = context.Request.QueryString["gate"] ?? "";
                    if (!user.IsDeviceAllowed(gate))
                    {
                        Logger?.Invoke(new LogData { Timestamp = DateTime.Now, User = userName, Ip = clientIp, RequestUrl = requestUrl, Message = $"Command failed (Device not allowed: {gate})", IsError = true });
                        context.Response.Abort();
                        return;
                    }
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

        /// <summary>
        /// Address of the client. Behind a reverse proxy on the same machine (e.g. Caddy) every request
        /// comes from localhost, so the address the proxy saw is read from X-Forwarded-For.
        /// The header is trusted only from localhost: anyone else could write any address in it.
        /// </summary>
        private static string GetClientIp(HttpListenerRequest request)
        {
            IPAddress remote = request.RemoteEndPoint.Address;
            if (IPAddress.IsLoopback(remote))
            {
                string? forwarded = request.Headers["X-Forwarded-For"];
                if (!string.IsNullOrWhiteSpace(forwarded))
                {
                    // The proxy appends the address it saw as the last entry
                    string last = forwarded.Split(',')[^1].Trim();
                    if (IPAddress.TryParse(last, out IPAddress? forwardedIp)) return forwardedIp.ToString();
                }
            }
            return remote.ToString();
        }
    }
}
