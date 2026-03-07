using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Data;

public static class Constants
{
    #region ConfigManager

    public static string AppSettingsSection = "appSettings";

    public static string ProjectSettingsGameRootDir = "ProjectSettings:GameRootDir";

    #endregion

    #region Editor

    public static string EditorProjectFolder = "Projects";
    public static string EditorModDataFolder = "ModData";
    public static string EditorTempFolder = "Temp";
    public static string EditorDatabasePath = "editor.db";

    #endregion

    #region Game

    public static string GameDatabasePath = "game.db";

    public static IDictionary<string, Type> GameTypes = typeof(IEntity).Assembly.GetTypes()
        .Where(type => type.IsClass
                       && !type.IsAbstract
                       && type != typeof(IEntity)
                       && type.Namespace == typeof(IEntity).Namespace
                       && typeof(IEntity).IsAssignableFrom(type)
                       && type.GetCustomAttribute<TableAttribute>() is not null)
        .ToDictionary(
            type => type.Name,
            type => type
        );

    public static IDictionary<string, string[]> GameTables = typeof(IEntity).Assembly
        .GetTypes()
        .Where(type => type.IsClass
                       && !type.IsAbstract
                       && type != typeof(IEntity)
                       && type.Namespace == typeof(IEntity).Namespace
                       && typeof(IEntity).IsAssignableFrom(type)
                       && type.GetCustomAttribute<TableAttribute>() is not null)
        .OrderBy(type => type.Name)
        .ToDictionary(
            ReflectionHelper.GetPropertyTableName,
            type => ReflectionHelper.GetPropertyColumnName(type).ToArray());

    #endregion
}