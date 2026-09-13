using System.Globalization;
using Avalonia.Data.Converters;

namespace SMUI.App.Views;

/// <summary>int ↔ bool 双向（RadioButton 群绑定步骤索引）。</summary>
public class IndexToBoolConverter : IValueConverter
{
    public static readonly IndexToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int a && int.TryParse(parameter?.ToString(), out var b) && a == b;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && int.TryParse(parameter?.ToString(), out var b) ? b : Avalonia.Data.BindingOperations.DoNothing;
}
