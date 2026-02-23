using System;

namespace NeoEditor.Data;

public class DataImportException : Exception
{
    public DataImportException(string message, string filePath = null, string tableName = null,
        Exception innerException = null)
        : base(message, innerException)
    {
        FilePath = filePath;
        TableName = tableName;
    }

    public string FilePath { get; }
    public string TableName { get; }
}

public class XmlParseException : Exception
{
    public XmlParseException(string message, string filePath = null, int? lineNumber = null,
        Exception innerException = null)
        : base(message, innerException)
    {
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    public string FilePath { get; }
    public int? LineNumber { get; }
}

public class ModLoadException : Exception
{
    public ModLoadException(string message, string modPath = null, Exception innerException = null)
        : base(message, innerException)
    {
        ModPath = modPath;
    }

    public string ModPath { get; }
}

public class EditorException : Exception
{
    public EditorException(string message, string operation = null, Exception innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
    }

    public string Operation { get; }
}