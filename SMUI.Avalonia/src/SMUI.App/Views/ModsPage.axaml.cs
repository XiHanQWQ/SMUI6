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
            if (info.NexusIds.Count == 0 && info.ModDropIds.Count == 0 && info.GitHubRepos.Count == 0)
            { _ = AppServices.Dialogs.InfoAsync("更新键", vm.DetailUpdateKeys); return; }

            var flyout = new MenuFlyout();
            foreach (var id in info.NexusIds)
            {
                var nid = id;
                flyout.Items.Add(new MenuItem
                {
                    Header = $"打开 NEXUS 页面：{nid}",
                    Command = new RelayCommand(() => OpenUrl($"https://www.nexusmods.com/stardewvalley/mods/{nid}")),
                });
            }
            if (info.NexusIds.Count > 0)
                flyout.Items.Add(new MenuItem { Header = "从 NEXUS 更新选中项", Command = vm.NexusUpdateCommand });
            if (info.ModDropIds.Count > 0) flyout.Items.Add(new Separator());
            foreach (var id in info.ModDropIds)
            {
                var mid = id;
                flyout.Items.Add(new MenuItem
                {
                    Header = $"打开 ModDrop 页面：{mid}",
                    Command = new RelayCommand(() => OpenUrl($"https://www.moddrop.com/stardew-valley/mods/{mid}")),
                });
            }
            if (info.GitHubRepos.Count > 0) flyout.Items.Add(new Separator());
            foreach (var repo in info.GitHubRepos)
            {
                var r = repo;
                flyout.Items.Add(new MenuItem
                {
                    Header = $"打开 GitHub 仓库：{r}",
                    Command = new RelayCommand(() => OpenUrl($"https://github.com/{r}")),
                });
            }
            if (info.GitHubRepos.Count > 0)
                flyout.Items.Add(new MenuItem { Header = "从 GitHub 更新选中项", Command = vm.GitHubUpdateCommand });
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

        // 全库搜索窗体
        BtnFullSearch.Click += (_, _) =>
        {
            if (DataContext is not ModsPageViewModel vm) return;
            var w = new SearchWindow(vm, AppServices.Settings,
                vm.SelectedSubLibrary ?? "", vm.SelectedCategory?.Name ?? "", vm.SearchText);
            w.LocateRequested += (sub, cat, name) => _ = vm.LocateItemAsync(sub, cat, name);
            if (TopLevel.GetTopLevel(this) is Window owner)
                w.Show(owner);
        };
        // 搜索窗体定位：选中指定名称的项
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
        }

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
