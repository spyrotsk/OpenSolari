using System.Text.Json;
using OpenSol.Core;
using System.Text;
using System.Globalization;

namespace SolAndroid;

public partial class Config : ContentPage
{
	public Config()
	{
		InitializeComponent();
        LoadConfig();
    }

    private void LoadConfig()
    {
        if (Preferences.ContainsKey("ServerIP"))
            txtServerIP.Text = Preferences.Get("ServerIP", "");
        
        if (Preferences.ContainsKey("ServerPort"))
            txtServerPort.Text = Preferences.Get("ServerPort", "8080");

        if (Preferences.ContainsKey("UseHttps"))
            swUseHttps.IsToggled = Preferences.Get("UseHttps", false);

        // Decrypt AuthToken when loading
        txtToken.Text = OpenSol.Core.SecurityUtils.Decrypt(Preferences.Get("AuthToken", ""));

        if (Preferences.ContainsKey("TargetLat") && Preferences.ContainsKey("TargetLon") &&
            !string.IsNullOrWhiteSpace(Preferences.Get("TargetLat", "")) &&
            !string.IsNullOrWhiteSpace(Preferences.Get("TargetLon", "")))
        {
            txtTargetLat = Preferences.Get("TargetLat", "");
            txtTargetLon = Preferences.Get("TargetLon", "");
            lblTargetCoords.Text = $"Target: {txtTargetLat}, {txtTargetLon}";
            lblTargetCoords.TextColor = Colors.Green;
        }
        else
        {
            txtTargetLat = "";
            txtTargetLon = "";
            lblTargetCoords.Text = "Target: Not set";
            lblTargetCoords.TextColor = Colors.Gray;
        }

        double radius = Preferences.Get("GeofenceRadiusMeters", 500.0);
        sldGeofenceRadius.Value = radius;
        UpdateRadiusLabel(radius);

        string accuracy = Preferences.Get("GeolocationAccuracy", "Best");
        pkrGeoAccuracy.SelectedItem = accuracy;
    }

    private void OnRadiusSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        double radius = Math.Round(e.NewValue / 50.0) * 50.0;
        UpdateRadiusLabel(radius);
    }

    private void UpdateRadiusLabel(double radius)
    {
        if (radius >= 1000)
        {
            lblGeofenceRadius.Text = $"Max Allowed Distance Radius: {radius / 1000.0:0.0#} km ({radius} m)";
        }
        else
        {
            lblGeofenceRadius.Text = $"Max Allowed Distance Radius: {radius} m";
        }
    }

    // We still need these strings even if the entries are gone, 
    // to manage the data internally between capture and save.
    private string txtTargetLat = "";
    private string txtTargetLon = "";

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        Preferences.Set("ServerIP", txtServerIP.Text);
        Preferences.Set("ServerPort", txtServerPort.Text);
        Preferences.Set("UseHttps", swUseHttps.IsToggled);
        // Encrypt AuthToken when saving
        Preferences.Set("AuthToken", OpenSol.Core.SecurityUtils.Encrypt(txtToken.Text));

        Preferences.Set("TargetLat", txtTargetLat);
        Preferences.Set("TargetLon", txtTargetLon);
        Preferences.Set("GeofenceRadiusMeters", sldGeofenceRadius.Value);

        string selectedAccuracy = pkrGeoAccuracy.SelectedItem?.ToString() ?? "Best";
        Preferences.Set("GeolocationAccuracy", selectedAccuracy);

        await DisplayAlert("Saved", "Configuration saved successfully.", "OK");
        await Navigation.PopAsync(); // Go back if navigated
    }

    private async void OnManageButtonsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DeviceButtonsPage());
    }

    private void OnClearLocationClicked(object sender, EventArgs e)
    {
        txtTargetLat = "";
        txtTargetLon = "";
        lblTargetCoords.Text = "Target: Not set";
        lblTargetCoords.TextColor = Colors.Gray;
    }

    private async void OnGeolocateClicked(object sender, EventArgs e)
    {
        try
        {
            double initialLat = 41.9028;
            double initialLon = 12.4964;

            if (double.TryParse(txtTargetLat, NumberStyles.Float, CultureInfo.InvariantCulture, out double savedLat) &&
                double.TryParse(txtTargetLon, NumberStyles.Float, CultureInfo.InvariantCulture, out double savedLon))
            {
                initialLat = savedLat;
                initialLon = savedLon;
            }

            var pickerPage = new LocationPickerPage(initialLat, initialLon, (lat, lon) =>
            {
                txtTargetLat = lat.ToString(CultureInfo.InvariantCulture);
                txtTargetLon = lon.ToString(CultureInfo.InvariantCulture);

                lblTargetCoords.Text = $"Target set to: {txtTargetLat}, {txtTargetLon}";
                lblTargetCoords.TextColor = Colors.Green;
            });

            await Navigation.PushAsync(pickerPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Location error: {ex.Message}", "OK");
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var config = new AppConfigExport
            {
                ServerIP = txtServerIP.Text,
                ServerPort = txtServerPort.Text,
                UseHttps = swUseHttps.IsToggled,
                AuthToken = Preferences.Get("AuthToken", ""), // Save the encrypted one
                TargetLat = txtTargetLat,
                TargetLon = txtTargetLon,
                GeofenceRadiusMeters = sldGeofenceRadius.Value,
                GeoAccuracy = pkrGeoAccuracy.SelectedItem?.ToString() ?? "Best",
                ExportDate = DateTime.Now
            };

            // Load buttons
            string buttonsJson = Preferences.Get("ButtonsConfig", "");
            if (!string.IsNullOrEmpty(buttonsJson))
            {
                var bConfig = JsonSerializer.Deserialize<OpenSol.Core.ButtonsConfig>(buttonsJson);
                if (bConfig != null)
                {
                    config.Buttons = bConfig.Buttons;
                }
            }

            string exportJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            string fileName = $"OpenSolConfig_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string cacheFile = Path.Combine(FileSystem.CacheDirectory, fileName);
            
            await File.WriteAllTextAsync(cacheFile, exportJson);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export OpenSol Configuration",
                File = new ShareFile(cacheFile)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error during export: {ex.Message}", "OK");
        }
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select configuration file (.json)",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.WinUI, new[] { ".json" } }
                })
            });

            if (result == null) return;

            using var stream = await result.OpenReadAsync();
            using var reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();

            var config = JsonSerializer.Deserialize<AppConfigExport>(json);
            if (config == null)
            {
                await DisplayAlert("Error", "Invalid configuration file.", "OK");
                return;
            }

            bool confirm = await DisplayAlert("Confirm", "Importing this configuration will overwrite current data. Continue?", "Yes", "No");
            if (!confirm) return;

            // Save parameters in Preferences
            Preferences.Set("ServerIP", config.ServerIP);
            Preferences.Set("ServerPort", config.ServerPort);
            Preferences.Set("UseHttps", config.UseHttps);
            Preferences.Set("AuthToken", config.AuthToken);
            Preferences.Set("TargetLat", config.TargetLat);
            Preferences.Set("TargetLon", config.TargetLon);
            if (config.GeofenceRadiusMeters > 0)
            {
                Preferences.Set("GeofenceRadiusMeters", config.GeofenceRadiusMeters);
            }
            if (!string.IsNullOrWhiteSpace(config.GeoAccuracy))
            {
                Preferences.Set("GeolocationAccuracy", config.GeoAccuracy);
            }
            
            if (config.Buttons != null && config.Buttons.Count > 0)
            {
                var buttonsConfig = new OpenSol.Core.ButtonsConfig { Buttons = config.Buttons };
                string buttonsJson = JsonSerializer.Serialize(buttonsConfig);
                Preferences.Set("ButtonsConfig", buttonsJson);
            }

            // Reload UI
            LoadConfig();

            await DisplayAlert("Imported", "Configuration imported successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error during import: {ex.Message}", "OK");
        }
    }
}