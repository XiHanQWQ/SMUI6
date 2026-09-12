using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SMUI.App.Services;

public enum LogKind
{
    Normal,   // 白
    Info,     // 蓝
    Error,    // 红
    Success,  // 绿
    Warning,  // 橙
}

public record LogEntry(DateTime Time, LogKind Kind, string Message)
{
    public string TimeString => Time.ToString("HH:mm:ss");

    public Avalonia.Media.IBrush KindBrush => Kind switch
    {
        LogKind.Info => LogKindBrushes.Info,
        LogKind.Error => LogKindBrushes.Error,
        LogKind.Success => LogKindBrushes.Success,
        LogKind.Warning => LogKindBrushes.Warning,
        _ => LogKindBrushes.Normal,
    };
}

public static class LogKindBrushes
{
    private static Avalonia.Media.IBrush Brush(string hex) =>
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex));

    public static readonly Avalonia.Media.IBrush Normal = Brush("#E8E8F0");
    public static readonly Avalonia.Media.IBrush Info = Brush("#4FB8FF");
    public static readonly Avalonia.Media.IBrush Error = Brush("#FF6A45");
    public static readonly Avalonia.Media.IBrush Success = Brush("#63D878");
    public static readonly Avalonia.Media.IBrush Warning = Brush("#FFA23E");
}

/// <summary>调试输出（对应原版 DebugPrint → 调试输出选项卡）。</summary>
public class UiLogService : ObservableObject
{
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public event Action<LogEntry>? EntryAdded;

    public void Print(string message, LogKind kind = LogKind.Normal)
        => PrintOn(DateTime.Now, message, kind);

    public void PrintOn(DateTime time, string message, LogKind kind)
    {
        var entry = new LogEntry(time, kind, message);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Entries.Add(entry);
            // 限制日志量，防止内存无限增长
            while (Entries.Count > 2000) Entries.RemoveAt(0);
            EntryAdded?.Invoke(entry);
            OnPropertyChanged(nameof(LatestText));
        });
    }

    public string LatestText => Entries.Count > 0 ? Entries[^1].Message : "就绪";
}
