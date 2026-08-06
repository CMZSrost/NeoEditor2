using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.Paratranz.Models;

namespace NeoEditor.Plugins.Paratranz.Conversion;

/// <summary>
/// 从游戏实体提取翻译单元（D03 §3.3）：
/// 可翻译列白名单 + 特殊规则（maps.strName 跳过、gamevars 整体跳过），
/// id 定位列取自实体 <see cref="UIDKeyAttribute"/> 的第一个非 EntityId 属性。
/// </summary>
public interface ITranslationExtractor
{
    /// <summary>提取翻译单元（空值列跳过；顺序 = 输入实体顺序 × 属性声明顺序）。</summary>
    IReadOnlyList<TranslationUnit> Extract(IEnumerable<IEntity> entities);
}

public sealed class TranslationExtractor : ITranslationExtractor
{
    /// <summary>可翻译列白名单（对齐 NeoParatranz translation_name）。</summary>
    public static readonly IReadOnlySet<string> TranslatableColumns = new HashSet<string>
    {
        "strName", "strNotes", "strWieldPhrase", "vAttackPhrases",
        "strSuccess", "strFail", "strPopUp",
        "strDesc", "strNamePublic", "strHeadline", "strPropertyName",
        "strDescAlt", "strSecretName", "strType",
    };

    private readonly ITranslationKeyParser _keyParser;

    public TranslationExtractor(ITranslationKeyParser keyParser) => _keyParser = keyParser;

    public IReadOnlyList<TranslationUnit> Extract(IEnumerable<IEntity> entities)
    {
        var result = new List<TranslationUnit>();
        foreach (var entity in entities)
        {
            var type = entity.GetType();
            var tableName = type.GetCustomAttribute<TableAttribute>()?.Name;
            if (tableName is null)
                continue;
            // gamevars 无 id 定位列且列均为变量名/数值，无可翻译显示文本（D03 §3.3 订正，
            // 与旧脚本实际行为一致——旧脚本对 gamevars 生成的 key 亦为坏 key）。
            if (tableName == "gamevars")
                continue;

            var key = ResolveId(type, entity);
            if (key.IdValue is null)
                continue;

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var columnName = prop.GetCustomAttribute<ColumnAttribute>()?.Name;
                if (columnName is null || !TranslatableColumns.Contains(columnName))
                    continue;
                // maps.strName 是图片名，不翻译（对齐旧脚本）
                if (tableName == "maps" && columnName == "strName")
                    continue;
                if (prop.GetValue(entity) is not string value || string.IsNullOrEmpty(value))
                    continue;

                result.Add(new TranslationUnit(
                    _keyParser.BuildKey(tableName, key.IdField, key.IdValue, columnName),
                    value,
                    null,
                    $"{tableName}.{columnName}"));
            }
        }
        return result;
    }

    private static (string IdField, string? IdValue) ResolveId(Type type, IEntity entity)
    {
        var uidKey = type.GetCustomAttribute<UIDKeyAttribute>();
        var propertyName = uidKey?.PropertyNames.FirstOrDefault(n => n != nameof(IEntity.EntityId));
        var prop = propertyName is null
            ? null
            : type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (prop is null)
            return ("", null);
        var idField = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
        var value = prop.GetValue(entity);
        return (idField, value?.ToString());
    }
}
