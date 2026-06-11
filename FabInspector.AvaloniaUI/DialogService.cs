using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Threading.Tasks;

namespace FabInspector.AvaloniaUI;

internal static class DialogService
{
    public static async Task ShowMessageAsync(Window owner, string title, string message)
    {
        var dialog = CreateDialog(owner, title, message, showCancelButton: false, out _, out var okButton);
        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ShowConfirmationAsync(Window owner, string title, string message)
    {
        var result = false;
        var dialog = CreateDialog(owner, title, message, showCancelButton: true, out var cancelButton, out var okButton);

        okButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        if (cancelButton != null)
        {
            cancelButton.Click += (_, _) => dialog.Close();
        }

        await dialog.ShowDialog(owner);
        return result;
    }

    private static Window CreateDialog(Window owner, string title, string message, bool showCancelButton, out Button? cancelButton, out Button okButton)
    {
        cancelButton = null;
        okButton = new Button
        {
            Content = showCancelButton ? "Yes" : "OK",
            MinWidth = 96,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 12,
            Children =
            {
                okButton
            }
        };

        if (showCancelButton)
        {
            cancelButton = new Button
            {
                Content = "No",
                MinWidth = 96
            };
            buttons.Children.Add(cancelButton);
        }

        return new Window
        {
            Title = title,
            Width = 460,
            MinWidth = 360,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.Parse("#1A2133")),
            Content = new Border
            {
                Padding = new Thickness(24),
                Child = new StackPanel
                {
                    Spacing = 20,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 400
                        },
                        buttons
                    }
                }
            },
            Icon = owner.Icon
        };
    }
}
