using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Command;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.Paratranz.Models;

namespace NeoEditor.Plugins.Paratranz.Conversion;

/// <summary>译文应用统计：Total 输入总数 / Applied 有变化将应用 / Unchanged 值相同跳过 / Skipped 无法定位。</summary>
public sealed record TranslationApplyResult(int Total, int Applied, int Unchanged, int Skipped);

/// <summary>命令构建结果：命令列表 + diff 预览行 + 统计（供调用方预览或直接执行）。</summary>
public sealed record TranslationBuildResult(
    IReadOnlyList<IEditorCommand> Commands,
    IReadOnlyList<DiffRow> Rows,
    TranslationApplyResult Stats);

/// <summary>
/// 把译文翻译单元转换为可执行的编辑命令（D03 §3.5）。
/// 纯命令构建——执行由调用方经 <see cref="IHostService.ExecuteBatchAsync"/> 走 R24 通路
/// （可 Undo / 脏跟踪 / 扩展点）。定位实体按翻译键的 (表, id列, id值) 与目标实体集合匹配，
/// 不依赖 EntityId 哈希算法。
/// </summary>
public interface ITranslationApplier
{
    /// <summary>
    /// 构建命令（单个 <see cref="BatchEditCommand"/> 含全部有变化的编辑）。
    /// 无译文 / key 不可解析 / 实体或列未匹配 的单元计入 Skipped；值无变化计入 Unchanged。
    /// </summary>
    TranslationBuildResult BuildCommands(
        IEnumerable<TranslationUnit> units, IEnumerable<IEntity> targetEntities);
}

public sealed class TranslationApplier : ITranslationApplier
{
    private readonly ITranslationKeyParser _keyParser;

    public TranslationApplier(ITranslationKeyParser keyParser) => _keyParser = keyParser;

    public TranslationBuildResult BuildCommands(
        IEnumerable<TranslationUnit> units, IEnumerable<IEntity> targetEntities)
    {
        var edits = new List<EditRecord>();
        var rows = new List<DiffRow>();
        var unchanged = 0;
        var skipped = 0;
        var total = 0;

        var byTable = targetEntities
            .GroupBy(e => e.GetType().GetCustomAttribute<TableAttribute>()?.Name ?? string.Empty)
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var unit in units)
        {
            total++;
            if (!unit.HasTranslation)
            {
                rows.Add(new DiffRow(unit.Key, unit.Original, "", DiffKind.Skipped));
                skipped++;
                continue;
            }
            if (!_keyParser.TryParseKey(unit.Key, out var key) || key is null)
            {
                rows.Add(new DiffRow(unit.Key, unit.Original, unit.Translation, DiffKind.Skipped));
                skipped++;
                continue;
            }
            if (!byTable.TryGetValue(key.Table, out var candidates))
            {
                rows.Add(new DiffRow(unit.Key, unit.Original, unit.Translation, DiffKind.Skipped));
                skipped++;
                continue;
            }

            var entity = candidates.FirstOrDefault(e => KeyValueMatches(e, key.IdField, key.Id));
            if (entity is null)
            {
                rows.Add(new DiffRow(unit.Key, unit.Original, unit.Translation, DiffKind.Skipped));
                skipped++;
                continue;
            }

            var prop = entity.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => p.GetCustomAttribute<ColumnAttribute>()?.Name == key.Column);
            if (prop is null || prop.GetValue(entity) is not string oldValue)
            {
                rows.Add(new DiffRow(unit.Key, unit.Original, unit.Translation, DiffKind.Skipped));
                skipped++;
                continue;
            }

            if (string.Equals(oldValue, unit.Translation, StringComparison.Ordinal))
            {
                rows.Add(new DiffRow(unit.Key, oldValue, unit.Translation, DiffKind.Unchanged));
                unchanged++;
                continue;
            }
            edits.Add(new EditRecord(entity, prop, key.Column, oldValue, unit.Translation));
            rows.Add(new DiffRow(unit.Key, oldValue, unit.Translation,
                string.IsNullOrEmpty(oldValue) ? DiffKind.Added : DiffKind.Modified));
        }

        var commands = edits.Count > 0
            ? (IReadOnlyList<IEditorCommand>)new IEditorCommand[] { new BatchEditCommand(edits, () => { }) }
            : [];
        return new TranslationBuildResult(commands, rows,
            new TranslationApplyResult(total, edits.Count, unchanged, skipped));
    }

    private static bool KeyValueMatches(IEntity entity, string idField, string id)
    {
        var prop = entity.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(p => p.GetCustomAttribute<ColumnAttribute>()?.Name == idField);
        return prop is not null &&
               string.Equals(prop.GetValue(entity)?.ToString(), id, StringComparison.Ordinal);
    }
}
