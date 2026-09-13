using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SMUI.App.Views;

/// <summary>内置浏览器页（一比一复刻 WinForms「浏览器」选项卡）：NativeWebView 懒初始化 + 手动释放。</summary>
public partial class BrowserPage : UserControl
{
    private const string 首页地址 = "https://www.nexusmods.com/stardewvalley";
    private const string Nexus账户页地址 = "https://users.nexusmods.com/auth/sign_in";
    private NativeWebView? _browser;

    public BrowserPage()
    {
        InitializeComponent();
    }

    /// <summary>按需创建浏览器控件（WinForms 懒初始化：只有需要使用时才完成初始化并创建控件）。</summary>
    private NativeWebView EnsureBrowser()
    {
        if (_browser != null) return _browser;
        _browser = new NativeWebView();
        _browser.NavigationCompleted += (_, _) => SyncAddress();
        BrowserHost.Children.Add(_browser);
        EmptyHint.IsVisible = false;
        return _browser;
    }

    /// <summary>释放浏览器控件（对齐 WinForms「释放」按钮）：移除并丢弃 NativeWebView。</summary>
    private void ReleaseBrowser()
    {
        if (_browser == null) return;
        BrowserHost.Children.Remove(_browser);
        _browser.Source = null;
        _browser = null;
        AddressBox.Text = "";
        EmptyHint.IsVisible = true;
    }

    private void SyncAddress()
    {
        if (_browser?.Source is { } uri && uri.Scheme.StartsWith("http"))
            AddressBox.Text = uri.ToString();
    }

    private void NavigateTo(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        Uri target;
        try
        {
            target = new Uri(url);
        }
        catch (UriFormatException)
        {
            return;
        }
        EnsureBrowser().Source = target;
        AddressBox.Text = url;
    }

    private void NexusAccount_Click(object? sender, RoutedEventArgs e) => NavigateTo(Nexus账户页地址);

    private void Release_Click(object? sender, RoutedEventArgs e) => ReleaseBrowser();

    private void Reload_Click(object? sender, RoutedEventArgs e)
    {
        if (_browser?.Source is { } uri) _browser.Source = uri; // 重设 Source 触发重新加载
    }

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        if (_browser is { CanGoBack: true }) _browser.GoBack();
    }

    private void Forward_Click(object? sender, RoutedEventArgs e)
    {
        if (_browser is { CanGoForward: true }) _browser.GoForward();
    }

    private void Stop_Click(object? sender, RoutedEventArgs e)
    {
        try { _browser?.Stop(); } catch { /* 平台不支持时忽略 */ }
    }

    private void Go_Click(object? sender, RoutedEventArgs e) => NavigateTo(AddressBox.Text);

    private void AddressBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) NavigateTo(AddressBox.Text);
    }
}
