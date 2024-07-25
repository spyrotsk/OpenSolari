
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenSol.Core; // Use the Core library

namespace OpenSolWinServer
{
    public partial class Form1 : Form
    {
        private HttpServer? _server;
        private DeviceController _deviceController;


        private ServerConfig _config;
        private CheckBox chkEnableGeolock;
        private TextBox txtGeoLat;
        private TextBox txtGeoLon;

        public Form1()
        {
            InitializeComponent();
            _deviceController = new DeviceController(Log);
            _config = ConfigManager.LoadConfig();

            // Initialize Logger
            AppLogger.Initialize(_config.LogsEnabled, _config.LogsRetentionDays);
            
            // Populate UI from loaded config
            txtPort.Text = _config.Port.ToString();
            chkUseHttps.Checked = _config.UseHttps;
            chkLogs.Checked = _config.LogsEnabled;

            // Add "Manage Users" button dynamically to not break the Designer
            Button btnManageUsers = new Button();
            btnManageUsers.Text = "Manage Users";
            btnManageUsers.Location = new Point(6, 22); // Place near the old token
            btnManageUsers.Size = new Size(150, 30);
            btnManageUsers.Click += BtnManageUsers_Click;

            // token label
            foreach (Control c in grpConfig.Controls) { if (c is Label l && (l.Text.Contains("Auth") || l.Name == "label5")) l.Visible = false; }

            grpConfig.Controls.Add(btnManageUsers);

            // Geolock UI Controls
            var lblGeolock = new Label() { Text = "Geolock Settings:", Location = new Point(6, 60), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            chkEnableGeolock = new CheckBox() { Text = "Enable Geolock (Global)", Location = new Point(6, 80), AutoSize = true, Checked = _config.EnableGeolock };
            
            var lblLat = new Label() { Text = "Target Latitude:", Location = new Point(200, 60), AutoSize = true };
            txtGeoLat = new TextBox() { Location = new Point(200, 80), Width = 100, Text = _config.GeoLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            
            var lblLon = new Label() { Text = "Target Longitude:", Location = new Point(320, 60), AutoSize = true };
            txtGeoLon = new TextBox() { Location = new Point(320, 80), Width = 100, Text = _config.GeoLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture) };

            var lblHelp = new Label() { 
                Text = "Don't know how to get coordinates? See: https://support.google.com/maps/answer/18539", 
                Location = new Point(6, 105), 
                AutoSize = true, 
                ForeColor = Color.Blue, 
                Cursor = Cursors.Hand 
            };
            lblHelp.Click += (s, e) => {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://support.google.com/maps/answer/18539") { UseShellExecute = true }); } catch { }
            };

            grpConfig.Controls.Add(lblGeolock);
            grpConfig.Controls.Add(chkEnableGeolock);
            grpConfig.Controls.Add(lblLat);
            grpConfig.Controls.Add(txtGeoLat);
            grpConfig.Controls.Add(lblLon);
            grpConfig.Controls.Add(txtGeoLon);
            grpConfig.Controls.Add(lblHelp);

            LoadDevicesConfig();
        }

        private void BtnManageUsers_Click(object sender, EventArgs e)
        {
            using (var frm = new UsersForm())
            {
                frm.ShowDialog();
                // Reload config after users form closes
                _config = ConfigManager.LoadConfig();
                chkUseHttps.Checked = _config.UseHttps; // Sync again
            }
        }

        private void LoadDevicesConfig()
        {
            try
            {
                if (System.IO.File.Exists("devices_config.json"))
                {
                    var devicesConfig = ConfigLoader.LoadDevices("devices_config.json");
                    _deviceController.ConfigureDevices(devicesConfig.Devices);
                    Log($"Loaded {devicesConfig.Devices.Count} devices from devices_config.json");
                }
                else
                {
                    Log("File devices_config.json not found.");
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading devices: {ex.Message}");
            }
        }

        private void SaveConfigFromUI()
        {
            try
            {
                // Update _config object from UI
                if (int.TryParse(txtPort.Text, out int p)) _config.Port = p;
                _config.UseHttps = chkUseHttps.Checked;
                _config.LogsEnabled = chkLogs.Checked;
                _config.EnableGeolock = chkEnableGeolock.Checked;

                if (double.TryParse(txtGeoLat.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat))
                    _config.GeoLatitude = lat;
                
                if (double.TryParse(txtGeoLon.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                    _config.GeoLongitude = lon;

                // NOTE: Users are managed by UsersForm and saved directly from there,
                // keeping _config synced via reload or shared reference.
                // Here we only save network settings.
                ConfigManager.SaveConfig(_config);
            }
            catch (Exception ex)
            {
                Log($"Error saving config: {ex.Message}");
            }
        }

        private void btnStartStop_Click(object sender, EventArgs e)
        {
            if (_server != null && _server.IsRunning)
            {
                StopServer();
            }
            else
            {
                SaveConfigFromUI(); // Save UI config
                StartServer();
            }
        }

        private void StartServer()
        {
            if (!int.TryParse(txtPort.Text, out int port))
            {
                Log("Invalid port.");
                return;
            }

            // Reload devices
            LoadDevicesConfig();

            _server = new HttpServer(ctx => HandleRequest(ctx));
            _server.Logger = LogHttp; // Use structured logger
            _server.GeoConfig = _config;

            // Trigger load (and potential migration)
            AccessControlManager.LoadConfig();
            Log($"Authentication managed by Access Control Manager.");

            try
            {
                _server.Start(port, _config.UseHttps);
                lblStatus.Text = "Running";
                lblStatus.ForeColor = Color.Green;
                btnStartStop.Text = "Stop Server";
                Log($"Server started on port {port}.");

                grpConfig.Enabled = false;
            }
            catch (Exception ex)
            {
                Log($"Error starting server: {ex.Message}");
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopServer()
        {
            _server?.Stop();
            lblStatus.Text = "Stopped";
            lblStatus.ForeColor = Color.Red;
            btnStartStop.Text = "Start Server";
            Log("Server stopped.");

            grpConfig.Enabled = true;
        }

        private async Task<string?> HandleRequest(System.Net.HttpListenerContext context)
        {
            var request = context.Request;

            if (request.Url?.AbsolutePath == "/open")
            {
                string gateType = request.QueryString["gate"] ?? "";
                if (string.IsNullOrEmpty(gateType)) return null;

                Log($"Open command: {gateType}");

                // Delegated to shared controller
                bool success = await _deviceController.OpenGate(gateType);
                return success ? "OK" : "ERROR";
            }

            return null;
        }

        private void Log(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(Log), message);
                return;
            }
            string logMsg = $"[{DateTime.Now:HH:mm:ss}] {message}";
            txtLogs.AppendText($"{logMsg}{Environment.NewLine}");
            AppLogger.Log(message);
        }

        private void LogHttp(LogData log)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<LogData>(LogHttp), log);
                return;
            }

            // Format message as requested:
            // [HH:mm:ss] User (IP) : URL
            // OR
            // [HH:mm:ss] ip IP, msg. Request: URL

            string userInfo = log.User != null ? $"{log.User} ({log.Ip})" : $"ip {log.Ip}";
            string fullMessage = "";

            if (log.IsError)
            {
                fullMessage = $"[{log.Timestamp:HH:mm:ss}] {userInfo}, {log.Message} Request: {log.RequestUrl}";
            }
            else
            {
                fullMessage = $"[{log.Timestamp:HH:mm:ss}] {userInfo}: {log.RequestUrl}";
            }

            txtLogs.AppendText(fullMessage + Environment.NewLine);
            // Log the complete formatted line to file (without duplicate timestamp if the logger already adds it,
            // but AppLogger adds its own timestamp.
            // For consistency with AppLogger: should I pass only the "clean" message or accept double timestamp?
            // AppLogger does: [HH:mm:ss] message
            // fullMessage already has: [HH:mm:ss] ...
            
            // Log a simplified version to avoid double timestamp in file
            // Or modify AppLogger to not add timestamp if not requested?
            // For simplicity I log the full message, AppLogger will add another timestamp at the beginning.
            // File example: [10:00:00] [10:00:00] User...
            // Better to pass only content to AppLogger.
            
            string fileMsg = log.IsError 
                ? $"{userInfo}, {log.Message} Request: {log.RequestUrl}"
                : $"{userInfo}: {log.RequestUrl}";
                
            AppLogger.Log(fileMsg);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
