using System.Collections.Generic;

namespace NeoEditor.Data.Validation;

public enum Severity { Error, Warning }

public record ValidationEntry(Severity Severity, string EntityLabel, string Field, string Message);

public class ValidationReport
{
    public List<ValidationEntry> Entries { get; } = [];
    public bool HasErrors => Entries.Exists(e => e.Severity == Severity.Error);
    public int ErrorCount => Entries.FindAll(e => e.Severity == Severity.Error).Count;
    public int WarningCount => Entries.FindAll(e => e.Severity == Severity.Warning).Count;

    public void Error(string entityLabel, string field, string message)
        => Entries.Add(new ValidationEntry(Severity.Error, entityLabel, field, message));

    public void Warning(string entityLabel, string field, string message)
        => Entries.Add(new ValidationEntry(Severity.Warning, entityLabel, field, message));
}
