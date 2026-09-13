using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SMUI.App.Views;

/// <summary>IEnumerable 有元素则 true（MenuItem 子菜单箭头显隐用）。</summary>
public class HasItemsConverter : IValueConverter
{
    public static readonly HasItemsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IEnumerable e && e.GetEnumerator().MoveNext();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
