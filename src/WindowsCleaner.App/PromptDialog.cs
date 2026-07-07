using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowsCleaner.App;

/// <summary>Minimal dark-themed modal input dialog used by partition actions.</summary>
public static class PromptDialog
{
    /// <summary>Shows the prompt and returns the entered text, or null if cancelled.</summary>
    public static string? Show(Window owner, string title, string message, string defaultText = "")
    {
        var window = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x20))
        };

        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var box = new TextBox
        {
            Text = defaultText,
            Padding = new Thickness(6),
            FontSize = 14,
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x31)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44)),
            CaretBrush = Brushes.White
        };
        root.Children.Add(box);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        string? result = null;
        var ok = new Button { Content = "OK", Width = 88, Height = 30, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 88, Height = 30, IsCancel = true };
        ok.Click += (_, _) => { result = box.Text; window.DialogResult = true; };

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        window.Content = root;
        window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };

        return window.ShowDialog() == true ? result : null;
    }
}
