using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using OpenSol.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace OpenSolariLinux
{
    public partial class MainWindow : Window
    {
        private HttpClient _httpClient;
        private string _serverIp = "127.0.0.1";
        private string _serverPort = "8080";
        private string _token = "";
        private bool _useHttps = false;
        private List<ButtonConfig> _buttonConfigs = new List<ButtonConfig>();

        // Window sizes (adapted for Avalonia, estimation)
        private const double HeightCollapsed = 250;
        private const double HeightExpanded = 550; // Increased for checkbox

        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(5);

            LoadConfig();
            LoadButtonsConfig();
            
            // Set initial UI state
            TxtServerIp.Text = _serverIp;
            TxtServerPort.Text = _serverPort;
            TxtToken.Text = _token;
            ChkUseHttps.IsChecked = _useHttps;
            
            // Initial state: Config hidden
            ToggleConfig(false);

            // Keyboard handler for ESC
            this.KeyDown += MainWindow_KeyDown;
        }

        private void LoadButtonsConfig()
        {
            try
            {
                if (File.Exists("buttons_config.json"))
                {
                    var config = ConfigLoader.LoadButtons("buttons_config.json");
                    _buttonConfigs = config.Buttons;
                    CreateDynamicButtons();
                }
                else
                {
                    // Use hardcoded buttons as fallback
                    ApriCancello.IsVisible = true;
                    ApriSbarra.IsVisible = true;
                    ApriPorta.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"Error loading buttons: {ex.Message}";
                // Fallback to hardcoded buttons
                ApriCancello.IsVisible = true;
                ApriSbarra.IsVisible = true;
                ApriPorta.IsVisible = true;
            }
        }

        private void CreateDynamicButtons()
        {
            // Hide hardcoded buttons
            ApriCancello.IsVisible = false;
            ApriSbarra.IsVisible = false;
            ApriPorta.IsVisible = false;

            foreach (var btnConfig in _buttonConfigs)
            {
                var btn = new Button
                {
                    Content = btnConfig.Description,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    FontSize = 20,
                    Tag = btnConfig.Id
                };

                // Imposta colori se specificati
                if (!string.IsNullOrEmpty(btnConfig.BackgroundColor))
                {
                    try
                    {
                        btn.Background = new SolidColorBrush(Color.Parse(btnConfig.BackgroundColor));
                    }
                    catch { /* Ignore invalid colors */ }
                }

                if (!string.IsNullOrEmpty(btnConfig.TextColor))
                {
                    try
                    {
                        btn.Foreground = new SolidColorBrush(Color.Parse(btnConfig.TextColor));
                    }
                    catch { /* Ignore invalid colors */ }
                }

                // Click handler
                btn.Click += async (s, e) => await SendCommand(btnConfig.Id);

                DynamicButtonsPanel.Children.Add(btn);
            }
        }

        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ToggleConfig(!GrpConfig.IsVisible);
                e.Handled = true;
            }
        }

        private void ToggleConfig(bool show)
        {
            GrpConfig.IsVisible = show;
            this.Height = show ? HeightExpanded : HeightCollapsed;
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists("config.txt"))
                {
                    var lines = File.ReadAllLines("config.txt");
                    if (lines.Length >= 2)
                    {
                        _serverIp = lines[0];
                        _serverPort = lines[1];
                    }
                    if (lines.Length >= 3)
                    {
                        _token = OpenSol.Core.SecurityUtils.Decrypt(lines[2]);
                    }
                    if (lines.Length >= 4)
                    {
                        bool.TryParse(lines[3], out _useHttps);
                    }
                }
            }
            catch { /* Ignore loading errors */ }
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllLines("config.txt", new[] { 
                    TxtServerIp.Text, 
                    TxtServerPort.Text, 
                    OpenSol.Core.SecurityUtils.Encrypt(TxtToken.Text),
                    ChkUseHttps.IsChecked.ToString()
                });
                _serverIp = TxtServerIp.Text;
                _serverPort = TxtServerPort.Text;
                _token = TxtToken.Text;
                _useHttps = ChkUseHttps.IsChecked ?? false;
                LblStatus.Text = "Configuration saved";
                ToggleConfig(false);
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"Save error: {ex.Message}";
            }
        }

        private void OnSaveConfigClick(object sender, RoutedEventArgs e)
        {
            SaveConfig();
        }

        private async void OnApriCancelloClick(object sender, RoutedEventArgs e) => await SendCommand("cancello");
        private async void OnApriSbarraClick(object sender, RoutedEventArgs e) => await SendCommand("sbarra");
        private async void OnApriPortaClick(object sender, RoutedEventArgs e) => await SendCommand("porta");

        private async Task SendCommand(string gateType)
        {
            string scheme = _useHttps ? "https" : "http";
            string url = $"{scheme}://{_serverIp}:{_serverPort}/open?gate={gateType}";
            LblStatus.Text = "Sending...";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(_token))
                {
                    request.Headers.Add("X-Auth-Token", _token);
                }

                var response = await _httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && content == "OK")
                {
                    LblStatus.Text = "Command sent successfully!";
                    await Task.Delay(2000);
                    LblStatus.Text = "Ready";
                }
                else
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        LblStatus.Text = "Error: Unauthorized (Wrong token?)";
                    else
                        LblStatus.Text = $"Server Error: {content}";
                }
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"Error: {ex.Message}";
            }
        }
    }
}
