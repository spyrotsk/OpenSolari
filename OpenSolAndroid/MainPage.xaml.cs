using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;
using OpenSol.Core;

namespace SolAndroid
{
    public partial class MainPage : ContentPage
    {
        private HttpClient client;
        private List<ButtonConfig> _buttonConfigs = new List<ButtonConfig>();
        private Location? _currentLocation;

        public MainPage()
        {
            InitializeComponent();
            client = new HttpClient();
            // Set short timeout to not block UI for too long in case of errors
            client.Timeout = TimeSpan.FromSeconds(5);
            
            SolAndroid.App.VoiceCommandReceived += OnVoiceCommandReceived;
        }

        private async void OnVoiceCommandReceived(string featureName)
        {
            if (_buttonConfigs == null || _buttonConfigs.Count == 0)
            {
                // Ensure buttons are loaded
                LoadButtonsConfig();
            }

            var targetBtn = _buttonConfigs?.FirstOrDefault(b => b.Description.Equals(featureName, StringComparison.OrdinalIgnoreCase));
            
            if (targetBtn != null)
            {
                await SendCommand(targetBtn.Id);
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    resultLabel.Text = $"Command '{featureName}' not found";
                    resultLabel.TextColor = Colors.Red;
                });
            }
        }

        private GeolocationAccuracy GetConfiguredAccuracy()
        {
            string accuracyStr = Preferences.Get("GeolocationAccuracy", "Best");
            return accuracyStr switch
            {
                "Medium" => GeolocationAccuracy.Medium,
                "High" => GeolocationAccuracy.High,
                _ => GeolocationAccuracy.Best
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            LoadButtonsConfig();
            await UpdateLocationAsync(GetConfiguredAccuracy(), 3);
        }

        private async Task UpdateLocationAsync(GeolocationAccuracy accuracy, int timeoutSeconds)
        {
            try
            {
                var request = new GeolocationRequest(accuracy, TimeSpan.FromSeconds(timeoutSeconds));
                _currentLocation = await Geolocation.Default.GetLocationAsync(request);
            }
            catch (Exception)
            {
                // Almeno mettici un log un giorno. 🙄
            }
        }

        private bool IsDistanceExcessive()
        {
            if (_currentLocation == null) return true;

            string targetLatStr = Preferences.Get("TargetLat", "");
            string targetLonStr = Preferences.Get("TargetLon", "");
            double maxRadiusMeters = Preferences.Get("GeofenceRadiusMeters", 500.0);

            if (double.TryParse(targetLatStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double targetLat) &&
                double.TryParse(targetLonStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double targetLon))
            {
                double distanceMeters = OpenSol.Core.GeoUtils.CalculateDistance(targetLat, targetLon, _currentLocation.Latitude, _currentLocation.Longitude);
                return distanceMeters > maxRadiusMeters;
            }

            return false; // Se non ci sono coordinate nel config, passa oltre
        }

        private void LoadButtonsConfig()
        {
            try
            {
                string json = Preferences.Get("ButtonsConfig", "");
                
                if (!string.IsNullOrEmpty(json))
                {
                    var config = JsonSerializer.Deserialize<OpenSol.Core.ButtonsConfig>(json);
                    if (config != null && config.Buttons != null && config.Buttons.Count > 0)
                    {
                        _buttonConfigs = config.Buttons;
                        CreateDynamicButtons();

#if ANDROID
                        var context = Android.App.Application.Context;
                        MainActivity.UpdateDynamicShortcuts(context, _buttonConfigs);
#endif
                        return;
                    }
                }
                
                // No configuration found: show message
                ShowNoButtonsMessage();
            }
            catch (Exception ex)
            {
                resultLabel.Text = $"Error loading buttons: {ex.Message}";
                resultLabel.TextColor = Colors.Orange;
            }
        }

        private void ShowNoButtonsMessage()
        {
            var layout = (VerticalStackLayout)((ScrollView)Content).Content;
            
            // Remove old dynamic buttons if present
            ClearDynamicButtons(layout);
            
            // Show informative message
            resultLabel.Text = "No buttons configured. Go to Config > Manage Buttons to add some.";
            resultLabel.TextColor = Colors.Orange;
        }

        private void ClearDynamicButtons(VerticalStackLayout layout)
        {
            // Remove all Button children
            var toRemove = new List<IView>();
            foreach (var child in layout.Children)
            {
                if (child is Button)
                {
                    toRemove.Add(child);
                }
            }
            
            foreach (var view in toRemove)
            {
                layout.Children.Remove(view);
            }
        }

        private void CreateDynamicButtons()
        {
            var layout = (VerticalStackLayout)((ScrollView)Content).Content;
            
            // Remove old dynamic buttons
            ClearDynamicButtons(layout);

            // Add dynamic buttons
            int insertIndex = 1; // After the image
            foreach (var btnConfig in _buttonConfigs)
            {
                var btn = new Button
                {
                    Text = btnConfig.Description,
                    WidthRequest = 300
                };

                // Set colors if specified
                if (!string.IsNullOrEmpty(btnConfig.BackgroundColor))
                {
                    try
                    {
                        btn.BackgroundColor = Color.Parse(btnConfig.BackgroundColor);
                    }
                    catch { /* Ignore invalid colors */ }
                }

                if (!string.IsNullOrEmpty(btnConfig.TextColor))
                {
                    try
                    {
                        btn.TextColor = Color.Parse(btnConfig.TextColor);
                    }
                    catch { /* Ignore invalid colors */ }
                }

                // Click handler
                string deviceId = btnConfig.Id; // Capture in closure
                btn.Clicked += async (s, e) => await SendCommand(deviceId);

                layout.Children.Insert(insertIndex++, btn);
            }
        }



        private bool IsTargetLocationSet()
        {
            string targetLatStr = Preferences.Get("TargetLat", "");
            string targetLonStr = Preferences.Get("TargetLon", "");

            return !string.IsNullOrWhiteSpace(targetLatStr) &&
                   !string.IsNullOrWhiteSpace(targetLonStr) &&
                   double.TryParse(targetLatStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) &&
                   double.TryParse(targetLonStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
        }

        private async Task SendCommand(string gateType)
        {
            string serverIp = Preferences.Get("ServerIP", "");
            string serverPort = Preferences.Get("ServerPort", "8080");
            bool useHttps = Preferences.Get("UseHttps", false);

            if (string.IsNullOrEmpty(serverIp))
            {
                resultLabel.Text = "Configuration missing! Go to Config.";
                resultLabel.TextColor = Colors.Orange;
                return;
            }

            string scheme = useHttps ? "https" : "http";
            string url = $"{scheme}://{serverIp}:{serverPort}/open?gate={gateType}";
            string token = OpenSol.Core.SecurityUtils.Decrypt(Preferences.Get("AuthToken", ""));

            try
            {
                if (IsTargetLocationSet())
                {
                    resultLabel.Text = "Acquiring location...";
                    resultLabel.TextColor = Colors.Yellow;

                    await UpdateLocationAsync(GetConfiguredAccuracy(), 10);

                    if (IsDistanceExcessive())
                    {
                        resultLabel.Text = "Incorrect position. Excessive distance.";
                        resultLabel.TextColor = Colors.Red;
                        return;
                    }
                }

                resultLabel.Text = "Sending...";
                resultLabel.TextColor = Colors.Yellow;

                var requestMsg = new HttpRequestMessage(HttpMethod.Get, url);
                requestMsg.Headers.Add("X-Client-Type", "Android");
                
                if (!string.IsNullOrEmpty(token))
                {
                    requestMsg.Headers.Add("X-Auth-Token", token);
                }

                if (_currentLocation != null)
                {
                    requestMsg.Headers.Add("X-Geo-Lat", _currentLocation.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    requestMsg.Headers.Add("X-Geo-Lon", _currentLocation.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                HttpResponseMessage response = await client.SendAsync(requestMsg);
                
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    if (content == "OK")
                    {
                        resultLabel.Text = "Ok";
                        resultLabel.TextColor = Colors.White; // Or Green if you prefer
                    }
                    else
                    {
                        resultLabel.Text = "Server Error";
                        resultLabel.TextColor = Colors.Red;
                    }
                }
                else
                {
                    resultLabel.Text = $"HTTP Error: {response.StatusCode}";
                    resultLabel.TextColor = Colors.Red;
                }
            }
            catch (Exception ex)
            {
                resultLabel.Text = $"Error: {ex.Message}";
                resultLabel.TextColor = Colors.Magenta;
            }
        }
    }
}
