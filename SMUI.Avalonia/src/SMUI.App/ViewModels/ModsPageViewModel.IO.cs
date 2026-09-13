using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;
using SMUI.Core.Services;

namespace SMUI.App.ViewModels;

/// <summary>ModsPageViewModel 的导入导出 / 虚拟组 / 更新扩展命令。</summary>
public partial class ModsPageViewModel
{
    [RelayCommand]
    private async Task ExportSubLibraryAsync()
    {
        if (SelectedSubLibrary == null) return;
        var file = await _dialogs.PickSaveFileAsync("导出数据子库", SelectedSubLibrary, "子库包文件", "*.smuispak");
        if (file == null) return;
        IsBusy = true;
        BusyText = "正在打包数据子库...";
        try
        {
            await System.Threading.Tasks.Task.Run(() => AppServices.Archive.PackDirectory(
                AppServices.Library.SubLibraryPath(SelectedSubLibrary), file));
            _log.Print($"已导出数据子库：{file}", LogKind.Success);
        }
        catch (Exception ex) { await _dialogs.InfoAsync("导出失败", ex.Message); }
        finally { IsBusy = false; BusyText = ""; }
    }

    [RelayCommand]
    private async Task ExportCategoryAsync()
    {
        if (SelectedSubLibrary == null || SelectedCategory == null) return;
        var file = await _dialogs.PickSaveFileAsync("导出分类", SelectedCategory.Name, "分类包文件", "*.smuicpak");
        if (file == null) return;
        IsBusy = true;
        BusyText = "正在打包分类...";
        try
        {
            await System.Threading.Tasks.Task.Run(() => AppServices.Archive.PackDirectory(
                AppServices.Library.CategoryPath(SelectedSubLibrary, SelectedCategory.Name), file));
            _log.Print($"已导出分类：{file}", LogKind.Success);
        }
        catch (Exception ex) { await _dialogs.InfoAsync("导出失败", ex.Message); }
        finally { IsBusy = false; BusyText = ""; }
    }

    [RelayCommand]
    private async Task ExportItemsAsync()
    {
        if (SelectedItems.Count == 0) return;
        var file = await _dialogs.PickSaveFileAsync("导出模组项", SelectedItems[0].Name, "项包文件", "*.smuimpak");
        if (file == null) return;
        IsBusy = true;
        BusyText = "正在打包模组项...";
        try
        {
            await System.Threading.Tasks.Task.Run(() => AppServices.Archive.PackFiles(
                SelectedItems.Select(x => x.ItemPath), file));
            _log.Print($"已导出 {SelectedItems.Count} 个模组项：{file}", LogKind.Success);
        }
        catch (Exception ex) { await _dialogs.InfoAsync("导出失败", ex.Message); }
        finally { IsBusy = false; BusyText = ""; }
    }

    [RelayCommand]
    private async Task ImportSubLibraryAsync()
    {
        if (_settings.RepositoryPath == "") { await _dialogs.InfoAsync("提示", "请先设置模组数据仓库。"); return; }
        await ImportToAsync(_settings.RepositoryPath, "导入数据子库", "*.smuispak");
    }

    [RelayCommand]
    private async Task ImportCategoryAsync()
    {
        if (SelectedSubLibrary == null) { await _dialogs.InfoAsync("提示", "请先选择子库。"); return; }
        await ImportToAsync(AppServices.Library.SubLibraryPath(SelectedSubLibrary), "导入分类", "*.smuicpak");
    }

    [RelayCommand]
    private async Task ImportItemsAsync()
    {
        if (SelectedCategory == null) { await _dialogs.InfoAsync("提示", "请先选择分类。"); return; }
        await ImportToAsync(AppServices.Library.CategoryPath(SelectedSubLibrary!, SelectedCategory.Name), "导入模组项", "*.smuimpak");
    }

    private async Task ImportToAsync(string targetDir, string title, string pattern)
    {
        var files = await _dialogs.PickFilesAsync(title, "SMUI 包文件", pattern);
        if (files == null || files.Length == 0) return;
        IsBusy = true;
        try
        {
            foreach (var file in files)
            {
                BusyText = $"正在解包 {Path.GetFileName(file)}...";
                await System.Threading.Tasks.Task.Run(() => AppServices.Archive.Extract(file, targetDir));
                _log.Print($"已导入 {Path.GetFileName(file)} → {targetDir}", LogKind.Success);
            }
        }
        catch (Exception ex) { await _dialogs.InfoAsync("导入失败", ex.Message); }
        finally { IsBusy = false; BusyText = ""; }
        await LoadCategoriesAsync();
    }

    // ------------------------------------------------- 虚拟组

    [RelayCommand]
    private async Task ManageVirtualGroupsAsync()
    {
        if (SelectedItems.Count == 0)
        {
            await _dialogs.InfoAsync("虚拟组", "请先选中模组项。");
            return;
        }
        var result = await Services.VirtualGroupService.EditAsync(_dialogs, SelectedSubLibrary!, SelectedItems.ToList());
        if (result == null) return;
        switch (result.Mode)
        {
            case "add":
                AppServices.ItemOps.AddVirtualGroups(SelectedItems.Select(x => x.ItemPath).ToList(), result.Groups);
                break;
            case "replace":
                foreach (var item in SelectedItems)
                    AppServices.ItemOps.WriteVirtualGroups(item.ItemPath, result.Groups);
                break;
            case "remove":
                AppServices.ItemOps.RemoveVirtualGroups(SelectedItems.Select(x => x.ItemPath).ToList(), result.Groups);
                break;
        }
        _log.Print("虚拟组已更新", LogKind.Success);
        await RefreshItemsAsync();
    }

    // ------------------------------------------------- 更新模组项

    /// <summary>底栏「更新键」菜单：对指定 NEXUS ID 执行更新（复刻 获取NEXUS文件列表 入口）。</summary>
    public async Task RunNexusUpdateForAsync(string nexusId)
    {
        if (SelectedItems.Count != 1) { await _dialogs.InfoAsync("提示", "请先选中一个模组项。"); return; }
        await _updates.RunNexusUpdateAsync(nexusId, SelectedItems[0].ItemPath);
        await RefreshItemsAsync();
    }

    /// <summary>底栏「更新键」菜单：对指定 GitHub 仓库执行更新。</summary>
    public async Task RunGitHubUpdateForAsync(string repo)
    {
        if (SelectedItems.Count != 1) { await _dialogs.InfoAsync("提示", "请先选中一个模组项。"); return; }
        await _updates.RunGitHubUpdateAsync(repo, SelectedItems[0].ItemPath);
        await RefreshItemsAsync();
    }

    [RelayCommand]
    private async Task NexusUpdateAsync()
    {
        if (SelectedItems.Count != 1 || _currentInfo == null)
        {
            await _dialogs.InfoAsync("提示", "请先选中一个模组项并等待信息加载。");
            return;
        }
        var nexusId = _currentInfo.NexusIds.FirstOrDefault();
        if (nexusId == null)
        {
            await _dialogs.InfoAsync("无法更新", "此模组项没有 NEXUS 更新键。");
            return;
        }
        await _updates.RunNexusUpdateAsync(nexusId, SelectedItems[0].ItemPath);
        await RefreshItemsAsync();
    }

    [RelayCommand]
    private async Task GitHubUpdateAsync()
    {
        if (SelectedItems.Count != 1 || _currentInfo == null)
        {
            await _dialogs.InfoAsync("提示", "请先选中一个模组项并等待信息加载。");
            return;
        }
        var repo = _currentInfo.GitHubRepos.FirstOrDefault();
        if (repo == null)
        {
            await _dialogs.InfoAsync("无法更新", "此模组项没有 GitHub 更新键。");
            return;
        }
        await _updates.RunGitHubUpdateAsync(repo, SelectedItems[0].ItemPath);
        await RefreshItemsAsync();
    }

    [RelayCommand]
    private void DownloadAndCreateItem()
    {
        _ = _updates.DownloadAndCreateItemCommand.ExecuteAsync(null);
    }
}
