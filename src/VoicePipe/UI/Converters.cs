using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VoicePipe.UI;

/// <summary>bool → 绿/灰色（用于状态指示椭圆）</summary>
public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true
            ? Color.FromRgb(52, 211, 153)   // 绿
            : Color.FromRgb(74, 106, 112);  // 灰

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>bool → 取反（用于按钮 IsEnabled 绑定）</summary>
public class BoolNegateConverter : IValueConverter
{
    public static readonly BoolNegateConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

/// <summary>bool True → Visible, False → Collapsed</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>bool True → Collapsed, False → Visible（取反版）</summary>
public class BoolToInvVisibilityConverter : IValueConverter
{
    public static readonly BoolToInvVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>float (0~1) → Width（峰值条宽度）</summary>
public class PeakToWidthConverter : IValueConverter
{
    public static readonly PeakToWidthConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double maxWidth = parameter is string s && double.TryParse(s, out var d) ? d : 52;
        return value is float f ? Math.Clamp((double)f, 0, 1) * maxWidth : 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// float 峰值 → 笔刷：达到/超过 0.99（接近满刻度）时返回主题 ClipBrush（红），
/// 否则返回主题 AccentBrush（正常）。两个笔刷均从当前主题资源解析，
/// 未找到时回退到硬编码的红 / 绿，保证削波始终可见。
/// </summary>
public class PeakToClipBrushConverter : IValueConverter
{
    public static readonly PeakToClipBrushConverter Instance = new();

    private static readonly Brush FallbackClip = new SolidColorBrush(Color.FromRgb(0xE0, 0x00, 0x00));   // 红
    private static readonly Brush FallbackNormal = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)); // 绿

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool clipping = value is float f && f >= 0.99f;
        string key = clipping ? "ClipBrush" : "AccentBrush";
        Brush fallback = clipping ? FallbackClip : FallbackNormal;
        return (Application.Current?.TryFindResource(key) as Brush) ?? fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// RadioButton 绑定字符串值：Convert(value, param) = (value == param)；
/// ConvertBack：IsChecked=true 时返回 param，false 返回 DoNothing（不触发更新）。
/// </summary>
public class StringEqualConverter : IValueConverter
{
    public static readonly StringEqualConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && parameter is string p && string.Equals(s, p, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true && parameter is string p ? p : System.Windows.Data.Binding.DoNothing;
}
