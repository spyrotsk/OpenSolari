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

        if (Preferences.ContainsKey("TargetLat") && Preferences.ContainsKey("TargetLon"))
        {
            txtTargetLat = Preferences.Get("TargetLat", "");
            txtTargetLon = Preferences.Get("TargetLon", "");
            lblTargetCoords.Text = $"Target: {txtTargetLat}, {txtTargetLon}";
        }
        else
        {
            lblTargetCoords.Text = "Target: Not set";
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

        await DisplayAlert("Saved", "Configuration saved successfully.", "OK");
        await Navigation.PopAsync(); // Go back if navigated
    }

    private async void OnManageButtonsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DeviceButtonsPage());
    }

    private async void OnGeolocateClicked(object sender, EventArgs e)
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location != null)
            {
                txtTargetLat = location.Latitude.ToString(CultureInfo.InvariantCulture);
                txtTargetLon = location.Longitude.ToString(CultureInfo.InvariantCulture);
                
                lblTargetCoords.Text = $"Target set to: {txtTargetLat}, {txtTargetLon}";
                lblTargetCoords.TextColor = Colors.Green;

                // Save immediately so it's not lost if user doesn't hit Save but hits Cancel (optional)
                // Actually better to keep it in class variable and save it together with other settings.
            }
            else
            {
                await DisplayAlert("Error", "Unable to get location.", "OK");
            }
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