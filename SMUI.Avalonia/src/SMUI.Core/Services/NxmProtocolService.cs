using System.Diagnostics;

namespace SMUI.Core.Services;

/// <summary>
/// nxm 下载协议注册（对应原版 常驻主题 的 注册表菜单：把 NEXUS 的 nxm: 链接转接给 SMUI）。
/// 仅 Windows 有效。
/// </summary>
public static class NxmProtocolService
{
    /// <summary>注册/更新 nxm 协议指向当前程序。返回错误信息，成功为空。</summary>
    public static string Register()
    {
        if (!OperatingSystem.IsWindows()) return "仅 Windows 支持注册 nxm 协议。";
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return "无法获取当前程序路径。";
        try
        {
            using var key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey("nxm");
            key.SetValue("", "URL:NXM Protocol");
            key.SetValue("URL Protocol", "");
            using var command = key.CreateSubKey(@"shell\open\command");
            command.SetValue("", $"\"{exePath}\" \"%1\"");
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message + "\n\n如果遇到权限问题，请暂时以管理员身份运行 SMUI，注册成功后再以普通权限运行。";
        }
    }

    /// <summary>移除 nxm 协议注册。返回错误信息，成功为空。</summary>
    public static string Remove()
    {
        if (!OperatingSystem.IsWindows()) return "仅 Windows 支持该操作。";
        try
        {
            using (var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("nxm"))
            {
                if (key == null) return "协议尚未注册。";
            }
            Microsoft.Win32.Registry.ClassesRoot.DeleteSubKeyTree("nxm");
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
