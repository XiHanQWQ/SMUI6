using SMUI.App.ViewModels;
using SMUI.Core.Services;

namespace SMUI.App.Services;

/// <summary>
/// 规划编辑服务：为每个规划 Key 弹出参数编辑对话框（对应原版 Form编辑规划_* 系列窗体）。
/// 返回编辑后的参数值（竖线分隔），取消返回 null。
/// </summary>
public static class PlanEditorService
{
    public static async Task<string?> EditAsync(IDialogService dialogs, string key, string value, string itemPath)
    {
        var p = value.Split('|');
        var contents = string.IsNullOrEmpty(itemPath)
            ? new List<(string Name, string Kind, string ColorKey)>()
            : AppServices.ItemOps.ScanItemContents(itemPath).ToList();
        var folders = contents.Where(c => c.Kind == "文件夹").Select(c => c.Name).ToList();
        var files = contents.Where(c => c.Kind == "文件").Select(c => c.Name).ToList();

        switch (key)
        {
            case "CD-D-MODS":
            case "CD-D-MODS-COVER":
            {
                var current = p.Length > 0 ? p[0] : "";
                var pick = await PickFromList(dialogs, key == "CD-D-MODS" ? "安装标准 SMAPI 模组" : "覆盖 Mods 中的文件夹",
                    "选择项内容中包含 manifest.json 的文件夹：", folders, current);
                return pick;
            }

            case "CD-D-ROOT":
            {
                var src = p.Length > 0 ? p[0] : "";
                var dest = p.Length > 1 ? p[1] : "";
                var pickSrc = await PickFromList(dialogs, "复制文件夹（步骤 1/2）", "选择项内的源文件夹：", folders, src);
                if (pickSrc == null) return null;
                var newDest = await dialogs.InputAsync("复制文件夹（步骤 2/2）", "输入游戏目录内的目标位置（相对游戏根目录，如 Content\\Modded）：", dest);
                if (newDest == null) return null;
                return $"{pickSrc}|{newDest}";
            }

            case "CD-D-CONTENT":
                await dialogs.InfoAsync("覆盖 Content 文件夹", "此规划没有参数，安装时会把项内的 Content 文件夹整个覆盖到游戏目录。");
                return "0";

            case "CD-F":
            {
                // <是否需要判断>|<是否需要验证>|<源文件>|<目标位置>
                var needCheck = p.Length > 0 && p[0].Equals("true", StringComparison.CurrentCultureIgnoreCase);
                var needVerify = p.Length > 1 && p[1].Equals("true", StringComparison.CurrentCultureIgnoreCase);
                var src = p.Length > 3 ? p[3] : (p.Length > 0 ? p[0] : "");
                var dest = p.Length > 4 ? p[4] : "";

                var check = await dialogs.ChoiceAsync("安装单个文件（步骤 1/4）",
                    "是否需要安装判断（未安装时才复制，安装状态下跳过）：",
                    new[] { "False 不需要判断（每次都复制）", "True 需要判断" });
                if (check < 0) return null;
                var verify = await dialogs.ChoiceAsync("安装单个文件（步骤 2/4）",
                    "是否需要 SHA256 验证（用于精确判断文件是否已安装，卸载时会自动还原备份）：",
                    new[] { "False 不需要验证", "True 需要验证" });
                if (verify < 0) return null;
                var pickSrc = await PickFromList(dialogs, "安装单个文件（步骤 3/4）", "选择项内的源文件：", files, src);
                if (pickSrc == null) return null;
                var newDest = await dialogs.InputAsync("安装单个文件（步骤 4/4）", "输入游戏目录内的目标位置（相对游戏根目录，如 Content\\Data\\File.xnb）：", dest);
                if (newDest == null) return null;
                return $"{(check == 1 ? "True" : "False")}|{(verify == 1 ? "True" : "False")}|False|{pickSrc}|{newDest}";
            }

            case "CD-D-Advanced":
            {
                // <卸载时如何操作>|<如何还原>|<源文件夹>|<目标位置>
                var mode = await dialogs.ChoiceAsync("文件夹高级安装（步骤 1/4）",
                    "卸载时如何操作：", new[] { "ReStore 卸载时从备份还原", "Delete 卸载时直接删除" });
                if (mode < 0) return null;
                var how = await dialogs.ChoiceAsync("文件夹高级安装（步骤 2/4）",
                    "如何还原（仅 ReStore 模式生效）：", new[] { "Delete-Copy 先删除再从备份复制", "Cover 直接用备份覆盖" });
                if (how < 0) return null;
                var pickSrc = await PickFromList(dialogs, "文件夹高级安装（步骤 3/4）", "选择项内要安装的文件夹：", folders, p.Length > 2 ? p[2] : "");
                if (pickSrc == null) return null;
                var dest = await dialogs.InputAsync("文件夹高级安装（步骤 4/4）", "输入游戏目录内的目标位置（相对游戏根目录）：", p.Length > 3 ? p[3] : "");
                if (dest == null) return null;
                return $"{(mode == 0 ? "ReStore" : "Delete")}|{(how == 0 ? "Delete-Copy" : "Cover")}|{pickSrc}|{dest}";
            }

            case "CR-Check-EXIST":
            {
                var phase = await dialogs.ChoiceAsync("检查存在性（步骤 1/3）",
                    "在哪个阶段生效：", new[] { "Install 安装阶段", "UnInstall 卸载阶段" });
                if (phase < 0) return null;
                var kind = await dialogs.ChoiceAsync("检查存在性（步骤 2/3）",
                    "检查对象类型：", new[] { "Folder 文件夹", "File 文件" });
                if (kind < 0) return null;
                var expected = await dialogs.ChoiceAsync("检查存在性（步骤 3/3）",
                    "期望的存在状态：", new[] { "True 应当存在", "False 应当不存在" });
                if (expected < 0) return null;

                var paths = new List<string>();
                while (true)
                {
                    var path = await dialogs.InputAsync($"检查存在性（已填 {paths.Count} 项）",
                        $"输入要检查的{(kind == 0 ? "文件夹" : "文件")}路径（相对游戏根目录，留空结束）：",
                        "");
                    if (string.IsNullOrWhiteSpace(path)) break;
                    paths.Add(path);
                }
                if (paths.Count == 0) return null;
                var phaseText = phase == 0 ? "Install" : "UnInstall";
                return $"{phaseText}|{(kind == 0 ? "Folder" : "File")}|{(expected == 0 ? "True" : "False")}|{string.Join("|", paths)}";
            }

            case "CR-IN-MODS-VER":
            {
                var modDir = await dialogs.InputAsync("检查已安装模组版本（步骤 1/3）", "输入 Mods 内的模组文件夹名：", p.Length > 0 ? p[0] : "");
                if (string.IsNullOrWhiteSpace(modDir)) return null;
                var op = await dialogs.ChoiceAsync("检查已安装模组版本（步骤 2/3）", "选择版本比较方式：",
                    new[] { "< 小于", "= 等于", "> 大于", "<= 小于等于", ">= 大于等于", "<> 不等于" });
                if (op < 0) return null;
                var opText = new[] { "<", "=", ">", "<=", ">=", "<>" }[op];
                var version = await dialogs.InputAsync("检查已安装模组版本（步骤 3/3）", "输入要比较的版本号：", p.Length > 2 ? p[2] : "");
                if (string.IsNullOrWhiteSpace(version)) return null;
                return $"{modDir}|{opText}|{version}";
            }

            case "CR-UN":
            {
                var mode = await dialogs.ChoiceAsync("卸载时取消操作", "选择卸载此模组项时的行为：",
                    new[] { "ERROR 禁止卸载（报错中止）", "CANCEL 静默取消卸载" });
                if (mode < 0) return null;
                return mode == 0 ? "ERROR" : "CANCEL";
            }

            case "CR-SHELL":
            {
                var phase = await dialogs.ChoiceAsync("运行可执行文件（步骤 1/4）",
                    "在哪个阶段运行：", new[] { "Install 安装阶段", "UnInstall 卸载阶段" });
                if (phase < 0) return null;
                var pickSrc = await PickFromList(dialogs, "运行可执行文件（步骤 2/4）", "选择项内的可执行文件：", files, p.Length > 1 ? p[1] : "");
                if (pickSrc == null) return null;
                var args = await dialogs.InputAsync("运行可执行文件（步骤 3/4）", "输入启动参数（可留空）：", p.Length > 2 ? p[2] : "");
                if (args == null) return null;
                var wait = await dialogs.ChoiceAsync("运行可执行文件（步骤 4/4）", "是否等待程序结束再继续：",
                    new[] { "False 不等待", "True 等待" });
                if (wait < 0) return null;
                var phaseText = phase == 0 ? "Install" : "UnInstall";
                return $"{phaseText}|{pickSrc}|{args}|{(wait == 1 ? "True" : "False")}";
            }

            case "CR-MSGBOX":
            {
                var phase = await dialogs.ChoiceAsync("弹窗（步骤 1/5）", "在哪个阶段弹窗：",
                    new[] { "Install 安装阶段", "UnInstall 卸载阶段" });
                if (phase < 0) return null;
                var title = await dialogs.InputAsync("弹窗（步骤 2/5）", "输入弹窗标题：", p.Length > 1 ? p[1] : "");
                if (string.IsNullOrWhiteSpace(title)) return null;
                var message = await dialogs.InputAsync("弹窗（步骤 3/5）", "输入弹窗内容（用 <br> 表示换行）：", p.Length > 2 ? p[2].Replace("<br>", "\n") : "");
                if (message == null) return null;
                var mustMatch = await dialogs.ChoiceAsync("弹窗（步骤 4/5）", "是否必须选择正确的选项才能继续：",
                    new[] { "False 不强制", "True 强制（选错中止）" });
                if (mustMatch < 0) return null;
                var options = new List<string>();
                while (true)
                {
                    var opt = await dialogs.InputAsync($"弹窗（已填 {options.Count} 个选项）", "输入选项文字（留空结束）：", "");
                    if (string.IsNullOrWhiteSpace(opt)) break;
                    options.Add(opt);
                }
                if (options.Count == 0) return null;
                var correct = 1;
                if (mustMatch == 1)
                {
                    var pick = await dialogs.ChoiceAsync("弹窗（步骤 5/5）", "选择哪一项是正确选项：", options);
                    if (pick < 0) return null;
                    correct = pick + 1;
                }
                var phaseText = phase == 0 ? "Install" : "UnInstall";
                return $"{phaseText}|{title}|{message.Trim().Replace("\n", "<br>")}|{(mustMatch == 1 ? "True" : "False")}|{correct}|{string.Join("|", options)}";
            }

            case "CORE-CLASS":
            {
                var options = new List<string> { "CG-DB 关闭 config 自动保留", "Mods-AMD", "FILE-ALLOW-ALL 允许复制所有文件" };
                var pick = await dialogs.MultiSelectAsync("声明核心功能启停", "选择要启用的核心功能：", options);
                if (pick == null || pick.Count == 0) return null;
                var flags = pick.Select(x => x.Split(' ')[0]).ToList();
                return string.Join("|", flags);
            }

            default:
                await dialogs.InfoAsync("提示", $"规划类型 {key} 没有内置编辑器。");
                return null;
        }
    }

    /// <summary>从列表中选择一个值；若当前值不在列表中则允许手动输入。</summary>
    private static async Task<string?> PickFromList(IDialogService dialogs, string title, string message,
        IReadOnlyList<string> candidates, string current)
    {
        var options = new List<string>(candidates);
        if (options.Count == 0)
            return await dialogs.InputAsync(title, message, current);
        if (!string.IsNullOrEmpty(current) && !options.Contains(current))
            options.Insert(0, current);
        var pick = await dialogs.ChoiceAsync(title, message, options);
        return pick < 0 ? null : options[pick];
    }
}
