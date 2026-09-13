using System.Globalization;
using Avalonia.Data.Converters;

namespace SMUI.App.Views;

/// <summary>int 相等比较（value == parameter 时 true）——设置二级菜单面板显隐用。</summary>
public class IndexEqualsConverter : IValueConverter
{
    public static readonly IndexEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int a && int.TryParse(parameter?.ToString(), out var b) && a == b;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
