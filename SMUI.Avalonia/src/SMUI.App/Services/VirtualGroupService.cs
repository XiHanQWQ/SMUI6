using SMUI.App.Services;
using SMUI.App.ViewModels;
using SMUI.Core.Services;

namespace SMUI.App.Services;

/// <summary>虚拟组编辑结果。</summary>
public record VirtualGroupEditResult(string Mode, IReadOnlyList<string> Groups);

/// <summary>虚拟组编辑流程（对应原版 Form编辑虚拟组）。</summary>
public static class VirtualGroupService
{
    public static async Task<VirtualGroupEditResult?> EditAsync(IDialogService dialogs, string subLibrary, List<ModItemVm> items)
    {
        // 已有的虚拟组全集
        var allGroups = AppServices.ItemOps.CollectVirtualGroupIndex(subLibrary).Keys.OrderBy(k => k).ToList();
        var currentGroups = items.SelectMany(i => i.VirtualGroups).Distinct().ToList();

        var mode = await dialogs.ChoiceAsync("设置虚拟组",
            $"为选中的 {items.Count} 个模组项设置虚拟组：", new[]
            {
                "添加虚拟组（保留已有，追加新的）",
                "替换虚拟组（清空后写入）",
                "移除虚拟组",
            });
        if (mode < 0) return null;

        var existing = await dialogs.MultiSelectAsync("选择虚拟组",
            "从已有的虚拟组中选择（可多选，确定后可继续新建）：", allGroups);
        var selected = new List<string>();
        if (existing != null) selected.AddRange(existing);

        while (true)
        {
            var add = await dialogs.InputAsync("新建虚拟组", $"输入新的虚拟组名称（当前已选 {selected.Count} 个，留空结束）：", "");
            if (string.IsNullOrWhiteSpace(add)) break;
            if (!selected.Contains(add.Trim())) selected.Add(add.Trim());
        }

        if (mode == 2 && selected.Count == 0) return null;
        return new VirtualGroupEditResult(mode switch
        {
            0 => "add",
            1 => "replace",
            _ => "remove",
        }, selected);
    }
}
