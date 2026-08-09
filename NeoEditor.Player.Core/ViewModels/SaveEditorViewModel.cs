using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NeoEditor.Player.Core.ViewModels;

/// <summary>
/// R47 存档修改工具（节点编辑器版）：加载 localStorage 里的 LSO 存档 → 解析为
/// JSON 树（LsoExpand.toTree）→ 构建 SaveNode 节点树（容器只读结构、标量可编辑）
/// → 保存回写（LsoExpand.fromTree，编码后回验 parseLso）。游戏运行期持有 Ruffle
/// SharedObject 内存缓存，「保存并加载」= 保存 + 重载页面后生效。宿主注入 JS
/// 执行器 / 本地化 / 重启回调，VM 保持平台无关可单测。
/// </summary>
public sealed partial class SaveEditorViewModel : ObservableObject
{
    private readonly Func<string, Task<string?>> _executeJs;
    private readonly Func<string, string> _localize;
    private readonly Action? _restartGame;

    public ObservableCollection<SaveEntry> Saves { get; } = [];

    /// <summary>存档 JSON 树（根 = body 条目，每项一个节点）。</summary>
    public ObservableCollection<SaveNode> RootNodes { get; } = [];

    [ObservableProperty] private SaveEntry? _selectedSave;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _windowTitle = "存档修改工具";

    private string _treeName = "";
    private int _treeFormatVersion = 3;

    /// <summary>「保存并加载」完成（窗口关闭并重启游戏）。</summary>
    public event Action? SavedAndLoaded;

    public SaveEditorViewModel(Func<string, Task<string?>> executeJs, Func<string, string> localize,
        Action? restartGame = null)
    {
        _executeJs = executeJs;
        _localize = localize;
        _restartGame = restartGame;
    }

    /// <summary>列出 localStorage 存档（与存档管理器同一过滤：当前页面路径 + 非 test）。</summary>
    [RelayCommand]
    public async Task Refresh()
    {
        Saves.Clear();
        var json = await _executeJs(
            "Object.keys(localStorage).filter(function (k) {" +
            "  var v = localStorage[k];" +
            "  if (typeof v !== 'string' || v.length === 0) return false;" +
            "  var path = location.pathname;" +
            "  if (path && path !== '/' && k.indexOf(path) < 0) return false;" +
            "  var name = (k.split('/').pop() || '').toLowerCase();" +
            "  return name.indexOf('test') < 0;" +
            "}).map(function (k) { return { k: k, s: localStorage[k].length }; })");
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var items = DeserializeItems(json);
            if (items is null) return;
            foreach (var item in items)
                Saves.Add(new SaveEntry(item.K, item.S));
            StatusText = Saves.Count == 0 ? _localize("SaveEditor.Empty") : "";
        }
        catch (JsonException)
        {
            StatusText = _localize("Storage.ParseFailed");
        }
    }

    /// <summary>加载选中存档 → 节点树 + 结构摘要。</summary>
    [RelayCommand]
    public async Task Load()
    {
        if (SelectedSave is null)
        {
            StatusText = _localize("SaveEditor.NoSelection");
            return;
        }
        var json = await _executeJs(
            "(function () {" +
            "  try { return LsoExpand.toTree(localStorage.getItem(" + JsonSerializer.Serialize(SelectedSave.Key) + ") || ''); }" +
            "  catch (e) { return { error: String(e && e.message ? e.message : e) }; }" +
            "})()");
        var tree = DeserializeTree(json);
        if (tree is null)
        {
            StatusText = _localize("Storage.ParseFailed");
            return;
        }
        if (tree.Error is not null)
        {
            StatusText = string.Format(_localize("SaveEditor.LoadFailed"), tree.Error);
            return;
        }

        _treeName = tree.Name ?? "";
        _treeFormatVersion = tree.FormatVersion ?? 3;
        var body = tree.Body ?? [];
        RootNodes.Clear();
        foreach (var item in body)
            RootNodes.Add(SaveTree.Build(item.Value, item.Name));

        SummaryText = string.Format(_localize("SaveEditor.Summary"), _treeName, _treeFormatVersion, body.Length)
            + Environment.NewLine + string.Join(Environment.NewLine,
                RootNodes.Select(n => "  " + n.Name + " → " + n.TypeLabel));
        WindowTitle = $"{_localize("SaveEditor.Title")} — {SelectedSave.Key}";
        StatusText = string.Format(_localize("SaveEditor.Loaded"), $"{SelectedSave.Length / 1024.0:F1}");
    }

    /// <summary>R46: 预载指定存档（存档管理「修改」按钮入口）。</summary>
    public async Task LoadEntryAsync(SaveEntry entry)
    {
        SelectedSave = entry;
        await LoadCommand.ExecuteAsync(null);
    }

    /// <summary>保存（写回 SelectedSave 的 key）。</summary>
    [RelayCommand]
    public async Task Save()
    {
        if (SelectedSave is null)
        {
            StatusText = _localize("SaveEditor.NoSelection");
            return;
        }
        await SaveToAsync(SelectedSave.Key);
    }

    /// <summary>另存为：写回指定 key（窗口先弹输入框）。</summary>
    public async Task SaveAsAsync(string newKey)
    {
        if (string.IsNullOrWhiteSpace(newKey)) return;
        await SaveToAsync(newKey.Trim());
    }

    /// <summary>保存并加载：写回 + 重载页面（Ruffle 内存缓存清掉后新档生效）+ 关窗口。</summary>
    [RelayCommand]
    public async Task SaveAndLoad()
    {
        if (SelectedSave is null)
        {
            StatusText = _localize("SaveEditor.NoSelection");
            return;
        }
        var ok = await SaveToAsync(SelectedSave.Key);
        if (!ok) return;
        await Task.Delay(300);   // 让 JS 回写落定再重载
        _restartGame?.Invoke();
        SavedAndLoaded?.Invoke();
    }

    /// <summary>核心保存：节点树 → toTree 结构 JSON → fromTree → setItem(key, b64)。</summary>
    private async Task<bool> SaveToAsync(string key)
    {
        if (RootNodes.Count == 0)
        {
            StatusText = _localize("SaveEditor.EmptyJson");
            return false;
        }
        string jsonText;
        try
        {
            jsonText = SerializeTree();
        }
        catch (SaveNodeException ex)
        {
            StatusText = string.Format(_localize("SaveEditor.SaveFailed"), ex.Message);
            return false;
        }

        var json = await _executeJs(
            "(function () {" +
            "  var r = LsoExpand.fromTree(" + JsonSerializer.Serialize(jsonText) + ");" +
            "  if (r.error) return r;" +
            // v2.72: 存档修改器保存 = 明确意图让该存档存在 → 解除可能存在的墓碑/恢复保护
            "  window.__unmarkKey && window.__unmarkKey(" + JsonSerializer.Serialize(key) + ");" +
            "  localStorage.setItem(" + JsonSerializer.Serialize(key) + ", r.b64);" +
            "  return { ok: true, len: r.b64.length };" +
            "})()");
        var result = DeserializeSaveResult(json);
        if (result is null)
        {
            StatusText = _localize("Storage.ParseFailed");
            return false;
        }
        if (result.Ok != true)
        {
            StatusText = string.Format(_localize("SaveEditor.SaveFailed"), result.Error ?? "?");
            return false;
        }

        await Refresh();   // 更新列表大小（Refresh 会重置状态行，故成功提示放在其后）
        StatusText = string.Format(_localize("SaveEditor.Saved"), result.Len ?? 0);
        return true;
    }

    /// <summary>节点树 → toTree 结构 JSON 文本（{name, formatVersion, body}）。</summary>
    private string SerializeTree()
    {
        var body = new JsonArray(RootNodes.Select(n => (JsonNode?)new JsonObject
        {
            ["name"] = n.Name,
            ["value"] = SaveTree.SerializeValue(n),
        }).ToArray());
        var root = new JsonObject
        {
            ["name"] = _treeName,
            ["formatVersion"] = _treeFormatVersion,
            ["body"] = body,
        };
        return root.ToJsonString();
    }

    // ── 容错反序列化（ExecuteScript 返回可能双重编码）──

    private static SaveItem[]? DeserializeItems(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SaveItem[]>(json);
        }
        catch (JsonException)
        {
            var inner = JsonSerializer.Deserialize<string>(json);
            return inner is null ? null : JsonSerializer.Deserialize<SaveItem[]>(inner);
        }
    }

    private static TreeResult? DeserializeTree(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<TreeResult>(json);
        }
        catch (JsonException)
        {
            var inner = JsonSerializer.Deserialize<string>(json);
            return inner is null ? null : JsonSerializer.Deserialize<TreeResult>(inner);
        }
    }

    private static SaveResult? DeserializeSaveResult(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<SaveResult>(json);
        }
        catch (JsonException)
        {
            var inner = JsonSerializer.Deserialize<string>(json);
            return inner is null ? null : JsonSerializer.Deserialize<SaveResult>(inner);
        }
    }

    private sealed class SaveItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("k")]
        public string K { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("s")]
        public int S { get; set; }
    }

    private sealed class TreeResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string? Error { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("formatVersion")]
        public int? FormatVersion { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public BodyItem[]? Body { get; set; }
    }

    private sealed class BodyItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("value")]
        public JsonElement Value { get; set; }
    }

    private sealed class SaveResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("ok")]
        public bool? Ok { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string? Error { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("len")]
        public int? Len { get; set; }
    }
}
