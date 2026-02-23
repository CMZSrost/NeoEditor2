namespace NeoEditor.Helper.Extensions;

public static class StringExtension
{
    public static string ToCamelCase(this string str)
    {
        if (string.IsNullOrEmpty(str) || !char.IsUpper(str[0]))
            return str;
        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
}