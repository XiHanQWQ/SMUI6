namespace SMUI.Core.Models;

/// <summary>安装状态 Key 与显示文本（与原版 项信息读取类.安装状态字典 一致）。</summary>
public static class InstallStatus
{
    public const string UnKnow = "UnKnow";
    public const string NoConfigured = "NoConfigured";
    public const string Installed = "Installed";
    public const string UnInstalled = "UnInstalled";
    public const string Incomplete = "Incomplete";
    public const string FolderCopied = "FolderCopied";
    public const string FolderNoCopied = "FolderNoCopied";
    public const string IncompleteFolderCopied = "IncompleteFolderCopied";
    public const string Additional = "Additional";
    public const string FileInstalled = "FileInstalled";
    public const string FileUnInstalled = "FileUnInstalled";
    public const string FileIncomplete = "FileIncomplete";
    public const string FileInstalledVerified = "FileInstalledVerified";
    public const string FileInstalledVerifyfailed = "FileInstalledVerifyfailed";
    public const string FolderMissing = "FolderMissing";
    public const string FileMissing = "FileMissing";
    public const string File = "File";
    public const string CoverContent = "CoverContent";
    public const string MissingCalculationProgram = "MissingCalculationProgram";

    // 文件夹高级安装引入的状态
    public const string ExistedFolder = "ExistedFolder";
    public const string FolderNotInstall = "FolderNotInstall";

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [UnKnow] = "未知",
        [NoConfigured] = "未配置",
        [Installed] = "已安装",
        [UnInstalled] = "未安装",
        [Incomplete] = "安装不完整",
        [FolderCopied] = "文件夹已复制",
        [FolderNoCopied] = "文件夹未复制",
        [IncompleteFolderCopied] = "文件夹部分复制",
        [Additional] = "附加内容",
        [FileInstalled] = "文件已安装",
        [FileUnInstalled] = "文件未安装",
        [FileIncomplete] = "文件部分安装",
        [FileInstalledVerified] = "文件已安装 (验证)",
        [FileInstalledVerifyfailed] = "文件未安装 (验证)",
        [FolderMissing] = "源文件夹丢失",
        [FileMissing] = "源文件丢失",
        [File] = "不带判断的文件",
        [CoverContent] = "覆盖 Content",
        [MissingCalculationProgram] = "缺少判断程序",
        [ExistedFolder] = "已存在的文件夹",
        [FolderNotInstall] = "文件夹未安装",
    };

    /// <summary>状态 → 主题色名称（与原版颜色标记映射一致）。</summary>
    public static string ColorKeyOf(string status) => status switch
    {
        UnInstalled or FileUnInstalled => "white",
        Installed => "green",
        Incomplete or FileInstalledVerifyfailed => "cyan",
        FolderCopied or FileInstalled or Additional or CoverContent or FileInstalledVerified or ExistedFolder => "purple",
        File => "blue",
        NoConfigured or UnKnow or MissingCalculationProgram => "red",
        FolderNotInstall => "white",
        _ => "white",
    };

    public static string DisplayName(string status)
        => DisplayNames.TryGetValue(status, out var name) ? name : "未定义的状态值";
}
