using SMUI.Core.Engine;
using SMUI.Core.Models;

namespace SMUI.Core.Engine;

public enum BatchOperation
{
    Install,
    Uninstall,
    UpdateFromModsOverwrite,
    UpdateFromModsReplace,
}

/// <summary>
/// 批量安装调度器（对应原版 安装卸载 类的后台线程逻辑）。
/// 安装失败自动反向执行卸载回滚；卸载倒序执行且尊重 CR-UN 取消。
/// </summary>
public class InstallRunner
{
    /// <summary>kind: 1=普通 2=信息 3=错误。itemIndex=-1 表示全局消息。</summary>
    public Action<int, int, string>? Report { get; set; }

    /// <summary>单个项处理完成（成功或失败后）回调，参数为项索引。</summary>
    public Action<int>? ItemFinished { get; set; }

    public Func<ChoiceRequest, Task<int>>? ShowChoice { get; set; }

    public string GamePath { get; set; } = "";
    public string GameBackupPath { get; set; } = "";

    private void Rep(int kind, int itemIndex, string msg) => Report?.Invoke(kind, itemIndex, msg);

    public async Task RunAsync(IReadOnlyList<string> itemPaths, BatchOperation operation)
    {
        await Task.Run(() =>
        {
            for (var i = 0; i < itemPaths.Count; i++)
            {
                var itemPath = itemPaths[i];
                var engine = new InstallEngine
                {
                    ItemPath = itemPath,
                    GamePath = GamePath,
                    GameBackupPath = GameBackupPath,
                    ShowChoice = ShowChoice,
                    Report = (kind, msg) => Rep(kind, i, msg),
                };

                Rep(2, i, $"加载规划数据：{Path.GetFileName(itemPath)}");
                var loadError = engine.LoadPlan();
                if (loadError != "")
                {
                    Rep(3, i, $"加载规划数据错误： {loadError}");
                    ItemFinished?.Invoke(i);
                    continue;
                }
                Rep(2, i, $"规划步骤总数：{engine.Steps.Count}");

                try
                {
                    switch (operation)
                    {
                        case BatchOperation.Install:
                            RunInstall(engine, i);
                            break;
                        case BatchOperation.Uninstall:
                            RunUninstall(engine, i);
                            break;
                        case BatchOperation.UpdateFromModsOverwrite:
                            Rep(2, i, "规划数据已载入");
                            UpdateFromMods(engine, itemPath, i, replace: false);
                            break;
                        case BatchOperation.UpdateFromModsReplace:
                            Rep(2, i, "规划数据已载入");
                            UpdateFromMods(engine, itemPath, i, replace: true);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Rep(3, i, ex.Message);
                }

                ItemFinished?.Invoke(i);
            }
        });
    }

    private void RunInstall(InstallEngine engine, int itemIndex)
    {
        for (var s = 0; s < engine.Steps.Count; s++)
        {
            try
            {
                engine.ExecuteInstall(s);
            }
            catch (Exception ex)
            {
                Rep(3, itemIndex, $"{ex.Message}");
                Rep(3, itemIndex, "正在回滚操作");
                for (var r = s; r >= 0; r--)
                {
                    try { engine.ExecuteUninstall(r); }
                    catch { /* 回滚尽力而为 */ }
                }
                return;
            }
        }
    }

    private void RunUninstall(InstallEngine engine, int itemIndex)
    {
        for (var s = engine.Steps.Count - 1; s >= 0; s--)
        {
            if (engine.UninstallCancelled) return;
            try
            {
                engine.ExecuteUninstall(s);
            }
            catch (Exception ex)
            {
                Rep(3, itemIndex, $"{ex.Message}");
                Rep(3, itemIndex, "卸载操作不能通过反向执行来回滚操作，这可能已经导致了预期外的问题");
                return;
            }
        }
    }

    /// <summary>把游戏 Mods 内已安装的文件夹拉回数据库（本地更新）。</summary>
    private void UpdateFromMods(InstallEngine engine, string itemPath, int itemIndex, bool replace)
    {
        foreach (var step in engine.Steps)
        {
            if (step.Name != "CD-D-MODS") continue;
            var folder = step.Args;
            var inGame = Path.Combine(GamePath, "Mods", folder);
            var inRepo = Path.Combine(itemPath, folder);
            if (!Directory.Exists(inGame))
            {
                Rep(1, itemIndex, $"未找到游戏内的 {folder} 文件夹，跳过");
                continue;
            }
            Rep(1, itemIndex, $"已找到游戏内的 {folder} 文件夹");
            if (replace)
            {
                if (!Directory.Exists(inRepo))
                {
                    Rep(1, itemIndex, $"数据库中不存在 {folder} 文件夹，为避免意外，跳过");
                    continue;
                }
                Rep(1, itemIndex, "正在删除数据库内已有内容");
                Directory.Delete(inRepo, true);
                Rep(1, itemIndex, "正在复制到数据库");
                CopyDirectory(inGame, inRepo);
            }
            else
            {
                Rep(1, itemIndex, $"正在覆盖到数据库");
                CopyDirectory(inGame, inRepo);
            }
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
}
