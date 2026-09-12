using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;
using SMUI.Core.Models;
using SMUI.Core.Services;

namespace SMUI.App.ViewModels;

/// <summary>ModsPageViewModel 的新增功能扩展：批量创建、游戏 Mods 导入、全库筛选、导出预设。</summary>
public partial class ModsPageViewModel
{
    // ------------------------------------------------- 批量创建项

    [RelayCommand]
    private async Task BatchCreateItemsAsync()
    {
        if (SelectedSubLibrary == null || SelectedCategory == null) return;
        var text = await _dialogs.MultilineInputAsync("批量创建模组项",
            "每行一个模组项名称（将创建同名文件夹）：", "");
        if (string.IsNullOrWhiteSpace(text)) return;

        var names = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Distinct().ToList();
        if (names.Count == 0) return;
        if (!await _dialogs.ConfirmAsync("批量创建", $"将在分类 {SelectedCategory.Name} 下创建 {names.Count} 个模组项，是否继续？"))
            return;

        var created = 0;
        var errors = new List<string>();
        foreach (var name in names)
        {
            try
            {
                AppServices.ItemOps.CreateItem(SelectedSubLibrary, SelectedCategory.Name, name);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"{name}: {ex.Message}");
            }
        }
        _log.Print($"批量创建完成：成功 {created} 个，失败 {errors.Count} 个", LogKind.Success);
        if (errors.Count > 0)
            await _dialogs.InfoAsync("部分失败", string.Join("\n", errors));
        await RefreshItemsAsync();
    }

    // ------------------------------------------------- 全局模组安装检查：从游戏 Mods 导入

    [RelayCommand]
    private async Task ImportFromGameModsAsync()
    {
        if (SelectedSubLibrary == null) { await _dialogs.InfoAsync("提示", "请先选择数据子库。"); return; }
        if (string.IsNullOrEmpty(_settings.GamePath) || !Directory.Exists(_settings.GamePath))
        {
            await _dialogs.InfoAsync("提示", "请先在「设置」配置游戏路径。");
            return;
        }

        var mods = await Task.Run(() => GameModsImporter.ScanGameMods(_settings.GamePath));
        if (mods.Count == 0)
        {
            await _dialogs.InfoAsync("全局模组安装检查", $"游戏 Mods 目录中没有找到任何带 manifest.json 的模组。");
            return;
        }

        var options = mods.Select(m => $"{m.FolderName}  ({m.ModName} {(string.IsNullOrEmpty(m.Version) ? "" : "v" + m.Version)})").ToList();
        var picked = await _dialogs.MultiSelectAsync("全局模组安装检查",
            $"在游戏 Mods 中发现 {mods.Count} 个模组，选择要导入数据库为模组项的（将自动生成安装规划）：", options);
        if (picked == null || picked.Count == 0) return;

        var pickedSet = picked.Select(p => p.Split("  (")[0]).ToHashSet();
        var targets = mods.Where(m => pickedSet.Contains(m.FolderName)).ToList();

        // 选择目标分类
        var cats = AppServices.Library.ScanCategories(SelectedSubLibrary);
        string catName;
        if (cats.Count > 0)
        {
            var catPick = await _dialogs.ChoiceAsync("选择目标分类",
                $"导入的 {targets.Count} 个模组项放入哪个分类：", cats.Select(c => c.Key).ToList());
            if (catPick < 0) return;
            catName = cats[catPick].Key;
        }
        else
        {
            catName = "Default";
            AppServices.ItemOps.CreateCategory(SelectedSubLibrary, catName);
        }

        IsBusy = true;
        BusyText = "正在导入...";
        var ok = 0;
        var errors = new List<string>();
        try
        {
            await Task.Run(() =>
            {
                foreach (var mod in targets)
                {
                    try
                    {
                        GameModsImporter.ImportAsItem(mod, SelectedSubLibrary!, catName, _settings.RepositoryPath);
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{mod.FolderName}: {ex.Message}");
                    }
                }
            });
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }

        _log.Print($"已从游戏 Mods 导入 {ok} 个模组项到分类 {catName}", LogKind.Success);
        var msg = $"成功导入 {ok} 个模组项（已自动生成 CD-D-MODS 安装规划）。";
        if (errors.Count > 0) msg += "\n\n失败：\n" + string.Join("\n", errors);
        await _dialogs.InfoAsync("导入完成", msg);
        await LoadCategoriesAsync();
    }

    // ------------------------------------------------- 导出预设（批量分发）

    [RelayCommand]
    private async Task AddToPresetAsync()
    {
        if (SelectedItems.Count == 0) { await _dialogs.InfoAsync("提示", "请先选中模组项。"); return; }
        var added = AppServices.Presets.Add(SelectedItems.Select(x => x.ItemPath));
        await _dialogs.InfoAsync("导出预设", $"已加入 {added} 个模组项，预设中共 {AppServices.Presets.Count} 个。");
    }

    [RelayCommand]
    private async Task ExportPresetAsync()
    {
        if (AppServices.Presets.Count == 0)
        {
            await _dialogs.InfoAsync("批量分发", "导出预设为空，请先在模组项菜单中「加入导出预设」。");
            return;
        }
        var dir = await _dialogs.PickFolderAsync($"选择导出目录（将导出 {AppServices.Presets.Count} 个 .smuimpak 分发包）");
        if (dir == null) return;
        IsBusy = true;
        BusyText = "正在批量导出...";
        try
        {
            var (ok, fail, output) = await Task.Run(() => AppServices.Presets.ExportAll(dir));
            _log.Print($"批量分发完成：成功 {ok} 个，失败 {fail} 个 → {output}", LogKind.Success);
            await _dialogs.InfoAsync("批量分发完成", $"成功导出 {ok} 个分发包到：\n{output}" +
                (fail > 0 ? $"\n\n失败 {fail} 个（源目录缺失）。" : ""));
        }
        finally { IsBusy = false; BusyText = ""; }
    }

    [RelayCommand]
    private async Task ClearPresetAsync()
    {
        if (AppServices.Presets.Count == 0) { await _dialogs.InfoAsync("提示", "导出预设已为空。"); return; }
        if (!await _dialogs.ConfirmAsync("清空导出预设", $"是否清空预设中的 {AppServices.Presets.Count} 个模组项？（不会删除任何数据）"))
            return;
        AppServices.Presets.Clear();
        await _dialogs.InfoAsync("已清空", "导出预设已清空。");
    }

    // ------------------------------------------------- 全库筛选（跨分类扫描）

    /// <summary>扫描整个子库所有分类，聚合显示已安装/未安装的项。</summary>
    private async Task LoadAllLibraryItemsAsync(bool installed)
    {
        if (SelectedSubLibrary == null) return;
        FilteredItems.Clear();
        Items.Clear();
        IsBusy = true;
        BusyText = "正在扫描整个子库...";
        try
        {
            var sub = SelectedSubLibrary!;
            var cats = AppServices.Library.ScanCategories(sub);
            await Task.Run(() =>
            {
                foreach (var (cat, _) in cats)
                {
                    foreach (var entry in AppServices.Library.ScanItems(sub, cat))
                    {
                        var isInstalled = entry.Status is
                            InstallStatus.Installed or InstallStatus.FileInstalled or InstallStatus.FileInstalledVerified
                            or InstallStatus.FolderCopied or InstallStatus.Additional or InstallStatus.CoverContent
                            or InstallStatus.ExistedFolder;
                        if (isInstalled != installed) continue;
                        var vm = ModItemVm.From(entry);
                        vm.SubText = $"[{cat}]" + (vm.GroupsText.Length > 0 ? " " + vm.GroupsText : "");
                        Items.Add(vm);
                    }
                }
            });
            foreach (var item in Items) FilteredItems.Add(item);
            ItemCountText = $"{Items.Count}";
            _log.Print($"全库筛选完成：找到 {Items.Count} 个{(installed ? "已安装" : "未安装")}模组项", LogKind.Info);
        }
        finally { IsBusy = false; BusyText = ""; }
    }
}
