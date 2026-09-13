using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SMUI.App.ViewModels;

namespace SMUI.App.Views;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();

        var index = 0;
        foreach (var child in LeftNavPanel.Children)
        {
            var captured = index;
            if (child is RadioButton radio)
                radio.IsCheckedChanged += (_, _) =>
                {
                    // 过滤取消选中事件，避免切换时刷新两次/回到上一页
                    if (radio.IsChecked != true) return;
                    ShowInner(captured);
                };
            index++;
        }
        ShowInner(0);

        DataContextChanged += (_, _) =>
        {
            if (DataContext is HomeViewModel vm)
            {
                vm.RefreshStatus();
                vm.NavigateRequested += navIndex =>
                {
                    // 转发到主窗口选项卡
                    if (this.VisualRoot is MainWindow main)
                        main.SelectNav(navIndex);
                };
            }
        };
    }

    /// <summary>左侧竖排菜单切换（顺序复刻 UiTabControlMenu1：欢迎页/设置/扩展内容/最新模组/集成工具/更新记录/关于）。</summary>
    private void ShowInner(int index)
    {
        NavWelcome.IsVisible = index == 0;
        NavSettings.IsVisible = index == 1;
        NavExtensions.IsVisible = index == 2;
        NavLatestMods.IsVisible = index == 3;
        NavTools.IsVisible = index == 4;
        NavUpdateLog.IsVisible = index == 5;
        NavAbout.IsVisible = index == 6;

        switch (index)
        {
            case 0 when DataContext is HomeViewModel vm0:
                vm0.RefreshStatus();
                break;
            case 2 when DataContext is HomeViewModel vm2:
                vm2.Plugins.Refresh();
                break;
            case 5:
                LoadUpdateLog();
                break;
        }
    }

    /// <summary>加载更新记录（随包分发的 UpdateLog.txt，由 WinForms 版 UpdateLog.rtf 转换而来）。</summary>
    private void LoadUpdateLog()
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, "UpdateLog.txt");
            UpdateLogText.Text = File.Exists(file)
                ? File.ReadAllText(file)
                : "未找到 UpdateLog.txt（更新记录文件随安装包分发）。";
        }
        catch (Exception ex)
        {
            UpdateLogText.Text = "读取更新记录失败：" + ex.Message;
        }
    }

    private void GoMods_Click(object? sender, RoutedEventArgs e)
    {
        (this.VisualRoot as MainWindow)?.SelectNav(1);
    }

    private void OpenRepo_Click(object? sender, RoutedEventArgs e)
        => HomeViewModel.OpenUrl("https://github.com/XiHanQWQ/SMUI6");

    private void OpenLink_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url } && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            HomeViewModel.OpenUrl(url);
    }

    /// <summary>卡片“+”按钮：转到下载更新页并预填 NEXUS 模组地址（复刻 Form下载并新建项）。</summary>
    private async void DownloadCreate_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url } || DataContext is not HomeViewModel vm) return;
        if (this.VisualRoot is not MainWindow main || main.DataContext is not MainWindowViewModel mainVm) return;
        main.SelectNav(3); // 下载更新
        await mainVm.Updates.DownloadAndCreateItemAsync(url);
    }
}
