using System;
using System.IO;
using NeoEditor.Plugins.ImageTools.Services;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.Services;

/// <summary>
/// ai-prompt-presets.json loading: auto-create defaults on first run, user JSON
/// parsing, corrupt-file fallback. The service must never throw — the AI panel
/// dropdown depends on it.
/// </summary>
public class AiPromptPresetServiceTests
{
    private static string TempPath()
        => Path.Combine(Path.GetTempPath(), "ne_ai_presets_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void MissingFile_WritesDefaults_AndReturnsThem()
    {
        var path = TempPath();
        try
        {
            var svc = new AiPromptPresetService(path);

            var presets = svc.GetPresets();

            Assert.Equal(AiPromptPresetService.DefaultPresets.Count, presets.Count);
            Assert.All(presets, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
            Assert.All(presets, p => Assert.False(string.IsNullOrWhiteSpace(p.Prompt)));
            Assert.True(File.Exists(path)); // the file was auto-created for the user to edit

            // A fresh service re-reads the auto-created file — the comment header must round-trip.
            var reloaded = new AiPromptPresetService(path);
            Assert.Equal(AiPromptPresetService.DefaultPresets.Count, reloaded.GetPresets().Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void UserJson_IsParsed()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, """
            [
              { "name": "自定义模板", "prompt": "custom prompt", "width": 640, "height": 480 }
            ]
            """);

            var presets = new AiPromptPresetService(path).GetPresets();

            var preset = Assert.Single(presets);
            Assert.Equal("自定义模板", preset.Name);
            Assert.Equal("custom prompt", preset.Prompt);
            Assert.Equal(640, preset.Width);
            Assert.Equal(480, preset.Height);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void UserJson_WithComments_IsParsed()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, """
            // 用户可以在文件里写注释说明
            [
              { "name": "A", "prompt": "prompt A" }
            ]
            """);

            var presets = new AiPromptPresetService(path).GetPresets();

            Assert.Equal("A", Assert.Single(presets).Name);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void InvalidJson_FallsBackToDefaults()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "{ this is not json !!");

            var presets = new AiPromptPresetService(path).GetPresets();

            Assert.Equal(AiPromptPresetService.DefaultPresets.Count, presets.Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void BlankEntries_AreDropped()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, """
            [
              { "name": "", "prompt": "no name" },
              { "name": "B", "prompt": "   " },
              { "name": "C", "prompt": "good" }
            ]
            """);

            var presets = new AiPromptPresetService(path).GetPresets();

            Assert.Equal("C", Assert.Single(presets).Name);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void AddOrUpdatePreset_AppendsAndPersists()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, """
            [
              { "name": "A", "prompt": "prompt A" }
            ]
            """);
            var svc = new AiPromptPresetService(path);

            svc.AddOrUpdatePreset(new AiPromptPreset("B", "prompt B", 640, 480));

            // A fresh instance re-reads the file — the new preset survived.
            var reloaded = new AiPromptPresetService(path).GetPresets();
            Assert.Equal(2, reloaded.Count);
            Assert.Equal("prompt A", reloaded[0].Prompt);
            Assert.Equal("B", reloaded[1].Name);
            Assert.Equal(640, reloaded[1].Width);
            Assert.Equal(480, reloaded[1].Height);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void AddOrUpdatePreset_ReplacesSameName_WithoutDuplicating()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, """
            [
              { "name": "A", "prompt": "old" },
              { "name": "B", "prompt": "keep me" }
            ]
            """);
            var svc = new AiPromptPresetService(path);

            svc.AddOrUpdatePreset(new AiPromptPreset("A", "new", 100, 100));

            var presets = svc.GetPresets();
            Assert.Equal(2, presets.Count);
            Assert.Equal("new", presets[0].Prompt);
            Assert.Equal("keep me", presets[1].Prompt);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void AddOrUpdatePreset_WhenFileMissing_AddsToDefaults()
    {
        var path = TempPath();
        try
        {
            var svc = new AiPromptPresetService(path);

            svc.AddOrUpdatePreset(new AiPromptPreset("我的模板", "my prompt"));

            // First-run semantics: missing file → defaults are written, the new preset joins them.
            Assert.True(File.Exists(path));
            var presets = new AiPromptPresetService(path).GetPresets();
            Assert.Equal(AiPromptPresetService.DefaultPresets.Count + 1, presets.Count);
            Assert.Contains(presets, p => p.Name == "我的模板");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void AddOrUpdatePreset_OverwritesCorruptFile()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "{ broken !!");
            var svc = new AiPromptPresetService(path);

            svc.AddOrUpdatePreset(new AiPromptPreset("修复", "fixed"));

            // The corrupt file is replaced with defaults + the new preset.
            var presets = new AiPromptPresetService(path).GetPresets();
            Assert.Contains(presets, p => p.Name == "修复");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
