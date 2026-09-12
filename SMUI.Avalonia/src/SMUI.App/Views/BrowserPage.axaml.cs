using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using AvaloniaWebView;

namespace SMUI.App.Views;

/// <summary>内置浏览器页（复刻 WinForms"浏览器"选项卡）：WebView 承载 NEXUS 取参 / ModDrop / 通用浏览。</summary>
public partial class BrowserPage : UserControl
{
    private readonly WebView? Browser;

    private const string 首页地址 = "https://www.nexusmods.com/stardewvalley";

    public BrowserPage()
    {
        InitializeComponent();
        try
        {
            BrowserHost.Children.Add(Browser = new WebView());
        }
        catch (Exception ex)
        {
            BrowserHost.Children.Add(new TextBlock
            {
                Text = "内置浏览器初始化失败：" + ex.Message + "\n请使用【外部打开】按钮在系统浏览器中继续。",
                Margin = new Avalonia.Thickness(16),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Avalonia.Media.Brushes.OrangeRed,
            });
        }
        AttachedToVisualTree += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(AddressBox.Text))
            {
                AddressBox.Text = 首页地址;
                NavigateTo(首页地址);
            }
        };
    }

    private void NavigateTo(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        if (Browser is null) { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); return; }
        AddressBox.Text = url;
        Browser.Url = new Uri(url);
    }

    private void Go_Click(object? sender, RoutedEventArgs e) => NavigateTo(AddressBox.Text);

    private void Reload_Click(object? sender, RoutedEventArgs e)
    {
        if (Browser is null) return;
        Browser.Url = new Uri(AddressBox.Text);
    }

    private void AddressBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) NavigateTo(AddressBox.Text);
    }

    private void OpenExternal_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AddressBox.Text)) return;
        Process.Start(new ProcessStartInfo(AddressBox.Text) { UseShellExecute = true });
    }
}
