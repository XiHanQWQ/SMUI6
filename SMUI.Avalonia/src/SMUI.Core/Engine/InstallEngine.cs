namespace SMUI.Core.Engine;

/// <summary>安装引擎执行失败时抛出（对应原版 Err.Raise），用于触发回滚。</summary>
public class SmuiEngineException : Exception
{
    public SmuiEngineException(string message) : base(message) { }
}

/// <summary>规划步骤（对应原版 任务列表结构）。</summary>
public record InstallStep(string Name, string Args);

/// <summary>CR-MSGBOX 弹窗请求。</summary>
public record ChoiceRequest(string Title, string Message, IReadOnlyList<string> Options);

/// <summary>
/// 安装/卸载引擎（对应原版 任务队列 + CD1/CD2/CD3 + 文件夹高级安装 CD-D-Advanced）。
/// 用法：设置上下文 → LoadPlan() 解析 Code2 生成步骤表 → 逐条 ExecuteInstall / ExecuteUninstall。
/// </summary>
public class InstallEngine
{
    public string ItemPath { get; set; } = "";
    public string GamePath { get; set; } = "";
    public string GameBackupPath { get; set; } = "";

    /// <summary>进度输出：kind 1=普通(白) 2=信息(蓝) 3=错误(红)。</summary>
    public Action<int, string>? Report { get; set; }

    /// <summary>CR-MSGBOX 弹窗回调（需要在 UI 线程实现并返回选择的索引，取消返回 -1）。</summary>
    public Func<ChoiceRequest, Task<int>>? ShowChoice { get; set; }

    /// <summary>CORE-CLASS 含 CG-DB 时为 true（关闭 config.json 自动保留机制）。</summary>
    public bool DisableConfigPreservation { get; private set; }

    /// <summary>CR-UN=CANCEL 后置位（卸载流程应中止）。</summary>
    public bool UninstallCancelled { get; private set; }

    public List<InstallStep> Steps { get; } = new();
    public List<KeyValuePair<string, string>> RawPlan { get; } = new();

    /// <summary>内置步骤码（插件注册的自定义码不在此列时，才会被接入引擎）。</summary>
    public static readonly string[] BuiltInStepCodes =
    {
        "CD-D-MODS", "CD-D-MODS-COVER", "CD-D-ROOT", "CD-D-CONTENT", "CD-F",
        "CR-Check-EXIST", "CR-IN-MODS-VER", "CR-UN", "CR-SHELL", "CR-MSGBOX", "CD-D-Advanced",
    };

    /// <summary>插件注册的自定义规划处理器（对应原版 任务队列 的三个匹配字典）。</summary>
    public Dictionary<string, Action> RecognizeHandlers { get; } = new();
    public Dictionary<string, Action> InstallHandlers { get; } = new();
    public Dictionary<string, Action> UninstallHandlers { get; } = new();

    /// <summary>识别/执行期间插件处理器读取的当前索引（对应原版 当前正在处理的索引）。</summary>
    public int CurrentPlanIndex { get; private set; }

    /// <summary>供识别处理器向步骤表追加步骤（与内置步骤同等参与安装/卸载）。</summary>
    public void AddStep(string name, string args) => Steps.Add(new InstallStep(name, args));

    private void Report1(string msg) => Report?.Invoke(1, msg);
    private void Report2(string msg) => Report?.Invoke(2, msg);

    /// <summary>解析项目录下的 Code2 文件，生成步骤表。返回错误信息，成功为空字符串。</summary>
    public string LoadPlan()
    {
        try
        {
            if (!Directory.Exists(ItemPath)) return "项不存在：" + ItemPath;
            var code2 = Path.Combine(ItemPath, "Code2");
            if (!File.Exists(code2)) return "项未配置：" + ItemPath;

            Steps.Clear();
            RawPlan.Clear();
            DisableConfigPreservation = false;
            UninstallCancelled = false;

            RawPlan.AddRange(SMUI.Core.IO.KeyValueFile.ReadPairs(code2));

            for (var i = 0; i < RawPlan.Count; i++)
            {
                var (key, value) = RawPlan[i];
                CurrentPlanIndex = i;
                switch (key.Trim())
                {
                    case "CD-D-MODS":
                    case "CD-D-MODS-COVER":
                    case "CD-D-ROOT":
                    case "CD-D-CONTENT":
                    case "CD-F":
                    case "CR-Check-EXIST":
                    case "CR-IN-MODS-VER":
                    case "CR-UN":
                    case "CR-SHELL":
                    case "CR-MSGBOX":
                    case "CD-D-Advanced":
                        Steps.Add(new InstallStep(key.Trim(), value));
                        break;
                    case "CORE-CLASS":
                        var flags = value.Split('|').Select(s => s.Trim()).ToList();
                        if (flags.Contains("CG-DB")) DisableConfigPreservation = true;
                        break;
                    default:
                        if (RecognizeHandlers.TryGetValue(key.Trim(), out var recognizer))
                        {
                            CurrentPlanIndex = i;
                            recognizer();
                        }
                        else
                        {
                            // 未知规划码：仅记录
                            Report?.Invoke(3, $"{key} 不是受支持的规划代码，是否缺失相关插件？");
                        }
                        break;
                }
            }
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // ---------------------------------------------------------------- 安装

    public void ExecuteInstall(int index)
    {
        var step = Steps[index];
        switch (step.Name)
        {
            case "CD-D-MODS": Install_CopyFolderToMods(step.Args); break;
            case "CD-D-MODS-COVER": Install_CoverFolderToMods(step.Args); break;
            case "CD-D-ROOT": Install_CopyFolder(step.Args); break;
            case "CD-D-CONTENT": Install_CoverContent(); break;
            case "CD-F": Install_SingleFile(step.Args); break;
            case "CR-Check-EXIST": CheckExistence(step.Args, forInstall: true); break;
            case "CR-IN-MODS-VER": CheckInstalledModVersion(step.Args); break;
            case "CR-SHELL": RunExecutable(step.Args, forInstall: true); break;
            case "CR-MSGBOX": ShowMessageBox(step.Args, forInstall: true).GetAwaiter().GetResult(); break;
            case "CD-D-Advanced": Install_AdvancedFolder(step.Args); break;
            default:
                if (InstallHandlers.TryGetValue(step.Name, out var installHandler))
                {
                    CurrentPlanIndex = index;
                    installHandler();
                }
                else
                    Report?.Invoke(3, $"{step.Name} 不是受支持的规划代码，是否缺失相关插件？");
                break;
        }
    }

    // ---------------------------------------------------------------- 卸载

    public void ExecuteUninstall(int index)
    {
        var step = Steps[index];
        switch (step.Name)
        {
            case "CD-D-MODS": Uninstall_CopyFolderToMods(step.Args); break;
            case "CD-D-ROOT": Uninstall_CopyFolder(step.Args); break;
            case "CD-D-CONTENT": Uninstall_Content(); break;
            case "CD-F": Uninstall_SingleFile(step.Args); break;
            case "CR-Check-EXIST": CheckExistence(step.Args, forInstall: false); break;
            case "CR-UN": Uninstall_Cancel(step.Args); break;
            case "CR-SHELL": RunExecutable(step.Args, forInstall: false); break;
            case "CR-MSGBOX": ShowMessageBox(step.Args, forInstall: false).GetAwaiter().GetResult(); break;
            case "CD-D-Advanced": Uninstall_AdvancedFolder(step.Args); break;
            default:
                if (UninstallHandlers.TryGetValue(step.Name, out var uninstallHandler))
                {
                    CurrentPlanIndex = index;
                    uninstallHandler();
                }
                else
                    Report?.Invoke(3, $"{step.Name} 不是受支持的规划代码，是否缺失相关插件？");
                break;
        }
    }

    // ---------------------------------------------------------------- 实现

    private void Install_CopyFolderToMods(string args)
    {
        var p = args.Split('|');
        Report1($"正在复制文件夹到 Mods 中：{p[0]}");
        CopyDir(Path.Combine(ItemPath, p[0]), Path.Combine(GamePath, "Mods", p[0]));
        if (!DisableConfigPreservation)
        {
            var backupConfig = Path.Combine(ItemPath, ".config", p[0], "config.json");
            var targetConfig = Path.Combine(GamePath, "Mods", p[0], "config.json");
            if (File.Exists(backupConfig))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetConfig)!);
                File.Copy(backupConfig, targetConfig, true);
                Report1("已还原 config.json");
            }
        }
    }

    private void Install_CoverFolderToMods(string args)
    {
        var p = args.Split('|');
        if (!Directory.Exists(Path.Combine(GamePath, "Mods", p[0])))
            throw new SmuiEngineException("在 Mods 找不到已安装的文件夹：" + p[0]);
        Report1($"正在覆盖 Mods 内已存在文件夹：{p[0]}");
        CopyDir(Path.Combine(ItemPath, p[0]), Path.Combine(GamePath, "Mods", p[0]));
    }

    private void Install_CopyFolder(string args)
    {
        var p = args.Split('|');
        Report1($"正在复制文件夹：{p[0]}");
        CopyDir(Path.Combine(ItemPath, p[0]), Path.Combine(GamePath, p[1]));
    }

    private void Install_CoverContent()
    {
        Report1("正在覆盖游戏 Content 文件夹");
        CopyDir(Path.Combine(ItemPath, "Content"), Path.Combine(GamePath, "Content"));
    }

    private void Install_SingleFile(string args)
    {
        var p = args.Split('|');
        Report1($"正在复制到目标文件：{p[4]}");
        var target = Path.Combine(GamePath, p[4]);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Path.Combine(ItemPath, p[3]), target, true);
    }

    private void CheckExistence(string args, bool forInstall)
    {
        var p = args.Split('|');
        // 安装时跳过标记为 uninstall 的检查；卸载时跳过标记为 install 的检查
        if (forInstall && p[0].Trim().Equals("uninstall", StringComparison.CurrentCultureIgnoreCase)) return;
        if (!forInstall && p[0].Trim().Equals("install", StringComparison.CurrentCultureIgnoreCase)) return;

        var expected = bool.Parse(p[2]);
        if (p[1].Trim().Equals("folder", StringComparison.CurrentCultureIgnoreCase))
        {
            for (var i = 3; i < p.Length; i++)
                if (Directory.Exists(Path.Combine(GamePath, p[i])) != expected)
                    throw new SmuiEngineException($"要检查的文件夹的存在性应该为：{expected}：{p[i]}");
        }
        else if (p[1].Trim().Equals("file", StringComparison.CurrentCultureIgnoreCase))
        {
            for (var i = 3; i < p.Length; i++)
                if (File.Exists(Path.Combine(GamePath, p[i])) != expected)
                    throw new SmuiEngineException($"要检查的文件的存在性应该为：{expected}：{p[i]}");
        }
    }

    private void CheckInstalledModVersion(string args)
    {
        var p = args.Split('|');
        if (p.Length != 3)
            throw new SmuiEngineException("参数数量不正确，请不要擅自修改规划文件");
        // 注意：与原版一致，此处原代码的判断条件为反义（目录存在时反而报"未安装"），
        // 为保证行为一致按原样移植会导致正常流程无法工作，这里采用语义正确的判断。
        var modDir = Path.Combine(GamePath, "Mods", p[0]);
        if (!Directory.Exists(modDir))
            throw new SmuiEngineException("要进行版本检查的模组文件夹未安装：" + p[0]);
        var manifest = Path.Combine(modDir, "manifest.json");
        if (!File.Exists(manifest))
            throw new SmuiEngineException("要进行版本检查的模组文件夹中没有清单文件：" + p[0]);

        string installedVersion;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
            installedVersion = doc.RootElement.TryGetProperty("Version", out var v) ? v.ToString() : "";
        }
        catch
        {
            installedVersion = "";
        }
        if (string.IsNullOrEmpty(installedVersion))
            throw new SmuiEngineException("目标清单文件中不包含版本信息：" + p[0]);

        var op = p[1];
        var target = p[2];
        var cmp = SMUI.Core.Util.VersionHelper.Compare(installedVersion, target);
        var ok = op switch
        {
            "<" => cmp < 0,
            "=" => cmp == 0,
            ">" => cmp > 0,
            "<=" => cmp <= 0,
            ">=" => cmp >= 0,
            "<>" => cmp != 0,
            _ => throw new SmuiEngineException("无法识别比较参数，请重新配置"),
        };
        if (!ok)
            throw new SmuiEngineException($"已安装的模组版本条件不满足：{op} {target}（当前 {installedVersion}）");
    }

    private void RunExecutable(string args, bool forInstall)
    {
        var p = args.Split('|');
        if (forInstall && p[0].Trim().Equals("uninstall", StringComparison.CurrentCultureIgnoreCase)) return;
        if (!forInstall && p[0].Trim().Equals("install", StringComparison.CurrentCultureIgnoreCase)) return;

        if (!File.Exists(Path.Combine(ItemPath, p[1])))
            throw new SmuiEngineException("指定的可执行文件不存在：" + p[1]);

        var exe = Path.Combine(GamePath, p[1]);
        Report1($"运行可执行文件：{p[1]}");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? GamePath,
            FileName = exe,
            Arguments = p.Length > 2 ? p[2] : "",
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc != null && p.Length > 3 && p[3].Trim().Equals("true", StringComparison.CurrentCultureIgnoreCase))
        {
            Report1("等待程序结束");
            proc.WaitForExit();
        }
        Report1("程序结束");
    }

    private async Task ShowMessageBox(string args, bool forInstall)
    {
        var p = args.Split('|');
        if (forInstall && p[0].Trim().Equals("uninstall", StringComparison.CurrentCultureIgnoreCase)) return;
        if (!forInstall && p[0].Trim().Equals("install", StringComparison.CurrentCultureIgnoreCase)) return;

        var options = new List<string>();
        for (var i = 5; i < p.Length; i++) options.Add(p[i]);

        int selected = -1;
        if (ShowChoice != null)
            selected = await ShowChoice(new ChoiceRequest(p[1], p[2].Replace("<br>", Environment.NewLine), options));

        if (p[3].Trim().Equals("true", StringComparison.CurrentCultureIgnoreCase))
        {
            if (selected != int.Parse(p[4]) - 1)
                throw new SmuiEngineException("没有选择正确的选项");
        }
    }

    private void Uninstall_CopyFolderToMods(string args)
    {
        var p = args.Split('|');
        if (!DisableConfigPreservation)
        {
            var backupConfig = Path.Combine(ItemPath, ".config", p[0], "config.json");
            var targetConfig = Path.Combine(GamePath, "Mods", p[0], "config.json");
            if (File.Exists(targetConfig))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupConfig)!);
                File.Copy(targetConfig, backupConfig, true);
                Report1("已备份 config.json");
            }
        }
        Report1($"正在删除 Mods 中的文件夹：{p[0]}");
        DeleteDir(Path.Combine(GamePath, "Mods", p[0]));
    }

    private void Uninstall_CopyFolder(string args)
    {
        var p = args.Split('|');
        Report1($"正在删除文件夹：{p[0]}");
        DeleteDir(Path.Combine(GamePath, p[1]));
    }

    private void Uninstall_Content()
    {
        Report1("正在按照 Content 结构卸载文件");
        RestoreOrDeleteTree(Path.Combine(ItemPath, "Content"));
    }

    private void Uninstall_SingleFile(string args)
    {
        var p = args.Split('|');
        Report1($"正在删除目标文件：{p[4]}");
        var target = Path.Combine(GamePath, p[4]);
        if (File.Exists(target)) File.Delete(target);
        if (p[0].Trim().Equals("true", StringComparison.CurrentCultureIgnoreCase))
        {
            var backup = Path.Combine(GameBackupPath, p[4]);
            if (File.Exists(backup))
            {
                Report1($"找到备份，正在还原目标文件：{p[4]}");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(backup, target, true);
            }
            else
            {
                Report1($"未找到备份：{p[4]}");
            }
        }
    }

    private void Uninstall_Cancel(string args)
    {
        var p = args.Split('|');
        switch (p[0])
        {
            case "ERROR":
                throw new SmuiEngineException("此项禁止卸载");
            case "CANCEL":
                Report1("规划数据设定取消卸载操作");
                UninstallCancelled = true;
                break;
        }
    }

    // ---------------------------------------------------------------- 文件夹高级安装 (CD-D-Advanced)
    // 参数格式：<卸载时如何操作 ReStore/Delete>|<如何还原 Delete-Copy/Cover>|<要安装的文件夹>|<目标位置>

    private void Install_AdvancedFolder(string args)
    {
        var p = args.Split('|');
        Report1($"正在安装文件夹：{p[3]}");
        CopyDir(Path.Combine(ItemPath, p[2]), Path.Combine(GamePath, p[3]));
    }

    private void Uninstall_AdvancedFolder(string args)
    {
        var p = args.Split('|');
        if (p[0].Trim().Equals("ReStore", StringComparison.CurrentCultureIgnoreCase))
        {
            var backup = Path.Combine(GameBackupPath, p[3]);
            if (Directory.Exists(backup))
            {
                if (p[1].Trim().Equals("Delete-Copy", StringComparison.CurrentCultureIgnoreCase))
                {
                    Report1($"正在删除文件夹：{p[3]}");
                    DeleteDir(Path.Combine(GamePath, p[3]));
                    Report1($"正在从备份中还原：{p[3]}");
                    CopyDir(backup, Path.Combine(GamePath, p[3]));
                }
                else
                {
                    Report1($"正在从备份中覆盖：{p[3]}");
                    CopyDir(backup, Path.Combine(GamePath, p[3]));
                }
            }
            else
            {
                Report1($"找不到对应的文件夹备份：{p[3]}，只能直接删除，注意这可能引发意外情况");
                DeleteDir(Path.Combine(GamePath, p[3]));
            }
        }
        else
        {
            Report1($"正在删除文件夹：{p[3]}");
            DeleteDir(Path.Combine(GamePath, p[3]));
        }
    }

    // ---------------------------------------------------------------- 文件工具

    private static void CopyDir(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDir(dir, Path.Combine(target, Path.GetFileName(dir)));
    }

    private static void DeleteDir(string target)
    {
        if (Directory.Exists(target))
            Directory.Delete(target, true);
    }

    /// <summary>按备份优先还原、无备份则删除的方式递归处理目录（对应原版 卸载CDVD）。</summary>
    private void RestoreOrDeleteTree(string sourceDir)
    {
        if (!Directory.Exists(sourceDir)) return;
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            try
            {
                var relative = Path.GetRelativePath(ItemPath, file);
                var backup = Path.Combine(GameBackupPath, relative);
                var gameTarget = Path.Combine(GamePath, relative);
                if (File.Exists(backup))
                {
                    Report1($"找到备份，正在还原：{relative}");
                    Directory.CreateDirectory(Path.GetDirectoryName(gameTarget)!);
                    File.Copy(backup, gameTarget, true);
                }
                else
                {
                    Report1($"找不到备份，正在删除：{relative}");
                    if (File.Exists(gameTarget)) File.Delete(gameTarget);
                }
            }
            catch { }
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
            RestoreOrDeleteTree(dir);
    }
}
