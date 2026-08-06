using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Data.Context;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.Paratranz.Conversion;
using NeoEditor.Plugins.Paratranz.Models;
using NeoEditor.Plugins.Paratranz.Services;

namespace NeoEditor.Plugins.Paratranz.ViewModels;

/// <summary>Mod 下拉选项。</summary>
public sealed class ModChoice(int modId, string name)
{
    public int ModId { get; } = modId;
    public string Name { get; } = name;
    public override string ToString() => $"{Name} (#{ModId})";
}

/// <summary>ParaTranz 文件行（进度 + 状态）。</summary>
public partial class ParatranzFileRow : ObservableObject
{
    public int FileId { get; }
    public string Name { get; }
    public int Total { get; }
    public int Translated { get; }

    public double Progress => Total > 0 ? (double)Translated / Total : 0;
    public string ProgressText => $"{Translated}/{Total}";
    public bool HasTranslation => Total > 0;

    [ObservableProperty]
    public partial bool IsApplying { get; set; }

    public ParatranzFileRow(ParatranzFile file)
    {
        FileId = file.Id ?? 0;
        Name = file.Name ?? "(unknown)";
        Total = file.Total ?? 0;
        Translated = file.Translated ?? 0;
    }
}

/// <summary>
/// ParaTranz Dock 工具面板（D03 §6.2 双 Tab）：
/// Tab 1「同步」——mod 选择、项目概览、文件列表（进度）、上传原文 / 拉取应用；
/// Tab 2「翻译工作台」——NativeWebView 嵌入 paratranz.cn 项目页（视图层负责）。
/// diff 预览经 <see cref="DiffPreviewRequested"/> 事件交给视图弹窗，确认后回调
/// <see cref="ExecuteBuildAsync"/>（R24 命令通路，可 Undo）。
/// </summary>
public partial class ParatranzPaneViewModel : ObservableObject
{
    private readonly IParatranzSyncService _sync;
    private readonly IConfigService _config;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly ILogger<ParatranzPaneViewModel> _logger;

    /// <summary>本地化服务（axaml 的 Loc[...] 绑定）。</summary>
    public ILocalizationService Loc { get; }

    /// <summary>通知横幅服务。</summary>
    public INotificationService Notification { get; }

    public AppConfig Config => _config.Config;

    /// <summary>请求打开 diff 预览弹窗（视图订阅；确认后调 ExecuteBuildAsync）。</summary>
    public event Action<TranslationBuildResult, ParatranzFileRow>? DiffPreviewRequested;

    public ObservableCollection<ModChoice> ModChoices { get; } = [];
    public ObservableCollection<ParatranzFileRow> Files { get; } = [];

    [ObservableProperty]
    public partial ModChoice? SelectedMod { get; set; }

    partial void OnSelectedModChanged(ModChoice? value)
    {
        if (value is not null)
            _ = RefreshFilesAsync();
    }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    /// <summary>网页工作台 URL（设置页项目；未配置时为空 → 视图显示提示）。</summary>
    public string WorkbenchUrl => Config.ParatranzProjectId > 0
        ? $"https://paratranz.cn/projects/{Config.ParatranzProjectId}"
        : "";

    [ObservableProperty]
    public partial string ProjectSummary { get; set; } = "";

    public ParatranzPaneViewModel(
        IParatranzSyncService sync,
        IConfigService config,
        IDbContextFactory<EditorDbContext> editorDbFactory,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ILogger<ParatranzPaneViewModel> logger)
    {
        _sync = sync;
        _config = config;
        _editorDbFactory = editorDbFactory;
        Loc = localizationService;
        Notification = notificationService;
        _logger = logger;
    }

    /// <summary>刷新：mod 列表 + 项目概览 + 文件列表（面板打开 / 工具栏刷新）。</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await LoadModsAsync();
            await RefreshProjectAsync();
            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"刷新失败: {ex.Message}";
            _logger.LogWarning(ex, "Paratranz refresh failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadModsAsync()
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var mods = await db.ModInfos
            .OrderBy(m => m.ModId)
            .Select(m => new { m.ModId, m.Name })
            .ToListAsync();
        ModChoices.Clear();
        foreach (var mod in mods)
            ModChoices.Add(new ModChoice(mod.ModId, mod.Name));
        if (SelectedMod is null && ModChoices.Count > 0)
            SelectedMod = ModChoices[0];
    }

    private async Task RefreshProjectAsync()
    {
        ProjectSummary = "";
        OnPropertyChanged(nameof(WorkbenchUrl));
        if (Config.ParatranzProjectId <= 0)
        {
            StatusText = "未配置 ParaTranz 项目：请在 设置 → ParaTranz 中测试连接并选择项目。";
            return;
        }
        try
        {
            var project = await _sync.GetFilesAsync(Config.ParatranzProjectId);
            var total = project.Sum(f => f.Total ?? 0);
            var translated = project.Sum(f => f.Translated ?? 0);
            ProjectSummary = $"共 {project.Count} 个文件 · {translated}/{total} 词条已翻译";
            OnPropertyChanged(nameof(ProjectSummary));
        }
        catch (Exception ex)
        {
            StatusText = $"项目加载失败: {ex.Message}";
            _logger.LogWarning(ex, "Paratranz project load failed");
        }
    }

    private async Task RefreshFilesAsync()
    {
        if (Config.ParatranzProjectId <= 0 || SelectedMod is null)
            return;
        try
        {
            var files = await _sync.GetFilesAsync(Config.ParatranzProjectId);
            Files.Clear();
            foreach (var file in files.OrderBy(f => f.Name))
                Files.Add(new ParatranzFileRow(file));
            StatusText = $"已加载 {Files.Count} 个翻译文件";
        }
        catch (Exception ex)
        {
            StatusText = $"文件列表加载失败: {ex.Message}";
            _logger.LogWarning(ex, "Paratranz files load failed");
        }
    }

    /// <summary>上传当前 mod 的原文（提取 → 创建/更新）。</summary>
    [RelayCommand]
    private async Task UploadOriginalsAsync()
    {
        if (Config.ParatranzProjectId <= 0 || SelectedMod is null)
        {
            StatusText = "请先选择项目与目标 Mod。";
            return;
        }
        IsBusy = true;
        try
        {
            var summary = await _sync.UploadOriginalsAsync(
                Config.ParatranzProjectId, SelectedMod.ModId, SelectedMod.Name, Config.GameRootDir);
            if (summary.TotalUnits == 0)
            {
                StatusText = "该 Mod 没有可翻译文本（无可翻译列或全部为空）。";
                return;
            }
            var parts = summary.Files
                .GroupBy(f => f.Action)
                .Select(g => $"{g.Count()} {g.Key.ToLower()}");
            StatusText = $"上传完成：{summary.TotalUnits} 词条（{string.Join("，", parts)}）。刷新文件列表…";
            Notification.ShowSuccess(StatusText, "ParaTranz");
            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"上传失败: {ex.Message}";
            Notification.ShowError(StatusText, "ParaTranz");
            _logger.LogWarning(ex, "Paratranz upload failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>拉取译文并打开 diff 预览（确认后经 ExecuteBuildAsync 应用）。</summary>
    [RelayCommand]
    private async Task ApplyFileAsync(ParatranzFileRow row)
    {
        if (row is null || Config.ParatranzProjectId <= 0 || SelectedMod is null)
            return;
        if (row.IsApplying)
            return;
        row.IsApplying = true;
        try
        {
            var build = await _sync.PrepareApplyAsync(
                Config.ParatranzProjectId, row.FileId, SelectedMod.ModId, Config.GameRootDir);
            if (build.Commands.Count == 0)
            {
                StatusText = $"「{row.Name}」无可用译文（跳过 {build.Stats.Skipped}，未变化 {build.Stats.Unchanged}）。";
                return;
            }
            DiffPreviewRequested?.Invoke(build, row);
        }
        catch (Exception ex)
        {
            StatusText = $"拉取失败: {ex.Message}";
            Notification.ShowError(StatusText, "ParaTranz");
            _logger.LogWarning(ex, "Paratranz apply prepare failed");
        }
        finally
        {
            row.IsApplying = false;
        }
    }

    /// <summary>执行 diff 预览确认后的命令（视图回调；R24 可 Undo）。</summary>
    public async Task ExecuteBuildAsync(TranslationBuildResult build, ParatranzFileRow row)
    {
        try
        {
            var result = await _sync.ExecuteBuildAsync(build);
            if (result.Executed)
            {
                StatusText = $"已应用 {result.Stats.Applied} 条译文（可撤销）；跳过 {result.Stats.Skipped}，未变化 {result.Stats.Unchanged}。";
                Notification.ShowSuccess(StatusText, "ParaTranz");
            }
            else
            {
                StatusText = "没有需要应用的译文。";
            }
            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"应用失败: {ex.Message}";
            Notification.ShowError(StatusText, "ParaTranz");
            _logger.LogWarning(ex, "Paratranz apply execute failed");
        }
    }

    /// <summary>在系统浏览器打开项目页。</summary>
    [RelayCommand]
    private void OpenInBrowser()
    {
        if (string.IsNullOrEmpty(WorkbenchUrl))
            return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WorkbenchUrl)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open browser for {Url}", WorkbenchUrl);
        }
    }
}
