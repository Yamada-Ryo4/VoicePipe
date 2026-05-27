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
