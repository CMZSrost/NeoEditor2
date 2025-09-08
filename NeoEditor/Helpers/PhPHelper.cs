using System.IO;
using System.Text.RegularExpressions;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.Helpers;

public static class PhPHelper
{
    private static readonly Regex NRowPatten = new("nRows=([0-9]+?)", RegexOptions.IgnoreCase);

    private static readonly Regex NModPatten = new("&strModName([0-9]+?)=(.*)&strModURL[0-9]+?=(.*)",
        RegexOptions.IgnoreCase);

    public static async IAsyncEnumerable<ModData> FileToList(string phpFilePath)
    {
        using var f = File.OpenText(phpFilePath);
        var nRowLine = await f.ReadLineAsync() ?? throw new Exception("Invalid PHP file");
        // Console.WriteLine(nRowLine);
        var nrows = int.Parse(NRowPatten.Match(nRowLine).Groups[1].Value);
        for (var i = 0; i <= nrows; i++)
        {
            var line = await f.ReadLineAsync();
            // Console.WriteLine(line);
            var match = NModPatten.Match(line ?? string.Empty);
            if (match.Success)
                yield return new ModData
                {
                    ModIndex = i,
                    ModName = match.Groups[2].Value,
                    ModDirectoryPath = match.Groups[3].Value,
                    ModDirectory = Path.GetFileName(match.Groups[3].Value)
                };
        }
    }

    public static async Task<bool> ListToFile(string phpFilePath, ICollection<ModData> list)
    {
        await using var tempFile = File.Create(
            phpFilePath + ".tmp",
            1024 * 1024,
            FileOptions.DeleteOnClose
        );
        try
        {
            // 创建空临时文件
            await using var f = new StreamWriter(tempFile);
            await f.WriteLineAsync($"nRows={list.Count()}");
            foreach (var modData in list)
                await f.WriteLineAsync(
                    $"&strModName{modData.ModIndex}={modData.ModName}&strModURL{modData.ModIndex}={modData.ModDirectoryPath}");
            File.Copy(phpFilePath + ".tmp", phpFilePath, true);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }
}