using System;
using System.Globalization;

namespace NeoEditor.Helper.Converter;

public static class ValueConverter
{
    public static object Convert(string str, Type targetType)
    {
        if (targetType == typeof(string)) return str;
        if (targetType == typeof(int)) return int.Parse(str);
        if (targetType == typeof(float)) return float.Parse(str, CultureInfo.InvariantCulture);
        if (targetType == typeof(bool)) return str == "1" || str.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (targetType == typeof(byte)) return byte.Parse(str);
        if (targetType.IsEnum) return Enum.Parse(targetType, str);
        // 可根据需要扩展
        return System.Convert.ChangeType(str, targetType);
    }
}