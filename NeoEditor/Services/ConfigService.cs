using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using NeoEditor.ViewModels;
using Newtonsoft.Json;

namespace NeoEditor.Services;

public interface IConfigService
{
    AppConfig Config { get; }
    Task LoadAsync();
    Task SaveAsync();
}

public class ConfigService : IConfigService
{
    public AppConfig Config { get; private set; }

    public async Task LoadAsync()
    {
        if (Design.IsDesignMode)
        {
            Config = new AppConfig()
            {
                GameRootDir = "D:\\software\\Steam\\steamapps\\common\\Neo Scavenger"
            };
        }
        else if (!File.Exists("config.json"))
        {
            Config = new AppConfig();
            await SaveAsync();
        }
        else
        {
            var json = await File.ReadAllTextAsync("config.json");
            Config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
        }
    }

    public async Task SaveAsync()
    {
        if (Design.IsDesignMode)
        {
            var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
            Serilog.Log.Logger.Debug("Save config: {Config}", json);
        }
        else
        {
            var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
            await File.WriteAllTextAsync("config.json", json);
        }
    }
}