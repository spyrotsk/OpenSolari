using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;

namespace SolAndroid;

public partial class LocationPickerPage : ContentPage
{
    public double SelectedLatitude { get; private set; }
    public double SelectedLongitude { get; private set; }
    public bool LocationConfirmed { get; private set; } = false;
    private readonly Action<double, double>? _onLocationConfirmed;
    private bool _userInteracted = false;

    public LocationPickerPage(double initialLat, double initialLon, Action<double, double>? onLocationConfirmed = null)
    {
        InitializeComponent();
        SelectedLatitude = initialLat;
        SelectedLongitude = initialLon;
        _onLocationConfirmed = onLocationConfirmed;
        
        UpdateCoordsLabel();
        LoadMapHtml(initialLat, initialLon);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = AutoFetchGpsAsync();
    }

    private async Task AutoFetchGpsAsync()
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location != null && !_userInteracted)
            {
                SelectedLatitude = location.Latitude;
                SelectedLongitude = location.Longitude;
                UpdateCoordsLabel();

                string latStr = location.Latitude.ToString(CultureInfo.InvariantCulture);
                string lonStr = location.Longitude.ToString(CultureInfo.InvariantCulture);

                await mapWebView.EvaluateJavaScriptAsync($"if(window.setMarkerPosition) setMarkerPosition({latStr}, {lonStr});");
            }
        }
        catch
        {
            // Silently ignore background GPS errors
        }
    }

    private void UpdateCoordsLabel()
    {
        lblCoords.Text = $"Lat: {SelectedLatitude.ToString("F6", CultureInfo.InvariantCulture)}, Lon: {SelectedLongitude.ToString("F6", CultureInfo.InvariantCulture)}";
    }

    private void LoadMapHtml(double lat, double lon)
    {
        string latStr = lat.ToString(CultureInfo.InvariantCulture);
        string lonStr = lon.ToString(CultureInfo.InvariantCulture);

        string html = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        html, body, #map {{ height: 100%; width: 100%; margin: 0; padding: 0; background-color: #1e293b; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        var map = L.map('map').setView([{latStr}, {lonStr}], 17);
        
        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            maxZoom: 19,
            attribution: '© OpenStreetMap'
        }}).addTo(map);

        var marker = L.marker([{latStr}, {lonStr}], {{ draggable: true }}).addTo(map);

        function updatePosition(lat, lng) {{
            window.location.href = 'locationpicker://pos?lat=' + lat + '&lon=' + lng;
        }}

        marker.on('dragend', function(e) {{
            var pos = marker.getLatLng();
            updatePosition(pos.lat, pos.lng);
        }});

        map.on('click', function(e) {{
            marker.setLatLng(e.latlng);
            updatePosition(e.latlng.lat, e.latlng.lng);
        }});

        window.setMarkerPosition = function(lat, lng) {{
            var newLatLng = new L.LatLng(lat, lng);
            marker.setLatLng(newLatLng);
            map.panTo(newLatLng);
        }};
    </script>
</body>
</html>";

        mapWebView.Source = new HtmlWebViewSource
        {
            Html = html
        };
    }

    private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        if (e.Url != null && e.Url.StartsWith("locationpicker://pos"))
        {
            e.Cancel = true;
            _userInteracted = true;
            try
            {
                var uri = new Uri(e.Url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                string? latStr = query["lat"];
                string? lonStr = query["lon"];

                if (!string.IsNullOrEmpty(latStr) && !string.IsNullOrEmpty(lonStr) &&
                    double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                {
                    SelectedLatitude = lat;
                    SelectedLongitude = lon;
                    UpdateCoordsLabel();
                }
            }
            catch { /* Ignore parsing issues */ }
        }
    }

    private async void OnRecenterGpsClicked(object sender, EventArgs e)
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location != null)
            {
                SelectedLatitude = location.Latitude;
                SelectedLongitude = location.Longitude;
                UpdateCoordsLabel();

                string latStr = location.Latitude.ToString(CultureInfo.InvariantCulture);
                string lonStr = location.Longitude.ToString(CultureInfo.InvariantCulture);
                
                await mapWebView.EvaluateJavaScriptAsync($"setMarkerPosition({latStr}, {lonStr});");
            }
            else
            {
                await DisplayAlert("Error", "Unable to get current GPS location.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"GPS error: {ex.Message}", "OK");
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        LocationConfirmed = true;
        _onLocationConfirmed?.Invoke(SelectedLatitude, SelectedLongitude);
        await Navigation.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        LocationConfirmed = false;
        await Navigation.PopAsync();
    }
}
