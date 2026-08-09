using System.Collections.Generic;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>D09 §五: the JS→C# interaction bridge — one protocol, one handler.</summary>
public class VizActionHandlerTests
{
    private static VizActionHandler CreateHandler(out StubNavigationRouter router,
        out StubSelectionService selection, StubEntityLookupService? lookup = null)
    {
        router = new StubNavigationRouter();
        selection = new StubSelectionService();
        return new VizActionHandler(router, selection, lookup ?? new StubEntityLookupService());
    }

    // ── TryParse ─────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_ValidJson()
    {
        var handler = CreateHandler(out _, out _);
        Assert.True(handler.TryParse(
            """{"kind":"navigate","entityType":"Encounter","entityId":"941","modifier":"ctrl"}""",
            out var action));
        Assert.Equal("navigate", action.Kind);
        Assert.Equal("Encounter", action.EntityType);
        Assert.Equal("941", action.EntityId);
        Assert.Equal("ctrl", action.Modifier);
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalse()
    {
        var handler = CreateHandler(out _, out _);
        Assert.False(handler.TryParse("not json", out _));
        Assert.False(handler.TryParse("""{"kind":""}""", out _));
    }

    // ── navigate（Ctrl+LMB 跳转）────────────────────────────────────────

    [Fact]
    public void Handle_Navigate_RoutesToRouter()
    {
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var handler = CreateHandler(out var router, out _, lookup);

        var error = handler.Handle(new VizActionHandler.VizAction("navigate", "Encounter", "941", "ctrl"));

        Assert.Null(error);
        var nav = Assert.Single(router.Navigated);
        Assert.Equal(typeof(Encounter), nav.Type);
        Assert.Equal("941", nav.Id);
    }

    // ── peek（Ctrl+RMB 预览）────────────────────────────────────────────

    [Fact]
    public void Handle_Peek_RoutesToRouter()
    {
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var handler = CreateHandler(out var router, out _, lookup);

        Assert.Null(handler.Handle(new VizActionHandler.VizAction("peek", "Encounter", "12", "ctrl")));
        var peek = Assert.Single(router.Peeked);
        Assert.Equal(typeof(Encounter), peek.Type);
        Assert.Equal("12", peek.Id);
    }

    // ── select（R12 选中同步）────────────────────────────────────────────

    [Fact]
    public void Handle_Select_SetsCurrentEntity()
    {
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "Sel", Type = EncounterType.Normal };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object> { enc } } };
        var handler = CreateHandler(out _, out var selection, lookup);

        Assert.Null(handler.Handle(new VizActionHandler.VizAction("select", "Encounter", "90", "")));
        Assert.Same(enc, selection.Current);
    }

    // ── 参数校验 / 白名单 ────────────────────────────────────────────────

    [Fact]
    public void Handle_UnknownKind_ReturnsError()
    {
        var handler = CreateHandler(out _, out _);
        Assert.NotNull(handler.Handle(new VizActionHandler.VizAction("destroy", "Encounter", "90", "")));
    }

    [Fact]
    public void Handle_UnknownType_ReturnsError()
    {
        var handler = CreateHandler(out _, out _);
        Assert.NotNull(handler.Handle(new VizActionHandler.VizAction("navigate", "NoSuchType", "1", "")));
    }

    [Fact]
    public void Handle_MissingId_ReturnsError()
    {
        var handler = CreateHandler(out _, out _);
        Assert.NotNull(handler.Handle(new VizActionHandler.VizAction("navigate", "Encounter", null, "")));
    }
}
