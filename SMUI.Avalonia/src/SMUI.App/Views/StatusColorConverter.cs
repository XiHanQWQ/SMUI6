using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SMUI.App.Views;

/// <summary>状态颜色 Key → 画刷。</summary>
public class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key) return Brushes.White;
        return key switch
        {
            "red" => FindBrush("StatusRedBrush"),
            "orange" => FindBrush("StatusOrangeBrush"),
            "yellow" => FindBrush("StatusYellowBrush"),
            "green" => FindBrush("StatusGreenBrush"),
            "cyan" => FindBrush("StatusCyanBrush"),
            "blue" => FindBrush("StatusBlueBrush"),
            "purple" => FindBrush("StatusPurpleBrush"),
            _ => FindBrush("StatusWhiteBrush"),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public static IBrush FindBrush(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var v) == true && v is IBrush b)
            return b;
        return Brushes.White;
    }
}
