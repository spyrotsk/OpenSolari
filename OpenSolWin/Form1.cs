
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenSol.Core;

namespace Cancello
{
    public partial class Form1 : Form
    {
        private HttpClient _httpClient;
        private List<ButtonConfig> _buttonConfigs = new List<ButtonConfig>();

        // Settings (In a real app I would use Properties.Settings)
        private string ServerIp = "127.0.0.1";
        private string ServerPort = "8080";
        private string Token = "";
        private bool UseHttps = false;

        public Form1()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        // Window sizes (hardcoded for simplicity based on Designer)
        private const int HeightCollapsed = 230; // 177 (end of buttons) + borders/title (~40-50)
        private const int HeightExpanded = 450; // With config

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadSettings();
            txtServerIP.Text = ServerIp;
            txtServerPort.Text = ServerPort;
            txtToken.Text = Token;
            chkUseHttps.Checked = UseHttps;

            // Load buttons from JSON
            LoadButtonsConfig();

            // Initial state: Config hidden
            ToggleConfig(false);
        }

        private void LoadButtonsConfig()
        {
            try
            {
                if (System.IO.File.Exists("buttons_config.json"))
                {
                    var config = ConfigLoader.LoadButtons("buttons_config.json");
                    _buttonConfigs = config.Buttons;
                    CreateDynamicButtons();
                }
                else
                {
                    // Use hardcoded buttons as fallback
                    ApriCancello_btn.Visible = true;
                    ApriSbarra_btn.Visible = true;
                    ApriPorta_btn.Visible = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading buttons: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Fallback to hardcoded buttons
                ApriCancello_btn.Visible = true;
                ApriSbarra_btn.Visible = true;
                ApriPorta_btn.Visible = true;
            }
        }

        private void CreateDynamicButtons()
        {
            // Hide hardcoded buttons
            ApriCancello_btn.Visible = false;
            ApriSbarra_btn.Visible = false;
            ApriPorta_btn.Visible = false;

            int yPosition = 12;
            foreach (var btnConfig in _buttonConfigs)
            {
                var btn = new Button
                {
                    Text = btnConfig.Description,
                    Location = new System.Drawing.Point(12, yPosition),
                    Size = new System.Drawing.Size(187, 51),
                    Font = new System.Drawing.Font("Segoe UI", 18F),
                    Tag = btnConfig.Id // Save ID in Tag
                };

                // Set colors if specified
                if (!string.IsNullOrEmpty(btnConfig.BackgroundColor))
                {
                    try
                    {
                        btn.BackColor = ColorTranslator.FromHtml(btnConfig.BackgroundColor);
                        btn.UseVisualStyleBackColor = false;
                    }
                    catch { /* Ignore invalid colors */ }
                }

                if (!string.IsNullOrEmpty(btnConfig.TextColor))
                {
                    try
                    {
                        btn.ForeColor = ColorTranslator.FromHtml(btnConfig.TextColor);
                    }
                    catch { /* Ignore invalid colors */ }
                }

                // Click handler
                btn.Click += async (s, e) => await SendCommand(btnConfig.Id);

                this.Controls.Add(btn);
                yPosition += 57; // Spacing between buttons
            }

            // Adjust window size
            if (_buttonConfigs.Count > 0)
            {
                int newHeight = yPosition + 10;
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, newHeight);
                // Move grpConfig under buttons
                grpConfig.Location = new System.Drawing.Point(12, yPosition);
            }
        }

        private void ToggleConfig(bool show)
        {
            grpConfig.Visible = show;
            // Set client height, the window border is added automatically
            // Buttons end at Y=177. Let's add some margin.
            this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, show ? HeightExpanded : (grpConfig.Location.Y + 10)); 
            // Better logic: show ? HeightExpanded : (current button bottom + margin)
            // But preserving existing logic structure slightly modified for dynamic height
            if (show) 
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, 230 + (HeightExpanded - 230)); // Simple expand
            else 
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, grpConfig.Location.Y + 10);
            
            // Actually, allow standard resize logic
             this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, show ? (grpConfig.Location.Y + grpConfig.Height + 10) : (grpConfig.Location.Y + 10));
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                ToggleConfig(!grpConfig.Visible);
                e.Handled = true;
                e.SuppressKeyPress = true; // Avoid system "ding"
            }
        }

        private void LoadSettings()
        {
            // Very simple file-based loading to avoid complexity
            // If a config.txt file exists, read it
            try
            {
                if (System.IO.File.Exists("config.txt"))
                {
                    var lines = System.IO.File.ReadAllLines("config.txt");
                    if (lines.Length >= 2)
                    {
                        ServerIp = lines[0];
                        ServerPort = lines[1];
                    }
                    if (lines.Length >= 3)
                    {
                        Token = OpenSol.Core.SecurityUtils.Decrypt(lines[2]);
                    }
                    if (lines.Length >= 4)
                    {
                        bool.TryParse(lines[3], out UseHttps);
                    }
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                System.IO.File.WriteAllLines("config.txt", new[] { 
                    txtServerIP.Text, 
                    txtServerPort.Text, 
                    OpenSol.Core.SecurityUtils.Encrypt(txtToken.Text),
                    chkUseHttps.Checked.ToString()
                });
                ServerIp = txtServerIP.Text;
                ServerPort = txtServerPort.Text;
                Token = txtToken.Text;
                UseHttps = chkUseHttps.Checked;
                MessageBox.Show("Configuration saved.");
                ToggleConfig(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save error: {ex.Message}");
            }
        }

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private async void ApriCancello_btn_Click(object sender, EventArgs e)
        {
            await SendCommand("cancello");
        }

        private async void ApriSbarra_btn_Click(object sender, EventArgs e)
        {
            await SendCommand("sbarra");
        }

        private async void ApriPorta_btn_Click(object sender, EventArgs e)
        {
            await SendCommand("porta");
        }

        private async Task SendCommand(string gateType)
        {
            string scheme = UseHttps ? "https" : "http";
            string url = $"{scheme}://{ServerIp}:{ServerPort}/open?gate={gateType}";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(Token))
                {
                    request.Headers.Add("X-Auth-Token", Token);
                }

                var response = await _httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && content == "OK")
                {
                    // Silent success or small visual feedback?
                    // MessageBox.Show("Command sent!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // For now silent as the original app seemed to be (more or less)
                }
                else
                {
                    // If not authorized, specific message
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                         MessageBox.Show("Access Denied: Invalid or missing token.", "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show($"Error from server: {content}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Communication error: {ex.Message}", "Communication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}