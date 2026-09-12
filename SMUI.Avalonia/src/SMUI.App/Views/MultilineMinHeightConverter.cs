using System.Globalization;
using Avalonia.Data.Converters;

namespace SMUI.App.Views;

/// <summary>多行输入框最小高度转换：多行 200，单行 0。</summary>
public class MultilineMinHeightConverter : IValueConverter
{
    public static readonly MultilineMinHeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 200d : 0d;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
