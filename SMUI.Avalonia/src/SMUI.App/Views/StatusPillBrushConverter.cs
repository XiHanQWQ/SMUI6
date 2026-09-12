using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SMUI.App.Views;

/// <summary>状态 → 半透明 pill 背景。</summary>
public class StatusPillBrushConverter : IValueConverter
{
    public static readonly StatusPillBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var base_ = value is string k ? StatusColorConverter.FindBrush("Status" + k[..1].ToUpper() + k[1..] + "Brush") : Brushes.White;
        if (base_ is SolidColorBrush solid)
            return new SolidColorBrush(solid.Color, 0.18);
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
