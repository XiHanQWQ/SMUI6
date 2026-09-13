using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;

namespace SMUI.App.ViewModels;

/// <summary>起始页「扩展内容」子页（复刻原版 用户插件.加载用户插件：扫描 UserData\Plugin\*.smui.dll 并尝试加载）。</summary>
public partial class PluginsViewModel : ViewModelBase
{
    public ObservableCollection<PluginRow> Plugins { get; } = new();

    [ObservableProperty]
    private string _summaryText = "";

    /// <summary>重新扫描插件目录。每次切到「扩展内容」页时调用（与原版行为一致：列表体现当前已加载状态）。</summary>
    [RelayCommand]
    public void Refresh()
    {
        Plugins.Clear();
        var dir = Path.Combine(AppServices.Settings.UserDataDirectory, "Plugin");
        if (!Directory.Exists(dir))
        {
            SummaryText = "尚未创建插件目录（UserData\\Plugin）";
            return;
        }

        var loaded = 0;
        var failed = 0;
        foreach (var filePath in Directory.EnumerateFiles(dir, "*.smui.dll"))
        {
            var fileName = Path.GetFileName(filePath);
            var row = new PluginRow();
            try
            {
                var assembly = Assembly.LoadFrom(filePath);
                var entryType = assembly.GetType(assembly.GetName().Name + ".Entry");
                var instance = Activator.CreateInstance(entryType!);
                entryType!.GetMethod("Entry")!.Invoke(instance, Array.Empty<object>());
                row.Name = assembly.GetName().Name ?? fileName;
#pragma warning disable SYSLIB0037
                row.Architecture = assembly.GetName().ProcessorArchitecture.ToString();
#pragma warning restore SYSLIB0037
                row.Version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "";
                row.Author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "";
                row.Failed = false;
                loaded++;
                AppServices.Log.Print($"已加载插件：{fileName}", LogKind.Info);
            }
            catch (Exception ex)
            {
                row.Name = fileName;
                row.Version = "";
                row.Author = "";
                row.Architecture = RuntimeInformation.ProcessArchitecture.ToString();
                row.Failed = true;
                failed++;
                row.ErrorText = ex.Message;
                AppServices.Log.Print($"加载插件错误：{fileName} — {ex.Message}", LogKind.Error);
            }
            Plugins.Add(row);
        }

        SummaryText = Plugins.Count == 0
            ? "没有发现插件（将 .smui.dll 放入 UserData\\Plugin 目录后刷新）"
            : $"共 {Plugins.Count} 个插件，成功 {loaded} 个，失败 {failed} 个";
    }
}

/// <summary>插件列表行（失败行显示红色，与原版一致）。</summary>
public partial class PluginRow : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _version = "";
    [ObservableProperty] private string _author = "";
    [ObservableProperty] private string _architecture = "";
    [ObservableProperty] private bool _failed;
    [ObservableProperty] private string _errorText = "";
}
