using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeoEditor.Player.Core.ViewModels;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

public class SaveEditorViewModelTests
{
    private const string TreeJson =
        "{\"name\":\"nsSGv1\",\"formatVersion\":3,\"body\":[{\"name\":\"objSG\"," +
        "\"value\":{\"__amf\":\"object\",\"className\":\"Creature\",\"names\":[\"m_fHealth\"]," +
        "\"values\":[{\"__n\":0.9}],\"dynamic\":[],\"isDynamic\":true}}]}";

    private sealed class FakeJs
    {
        public readonly List<string> Scripts = [];
        public Func<string, string>? Responder;

        public Func<string, Task<string?>> Executor => script =>
        {
            Scripts.Add(script);
            return Task.FromResult<string?>(Responder?.Invoke(script) ?? "null");
        };
    }

    private sealed class Flag
    {
        public bool Value;
    }

    private static SaveEditorViewModel Create(FakeJs js, Flag restarted)
        => new SaveEditorViewModel(js.Executor,
            key => key switch
            {
                "SaveEditor.Summary" => "LSO: {0} · v{1} · {2} root entries",
                "SaveEditor.LoadFailed" => "Load failed: {0}",
                "SaveEditor.SaveFailed" => "Save failed: {0}",
                "SaveEditor.Saved" => "Saved ({0} bytes)",
                "SaveEditor.Loaded" => "Loaded ({0} KB)",
                "SaveEditor.Title" => "Save Editor",
                _ => key,
            },
            restartGame: () => restarted.Value = true);

    [Fact]
    public async Task RefreshListsSaveKeys()
    {
        var js = new FakeJs { Responder = _ => "[{\"k\":\"127.0.0.1/NEOScavenger.swf/nsSGv1\",\"s\":82920}]" };
        var vm = Create(js, new Flag());

        await vm.RefreshCommand.ExecuteAsync(null);

        var entry = Assert.Single(vm.Saves);
        Assert.Equal("127.0.0.1/NEOScavenger.swf/nsSGv1", entry.Key);
        Assert.Equal(82920, entry.Length);
    }

    [Fact]
    public async Task LoadBuildsNodeTreeWithSummaryAndTitle()
    {
        var js = new FakeJs { Responder = _ => TreeJson };
        var vm = Create(js, new Flag());
        vm.Saves.Add(new SaveEntry("127.0.0.1/NEOScavenger.swf/nsSGv1", 82920));
        vm.SelectedSave = vm.Saves[0];

        await vm.LoadCommand.ExecuteAsync(null);

        var root = Assert.IsType<SaveObjectNode>(Assert.Single(vm.RootNodes));
        Assert.Equal("objSG", root.Name);
        Assert.Equal("Creature", root.ClassName);
        var health = Assert.IsType<SaveScalarNode>(Assert.Single(root.SealedValues));
        Assert.Equal("m_fHealth", health.Name);
        Assert.Equal("0.9", health.ValueText);
        Assert.Contains("nsSGv1", vm.SummaryText);
        Assert.Contains("objSG", vm.SummaryText);
        Assert.Contains("nsSGv1", vm.WindowTitle);
    }

    [Fact]
    public async Task LoadReportsParseError()
    {
        var js = new FakeJs { Responder = _ => "{\"error\":\"非 LSO 存档（缺少 AL8A 前缀）\"}" };
        var vm = Create(js, new Flag());
        vm.Saves.Add(new SaveEntry("k", 1));
        vm.SelectedSave = vm.Saves[0];

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("非 LSO 存档", vm.StatusText);
        Assert.Empty(vm.RootNodes);
    }

    [Fact]
    public async Task SaveWritesEditedValueViaFromTreeAndSetItem()
    {
        var js = new FakeJs { Responder = script => script.Contains("LsoExpand.toTree") ? TreeJson : script.Contains("Object.keys") ? "[{\"k\":\"k\",\"s\":1}]" : "{\"ok\":true,\"len\":82920}" };
        var vm = Create(js, new Flag());
        vm.Saves.Add(new SaveEntry("127.0.0.1/NEOScavenger.swf/nsSGv1", 82920));
        vm.SelectedSave = vm.Saves[0];
        await vm.LoadCommand.ExecuteAsync(null);
        // 用户修改：m_fHealth 0.9 → 0.5
        var health = Assert.IsType<SaveScalarNode>(Assert.IsType<SaveObjectNode>(vm.RootNodes[0]).SealedValues[0]);
        health.ValueText = "0.5";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(3, js.Scripts.Count);   // 加载 + 保存 + 保存后刷新列表
        Assert.Contains("LsoExpand.fromTree", js.Scripts[1]);
        Assert.Contains("localStorage.setItem", js.Scripts[1]);
        Assert.Contains("0.5", js.Scripts[1]);   // 脚本里 JSON 已转义（\"__n\":0.5）
        Assert.DoesNotContain("0.9", js.Scripts[1].Substring(js.Scripts[1].IndexOf("fromTree", StringComparison.Ordinal)));
        Assert.Contains("Saved (82920 bytes)", vm.StatusText);
    }

    [Fact]
    public async Task SaveReportsEncoderError()
    {
        var js = new FakeJs { Responder = script => script.Contains("LsoExpand.toTree") ? TreeJson : script.Contains("Object.keys") ? "[{\"k\":\"k\",\"s\":1}]" : "{\"error\":\"编码失败: 无法编码的值\"}" };
        var vm = Create(js, new Flag());
        vm.Saves.Add(new SaveEntry("k", 1));
        vm.SelectedSave = vm.Saves[0];
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains("编码失败", vm.StatusText);
    }

    [Fact]
    public async Task SaveWithInvalidNumberShowsFieldErrorWithoutCallingJs()
    {
        var js = new FakeJs { Responder = _ => TreeJson };
        var vm = Create(js, new Flag());
        vm.Saves.Add(new SaveEntry("k", 1));
        vm.SelectedSave = vm.Saves[0];
        await vm.LoadCommand.ExecuteAsync(null);
        var health = Assert.IsType<SaveScalarNode>(Assert.IsType<SaveObjectNode>(vm.RootNodes[0]).SealedValues[0]);
        health.ValueText = "abc";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains("m_fHealth", vm.StatusText);
        Assert.DoesNotContain(js.Scripts, s => s.Contains("fromTree"));   // 序列化失败 → 不发保存脚本
    }

    [Fact]
    public async Task SaveAsWritesToNewKey()
    {
        var js = new FakeJs { Responder = script => script.Contains("LsoExpand.toTree") ? TreeJson : script.Contains("Object.keys") ? "[{\"k\":\"k\",\"s\":1}]" : "{\"ok\":true,\"len\":10}" };
        var vm = Create(js, new Flag());
        vm.Saves.Add(new SaveEntry("k", 1));
        vm.SelectedSave = vm.Saves[0];
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.SaveAsAsync("127.0.0.1/NEOScavenger.swf/nsSGv1-copy");

        Assert.Equal(3, js.Scripts.Count);   // 加载 + 保存 + 保存后刷新列表
        Assert.Contains("localStorage.setItem", js.Scripts[1]);
        Assert.Contains("nsSGv1-copy", js.Scripts[1]);
    }

    [Fact]
    public async Task SaveAndLoadRestartsGameAfterWrite()
    {
        var js = new FakeJs { Responder = script => script.Contains("LsoExpand.toTree") ? TreeJson : script.Contains("Object.keys") ? "[{\"k\":\"k\",\"s\":1}]" : "{\"ok\":true,\"len\":10}" };
        var restarted = new Flag();
        var vm = Create(js, restarted);
        vm.Saves.Add(new SaveEntry("k", 1));
        vm.SelectedSave = vm.Saves[0];
        await vm.LoadCommand.ExecuteAsync(null);
        var raised = false;
        vm.SavedAndLoaded += () => raised = true;

        await vm.SaveAndLoadCommand.ExecuteAsync(null);

        Assert.True(restarted.Value);
        Assert.True(raised);
    }

    [Fact]
    public async Task SaveWithoutSelectionShowsHint()
    {
        var js = new FakeJs();
        var vm = Create(js, new Flag());

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains("NoSelection", vm.StatusText);   // localize 直通 key
        Assert.Empty(js.Scripts);
    }
}
