using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Validation;

public class ValidationService
{
    private readonly List<IValidationRule> _rules;

    public ValidationService()
    {
        _rules =
        [
            new ReferenceIntegrityRule(),
            new RequiredFieldRule(),
            new ValueRangeRule()
        ];
    }

    public ValidationReport Validate(IReadOnlyList<IEntity> entities)
    {
        var report = new ValidationReport();
        foreach (var rule in _rules)
            rule.Validate(entities, report);
        return report;
    }

    public string FormatReport(ValidationReport report)
    {
        var sb = new StringBuilder();
        if (report.Entries.Count == 0)
        {
            sb.AppendLine("Validation passed — no issues found.");
            return sb.ToString();
        }

        var errors = report.Entries.Where(e => e.Severity == Severity.Error).ToList();
        var warnings = report.Entries.Where(e => e.Severity == Severity.Warning).ToList();

        if (errors.Count > 0)
        {
            sb.AppendLine($"✘ {errors.Count} Error(s):");
            foreach (var e in errors)
                sb.AppendLine($"  [{e.EntityLabel}] {e.Field}: {e.Message}");
            sb.AppendLine();
        }

        if (warnings.Count > 0)
        {
            sb.AppendLine($"⚠ {warnings.Count} Warning(s):");
            var maxShow = 20;
            foreach (var w in warnings.Take(maxShow))
                sb.AppendLine($"  [{w.EntityLabel}] {w.Field}: {w.Message}");
            if (warnings.Count > maxShow)
                sb.AppendLine($"  ... and {warnings.Count - maxShow} more");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns true if save should proceed. Errors always block; warnings can be overridden.
    /// </summary>
    public static bool CanProceed(ValidationReport report, bool ignoreWarnings = true)
    {
        return !report.HasErrors;
    }
}
