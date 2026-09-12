using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Diagnostics;

namespace SMUI.App.Views;

/// <summary>浏览器启动器页面：输入网址后在系统浏览器中打开（内置 WebView 等后续版本加入）。</summary>
public partial class BrowserPage : UserControl
{
    public BrowserPage()
    {
        InitializeComponent();
        AddressBox.Text = "https://www.nexusmods.com/stardewvalley/mods";
    }

    private void NavigateTo(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        AddressBox.Text = url;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打开浏览器失败: {ex.Message}");
        }
    }

    private void Go_Click(object? sender, RoutedEventArgs e) => NavigateTo(AddressBox.Text);

    private void AddressBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) NavigateTo(AddressBox.Text);
    }

    private void OpenExternal_Click(object? sender, RoutedEventArgs e) => NavigateTo(AddressBox.Text);
    private void OpenNexus_Click(object? sender, RoutedEventArgs e) => NavigateTo("https://www.nexusmods.com/stardewvalley/mods");
    private void OpenSmapi_Click(object? sender, RoutedEventArgs e) => NavigateTo("https://smapi.io");
    private void OpenModDrop_Click(object? sender, RoutedEventArgs e) => NavigateTo("https://www.moddrop.com/stardew-valley");
}
