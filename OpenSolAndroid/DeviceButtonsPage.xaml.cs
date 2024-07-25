#nullable disable
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls;
using OpenSol.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SolAndroid;

public partial class DeviceButtonsPage : ContentPage
{
    private List<ButtonConfig> _buttons = new List<ButtonConfig>();

    public DeviceButtonsPage()
    {
        InitializeComponent();
        LoadButtons();
        RenderButtonsList();
    }

    private async void LoadButtons()
    {
        try
        {
            string json = Preferences.Get("ButtonsConfig", "");
            if (!string.IsNullOrEmpty(json))
            {
                var config = JsonSerializer.Deserialize<OpenSol.Core.ButtonsConfig>(json);
                if (config != null && config.Buttons != null)
                {
                    _buttons = config.Buttons;
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error loading buttons: {ex.Message}", "OK");
        }
    }

    private void RenderButtonsList()
    {
        ButtonsList.Children.Clear();

        if (_buttons.Count == 0)
        {
            ButtonsList.Children.Add(new Label 
            { 
                Text = "No buttons configured. Add your first button below.",
                FontAttributes = FontAttributes.Italic,
                TextColor = Colors.Gray
            });
            return;
        }

        foreach (var btn in _buttons)
        {
            var frame = new Border
            {
                StrokeThickness = 1,
                Stroke = Colors.LightGray,
                Padding = 10,
                Margin = new Thickness(0, 5)
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            var stack = new VerticalStackLayout { Spacing = 5 };
            stack.Children.Add(new Label { Text = btn.Description, FontAttributes = FontAttributes.Bold });
            stack.Children.Add(new Label { Text = $"ID: {btn.Id}", FontSize = 12, TextColor = Colors.Gray });
            
            if (!string.IsNullOrEmpty(btn.BackgroundColor) || !string.IsNullOrEmpty(btn.TextColor))
            {
                string colorInfo = "Colors: ";
                if (!string.IsNullOrEmpty(btn.BackgroundColor))
                    colorInfo += $"Bg={btn.BackgroundColor} ";
                if (!string.IsNullOrEmpty(btn.TextColor))
                    colorInfo += $"Text={btn.TextColor}";
                stack.Children.Add(new Label { Text = colorInfo, FontSize = 10, TextColor = Colors.DarkGray });
            }

            var deleteButton = new Button
            {
                Text = "Delete",
                BackgroundColor = Colors.Red,
                TextColor = Colors.White,
                WidthRequest = 80
            };

            string buttonId = btn.Id;
            deleteButton.Clicked += (s, e) => OnDeleteButton(buttonId);

            grid.Add(stack, 0, 0);
            grid.Add(deleteButton, 1, 0);

            frame.Content = grid;
            ButtonsList.Children.Add(frame);
        }
    }

    private async void OnAddButtonClicked(object sender, EventArgs e)
    {
        string id = txtNewId.Text?.Trim();
        string description = txtNewDescription.Text?.Trim();
        string backgroundColor = txtNewBackgroundColor.Text?.Trim();
        string textColor = txtNewTextColor.Text?.Trim();

        if (string.IsNullOrEmpty(id))
        {
            await DisplayAlert("Error", "Enter a device ID", "OK");
            return;
        }

        if (string.IsNullOrEmpty(description))
        {
            await DisplayAlert("Error", "Enter a description for the button", "OK");
            return;
        }

        if (_buttons.Exists(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlert("Error", "A button with this ID already exists", "OK");
            return;
        }

        var newButton = new ButtonConfig
        {
            Id = id,
            Description = description
        };

        if (!string.IsNullOrEmpty(backgroundColor))
            newButton.BackgroundColor = backgroundColor;
        
        if (!string.IsNullOrEmpty(textColor))
            newButton.TextColor = textColor;

        _buttons.Add(newButton);

        txtNewId.Text = "";
        txtNewDescription.Text = "";
        txtNewBackgroundColor.Text = "";
        txtNewTextColor.Text = "";

        RenderButtonsList();
    }

    private async void OnDeleteButton(string buttonId)
    {
        bool confirm = await DisplayAlert("Confirm", $"Delete button '{buttonId}'?", "Yes", "No");
        if (confirm)
        {
            _buttons.RemoveAll(b => b.Id == buttonId);
            RenderButtonsList();
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var config = new OpenSol.Core.ButtonsConfig { Buttons = _buttons };
            string json = JsonSerializer.Serialize(config);
            Preferences.Set("ButtonsConfig", json);
            
            await DisplayAlert("Saved", "Buttons configuration saved. Go back home to see the new buttons.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Save error: {ex.Message}", "OK");
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
