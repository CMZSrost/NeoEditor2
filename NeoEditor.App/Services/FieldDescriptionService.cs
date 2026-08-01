using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NeoEditor.Helper;

namespace NeoEditor.Services;

public class FieldDescriptionService
{
    private readonly ILogger<FieldDescriptionService> _logger;
    private Dictionary<string, string> _descriptions = new(StringComparer.OrdinalIgnoreCase);

    public FieldDescriptionService(ILogger<FieldDescriptionService> logger)
    {
        _logger = logger;
    }

    /// <summary>Load descriptions from a JSON file.</summary>
    public void LoadFromJson(string jsonPath)
    {
        if (!File.Exists(jsonPath)) return;
        try
        {
            var json = File.ReadAllText(jsonPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict is not null)
            {
                _descriptions = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
                _logger.LogInformation("Loaded {Count} field descriptions from {Path}", _descriptions.Count, jsonPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load field descriptions from {Path}", jsonPath);
        }
    }

    /// <summary>Extract field descriptions from the .docx file and save as JSON.</summary>
    public void ExtractFromDocx(string docxPath, string jsonOutputPath)
    {
        if (!File.Exists(docxPath))
        {
            _logger.LogWarning("Field descriptions .docx not found at {Path}", docxPath);
            return;
        }

        try
        {
            var text = DocxTextExtractor.ExtractText(docxPath);
            var dict = DocxTextExtractor.ParseFieldDescriptions(text);

            if (dict.Count > 0)
            {
                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                var dir = Path.GetDirectoryName(jsonOutputPath);
                if (dir is not null) Directory.CreateDirectory(dir);
                File.WriteAllText(jsonOutputPath, json);
                _descriptions = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
                _logger.LogInformation("Extracted {Count} field descriptions to {Path}", dict.Count, jsonOutputPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract field descriptions from {Path}", docxPath);
        }
    }

    /// <summary>Get field description by table name and column name.</summary>
    public string? GetDescription(string tableName, string columnName)
    {
        var key = $"{tableName}.{columnName}".ToLowerInvariant();
        return _descriptions.TryGetValue(key, out var desc) ? desc : null;
    }

    /// <summary>Get field description by table name and column name.</summary>
    public string? GetDescription(string tableName, string columnName, string? commentAttribute)
    {
        // Prefer the .docx description over the Comment attribute
        var docxDesc = GetDescription(tableName, columnName);
        if (!string.IsNullOrWhiteSpace(docxDesc)) return docxDesc;
        return commentAttribute;
    }
}
