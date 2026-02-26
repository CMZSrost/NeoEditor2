using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.ExplorerPane;

public class SettingsPaneViewModel : ViewModelBase
{
    // 设置选项
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;
    private readonly ILogger<ResourceManagerViewModel> _logger;
    private readonly LocalizationService _localizationService;

    public SettingsPaneViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ILogger<ResourceManagerViewModel>>(),
        App.ServiceProvider!.GetRequiredService<LocalizationService>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>())
    {
    }

    public SettingsPaneViewModel(ILogger<ResourceManagerViewModel> logger, LocalizationService localizationService,
        IConfigService configService)
    {
        _config = configService;
        _logger = logger;
        _localizationService = localizationService;
    }
}