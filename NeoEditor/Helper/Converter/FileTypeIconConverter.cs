using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentIcons.Common;

namespace NeoEditor.Helper.Converter;

public class FileTypeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var name = value?.ToString()?.ToLowerInvariant() ?? "";
        return name switch
        {
            _ when name.EndsWith(".png") || name.EndsWith(".jpg") || name.EndsWith(".jpeg") ||
                   name.EndsWith(".gif") || name.EndsWith(".bmp") || name.EndsWith(".svg") ||
                   name.EndsWith(".tiff") || name.EndsWith(".webp") => Symbol.Image,

            _ when name.EndsWith(".xml") => Symbol.Code,
            _ when name.EndsWith(".php") => Symbol.Code,
            _ when name.EndsWith(".json") => Symbol.CodeBlock,
            _ when name.EndsWith(".md") || name.EndsWith(".markdown") => Symbol.DocumentBulletList,
            _ when name.EndsWith(".cs") || name.EndsWith(".csproj") || name.EndsWith(".sln") => Symbol.Code,
            _ when name.EndsWith(".txt") || name.EndsWith(".log") => Symbol.DocumentOnePage,
            _ when name.EndsWith(".zip") || name.EndsWith(".rar") || name.EndsWith(".7z") => Symbol.Box,
            _ when name.EndsWith(".db") || name.EndsWith(".sqlite") => Symbol.Database,
            _ when name.EndsWith(".pdf") => Symbol.DocumentPdf,
            _ when name.EndsWith(".exe") || name.EndsWith(".dll") => Symbol.AppGeneric,
            _ when name.EndsWith(".avi") || name.EndsWith(".mp4") || name.EndsWith(".webm") ||
                   name.EndsWith(".mov") || name.EndsWith(".mkv") => Symbol.VideoClip,
            _ when name.EndsWith(".mp3") || name.EndsWith(".wav") || name.EndsWith(".ogg") ||
                   name.EndsWith(".flac") => Symbol.MusicNote1,
            _ when name.EndsWith(".html") || name.EndsWith(".htm") || name.EndsWith(".css") ||
                   name.EndsWith(".js") => Symbol.Code,
            _ => Symbol.Document
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
