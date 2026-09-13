using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.ViewModels;

namespace SMUI.App.Views;

public partial class ModsPage : UserControl
{
    public ModsPage()
    {
        InitializeComponent();

        // 描述编辑框失焦自动保存（axaml 的 LostFocus 事件）
        DescriptionEditor.LostFocus += (_, _) =>
        {
            if (DataContext is ModsPageViewModel vm)
                vm.SaveDescriptionIfEditing();
        };

        ItemsList.SelectionChanged += (_, _) =>
        {
            if (DataContext is ModsPageViewModel vm)
                vm.SyncSelection(ItemsList.Selection.SelectedItems.ToList());
        };

        // 右键时先选中鼠标所在的行（否则菜单命令作用不到目标行）
        ItemsList.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(ItemsList).Properties.IsRightButtonPressed)
                SelectRowUnderMouse(ItemsList, e);
        };
        CategoryList.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(CategoryList).Properties.IsRightButtonPressed)
                SelectRowUnderMouse(CategoryList, e);
        };

        // 底部信息条（复刻 Panel1：更新键/依赖项表/UniqueID 表/作者表）
        BtnUpdateKeys.Click += (_, _) =>
        {
            if (DataContext is not ModsPageViewModel vm) return;
            var info = vm.CurrentInfo;
            if (info == null) { _ = AppServices.Dialogs.InfoAsync("更新键", "请先选中一个模组项。"); return; }

            // 复刻 WinForms 更新模组.生成更新地址表菜单
            static Avalonia.Media.Imaging.Bitmap? Bmp(string name)
            {
                try
                {
                    using var s = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://SMUI/Assets/Menu/{name}.png"));
                    return new Avalonia.Media.Imaging.Bitmap(s);
                }
                catch { return null; }
            }
            static MenuItem Item(string header, string? icon, System.Windows.Input.ICommand cmd, object? param = null)
                => new()
                {
                    Header = header,
                    Command = cmd,
                    CommandParameter = param,
                    Icon = icon == null ? null : new Avalonia.Controls.Image { Source = Bmp(icon), Width = 16, Height = 16 },
                };
            void CopyLink(string url)
            {
                if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip)
                {
                    var transfer = new Avalonia.Input.DataTransfer();
                    transfer.Add(Avalonia.Input.DataTransferItem.CreateText(url));
                    _ = clip.SetDataAsync(transfer);
                }
            }

            var flyout = new MenuFlyout();
            var hasAny = false;

            foreach (var nid in info.NexusIds)
            {
                hasAny = true;
                var url = $"https://www.nexusmods.com/stardewvalley/mods/{nid}";
                flyout.Items.Add(Item($"NEXUS: {nid}", "NEXUS", new RelayCommand(() => OpenUrl(url))));
                flyout.Items.Add(Item("复制链接", null, new RelayCommand(() => CopyLink(url))));
                flyout.Items.Add(Item("从 NEXUS API 更新", "NEXUS",
                    new AsyncRelayCommand(() => vm.RunNexusUpdateForAsync(nid))));
            }
            if (info.NexusIds.Count > 0) flyout.Items.Add(new Separator());
            flyout.Items.Add(Item("自由输入 NEXUS ID", "NEXUS", new AsyncRelayCommand(async () =>
            {
                var id = await AppServices.Dialogs.InputAsync("打开模组页面", "输入你要访问的 NEXUS 模组 ID");
                if (!string.IsNullOrWhiteSpace(id)) await vm.RunNexusUpdateForAsync(id.Trim());
            })));

            if (info.ModDropIds.Count > 0) flyout.Items.Add(new Separator());
            foreach (var mid in info.ModDropIds)
            {
                hasAny = true;
                var url = $"https://www.moddrop.com/stardew-valley/mods/{mid}";
                flyout.Items.Add(Item($"ModDrop: {mid}", "ModDrop-White32", new RelayCommand(() => OpenUrl(url))));
                flyout.Items.Add(Item("复制链接", null, new RelayCommand(() => CopyLink(url))));
                flyout.Items.Add(Item("从 ModDrop 更新", "ModDrop-White32", new RelayCommand(() => OpenUrl(url))));
            }
            if (info.ModDropIds.Count > 0) flyout.Items.Add(new Separator());
            flyout.Items.Add(Item("自由输入 ModDrop ID", "ModDrop-White32", new AsyncRelayCommand(async () =>
            {
                var id = await AppServices.Dialogs.InputAsync("打开模组页面", "输入你要访问的 ModDrop 模组 ID");
                if (!string.IsNullOrWhiteSpace(id)) OpenUrl($"https://www.moddrop.com/stardew-valley/mods/{id.Trim()}");
            })));

            if (info.GitHubRepos.Count > 0) flyout.Items.Add(new Separator());
            foreach (var repo in info.GitHubRepos)
            {
                hasAny = true;
                var url = $"https://github.com/{repo}";
                flyout.Items.Add(Item(repo, "Github", new RelayCommand(() => OpenUrl(url))));
                flyout.Items.Add(Item("复制链接", null, new RelayCommand(() => CopyLink(url))));
                flyout.Items.Add(Item("从 GitHub 更新", "Github",
                    new AsyncRelayCommand(() => vm.RunGitHubUpdateForAsync(repo))));
            }
            if (info.GitHubRepos.Count > 0) flyout.Items.Add(new Separator());
            flyout.Items.Add(Item("自由输入 GitHub 仓库名", "Github", new AsyncRelayCommand(async () =>
            {
                var repo = await AppServices.Dialogs.InputAsync("打开模组页面", "输入你要访问的 GitHub 仓库名，格式：用户名/仓库名");
                if (!string.IsNullOrWhiteSpace(repo)) await vm.RunGitHubUpdateForAsync(repo.Trim());
            })));

            if (!hasAny)
                flyout.Items.Insert(0, Item("没有可用的选项", "模块", new RelayCommand(() => { })));

            BtnUpdateKeys.Flyout = flyout;
            flyout.ShowAt(BtnUpdateKeys);
        };
        BtnDeps.Click += (_, _) =>
        {
            if (DataContext is not ModsPageViewModel vm) return;
            if (vm.SelectedItem == null) { _ = AppServices.Dialogs.InfoAsync("依赖项表", "请先选中一个模组项。"); return; }
            if (TopLevel.GetTopLevel(this) is Window owner)
                new DependenciesWindow(vm.SelectedItem.Name, vm.SelectedItem.ItemPath).Show(owner);
        };
        BtnUniqueIds.Click += (_, _) =>
        {
            if (DataContext is not ModsPageViewModel vm) return;
            if (vm.DetailUniqueIdsList.Count == 0) { _ = AppServices.Dialogs.InfoAsync("UniqueID 表", vm.DetailUniqueIds); return; }
            var flyout = new MenuFlyout();
            flyout.Items.Add(new MenuItem
            {
                Header = "复制全部",
            // TODO: Avalonia 12 剪贴板 API 重构后恢复
            });
            foreach (var id in vm.DetailUniqueIdsList)
            {
                var copy = id;
                flyout.Items.Add(new MenuItem
                {
                    Header = $"复制：{copy}",
            // TODO: Avalonia 12 剪贴板 API 重构后恢复
                });
            }
            BtnUniqueIds.Flyout = flyout;
            flyout.ShowAt(BtnUniqueIds);
        };
        BtnAuthors.Click += (_, _) =>
        {
            if (DataContext is not ModsPageViewModel vm) return;
            if (vm.DetailAuthorsList.Count == 0) { _ = AppServices.Dialogs.InfoAsync("作者表", vm.DetailAuthors); return; }
            var flyout = new MenuFlyout();
            flyout.Items.Add(new MenuItem
            {
                Header = "复制全部",
            // TODO: Avalonia 12 剪贴板 API 重构后恢复
            });
            foreach (var a in vm.DetailAuthorsList)
            {
                var copy = a;
                flyout.Items.Add(new MenuItem
                {
                    Header = $"复制：{copy}",
            // TODO: Avalonia 12 剪贴板 API 重构后恢复
                });
            }
            BtnAuthors.Flyout = flyout;
            flyout.ShowAt(BtnAuthors);
        };

        // 全库搜索窗体（菜单栏「搜索」按钮）
        // 搜索窗体定位：选中指定名称的项
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ModsPageViewModel vmSel)
            {
                vmSel.RequestSelectItemByName += name =>
                {
                    ModItemVm? target = null;
                    foreach (var candidate in vmSel.Items)
                        if (candidate.Name == name) { target = candidate; break; }
                    if (target != null)
                    {
                        ItemsList.SelectedItems.Clear();
                        ItemsList.SelectedItem = target;
                        ItemsList.ScrollIntoView(target);
                    }
                };
                // 全选/反选/按状态选中：VM → 列表选中回写
                vmSel.RequestSelectItems += items =>
                {
                    ItemsList.SelectedItems.Clear();
                    foreach (var i in items) ItemsList.SelectedItems.Add(i);
                };
            }
        };

        // 快捷键：F5 安装 F6 卸载 F8 配置 F2 重命名 N/G 更新
        KeyDown += async (_, e) =>
        {
            if (DataContext is not ModsPageViewModel vm) return;
            switch (e.Key)
            {
                case Key.F5: await vm.InstallSelectedCommand.ExecuteAsync(null); break;
                case Key.F6: await vm.UninstallSelectedCommand.ExecuteAsync(null); break;
                case Key.F8: vm.AddToQueueCommand.Execute(null); break;
                case Key.F2:
                    if (ItemsList.IsFocused) await vm.RenameItemCommand.ExecuteAsync(null);
                    else if (CategoryList.IsFocused) await vm.RenameCategoryCommand.ExecuteAsync(null);
                    break;
                case Key.N: await vm.NexusUpdateCommand.ExecuteAsync(null); break;
                case Key.G: await vm.GitHubUpdateCommand.ExecuteAsync(null); break;
            }
        };
    }

    private static void OpenUrl(string url)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    // ------------------------------------------------- 菜单构建 helper（底栏更新键菜单 / 描述链接菜单共用）

    private static Avalonia.Media.Imaging.Bitmap? Bmp(string name)
    {
        try
        {
            using var s = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://SMUI/Assets/Menu/{name}.png"));
            return new Avalonia.Media.Imaging.Bitmap(s);
        }
        catch { return null; }
    }

    private static Avalonia.Controls.MenuItem MakeItem(string header, string? icon, System.Windows.Input.ICommand cmd, object? param = null)
        => new()
        {
            Header = header,
            Command = cmd,
            CommandParameter = param,
            Icon = icon == null ? null : new Avalonia.Controls.Image { Source = Bmp(icon), Width = 16, Height = 16 },
        };

    /// <summary>描述区链接点击 → WinForms 同款菜单（打开链接/复制链接/从 NEXUS/ModDrop/GitHub 更新）。</summary>
    private void DetailLink_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ModsPageViewModel vm) return;
        if (sender is not Avalonia.Controls.Button { DataContext: string url }) return;

        void CopyLink(string u)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip)
            {
                var transfer = new Avalonia.Input.DataTransfer();
                transfer.Add(Avalonia.Input.DataTransferItem.CreateText(u));
                _ = clip.SetDataAsync(transfer);
            }
        }

        var flyout = new MenuFlyout();
        flyout.Items.Add(MakeItem("打开链接", "上传云", new RelayCommand(() => OpenUrl(url))));
        flyout.Items.Add(MakeItem("复制链接", null, new RelayCommand(() => CopyLink(url))));
        flyout.Items.Add(new Separator());

        string? nexusId = null, modDropId = null, repo = null;
        if (System.Text.RegularExpressions.Regex.Match(url, @"nexusmods\.com/stardewvalley/mods/(\d+)") is { Success: true } m1)
            nexusId = m1.Groups[1].Value;
        if (System.Text.RegularExpressions.Regex.Match(url, @"moddrop\.com/stardew-valley/mods/(\d+)") is { Success: true } m2)
            modDropId = m2.Groups[1].Value;
        if (System.Text.RegularExpressions.Regex.Match(url, @"github\.com/([\w.-]+/[\w.-]+)") is { Success: true } m3)
            repo = m3.Groups[1].Value;

        flyout.Items.Add(MakeItem("从 NEXUS 更新", "NEXUS", new AsyncRelayCommand(async () =>
        {
            if (nexusId == null) { await AppServices.Dialogs.InfoAsync("从 NEXUS 更新", "该链接不是 NEXUS 模组页面。"); return; }
            await vm.RunNexusUpdateForAsync(nexusId);
        })));
        flyout.Items.Add(MakeItem("从 ModDrop 更新", "ModDrop-White32", new RelayCommand(() =>
        {
            if (modDropId == null) { _ = AppServices.Dialogs.InfoAsync("从 ModDrop 更新", "该链接不是 ModDrop 模组页面。"); return; }
            OpenUrl($"https://www.moddrop.com/stardew-valley/mods/{modDropId}");
        })));
        flyout.Items.Add(MakeItem("从 GitHub 更新", "Github", new AsyncRelayCommand(async () =>
        {
            if (repo == null) { await AppServices.Dialogs.InfoAsync("从 GitHub 更新", "该链接不是 GitHub 仓库页面。"); return; }
            await vm.RunGitHubUpdateForAsync(repo);
        })));

        if (sender is Avalonia.Controls.Button btn)
            flyout.ShowAt(btn);
    }

    /// <summary>菜单栏「搜索」：打开全库搜索窗体。</summary>
    private void Search_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ModsPageViewModel vm) return;
        var w = new SearchWindow(vm, AppServices.Settings,
            vm.SelectedSubLibrary ?? "", vm.SelectedCategory?.Name ?? "", vm.SearchText);
        w.LocateRequested += (sub, cat, name) => _ = vm.LocateItemAsync(sub, cat, name);
        if (TopLevel.GetTopLevel(this) is Window owner)
            w.Show(owner);
    }

    /// <summary>菜单栏「描述菜单 TYPE」：切换描述显示类型（纯文本/预览图优先）。</summary>
    private void ToggleDescType_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ModsPageViewModel vm) return;
        if (PreviewBox != null) PreviewBox.IsVisible = !PreviewBox.IsVisible;
    }

    /// <summary>底部「IMG」：切换下一张预览图。</summary>
    private void Img_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ModsPageViewModel vm) vm.PreviewNextCommand.Execute(null);
    }

    private async Task ShowInfoAsync(string title, string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            await AppServices.Dialogs.InfoAsync(title, "请先选中一个模组项。");
            return;
        }
        await AppServices.Dialogs.InfoAsync(title, content);
    }

    /// <summary>右键命中行：若该行未被选中，则改为仅选中该行。</summary>
    private void SelectRowUnderMouse(ListBox list, PointerPressedEventArgs e)
    {
        if (e.Source is not Avalonia.Visual source) return;
        var item = source.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        if (item?.DataContext == null) return;
        if (list.Selection.SelectedItems.Contains(item.DataContext)) return;
        list.Selection.Clear();
        list.SelectedItem = item.DataContext;
    }
}
