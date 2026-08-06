using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.Dialog;

public partial class CreateModDialogViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Author { get; set; } = "";
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Namespace { get; set; } = "";
    [ObservableProperty] public partial bool CreateProfile { get; set; } = true;

    public EventHandler? CloseRequested;
    private readonly IModManager _modManager;
    private readonly IProfileManager _profileManager;
    private readonly IConfigService _config;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;

    public CreateModDialogViewModel(IModManager modManager, IProfileManager profileManager, IConfigService config,
        IDbContextFactory<EditorDbContext> editorDbFactory,
        INotificationService notificationService,
        ILocalizationService localizationService)
        : base(localizationService, notificationService, null)
    {
        _modManager = modManager;
        _profileManager = profileManager;
        _config = config;
        _editorDbFactory = editorDbFactory;
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Author))
        {
            Notification.ShowWarning("Author and Mod Name are required.");
            return;
        }

        var gameRoot = _config.Config.GameRootDir;
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            Notification.ShowWarning("Game root directory not set. Configure it in Settings first.");
            return;
        }

        await _modManager.CreateModAsync(Name, Author);

        if (CreateProfile)
        {
            // Find the just-created mod
            await using var db = await _editorDbFactory
                .CreateDbContextAsync();
            var modInfo = await db.ModInfos
                .Where(m => m.Name == Name && !m.IsBase)
                .OrderByDescending(m => m.ModId)
                .FirstOrDefaultAsync();

            if (modInfo is not null)
            {
                var ns = string.IsNullOrWhiteSpace(Namespace) ? "0" : Namespace;
                var phpContent = $"nRows=1\n&strModName0={ns}\n&strModURL0={modInfo.Path}\n";

                var profile = _profileManager.CreateProfile(
                    $"{Name} Loadout",
                    $"Auto-created profile for {Name}",
                    null);

                // Update profile content with the mod reference
                await using var db2 = await _editorDbFactory
                    .CreateDbContextAsync();
                var savedProfile = await db2.ProfileInfos
                    .OrderByDescending(p => p.ProfileId)
                    .FirstOrDefaultAsync(p => p.Name == profile.Name);
                if (savedProfile is not null)
                {
                    savedProfile.Content = phpContent;
                    await db2.SaveChangesAsync();
                    Messenger.Send(new OpenMergeEditorMessage(savedProfile));
                }
            }
        }

        Messenger.Send(new RefreshModMessage());
        Notification.ShowSuccess($"Mod '{Name}' created!");
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
