using System.Threading.Tasks;
using NeoEditor.Core.Model;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;

namespace NeoEditor.Services;

/// <summary>
/// Docs/41 P3: one-shot, dismissible, resettable onboarding hints (strength-2 guidance).
/// <see cref="TryShow"/> returns true only the first time a hint key is shown; the caller
/// decides how to display it (toast / banner). Users can reset all hints in Settings.
/// </summary>
public interface IOnboardingHintService
{
    /// <summary>Mark the hint as shown and return true if it has never been shown before.</summary>
    bool TryShow(string hintKey);

    /// <summary>Mark a hint as dismissed without showing it (user closed it manually).</summary>
    void Dismiss(string hintKey);

    /// <summary>Re-enable every hint (Settings → "Reset onboarding hints").</summary>
    Task ResetAllAsync();
}

/// <inheritdoc cref="IOnboardingHintService"/>
/// <remarks>State lives in <see cref="AppConfig.DismissedHints"/> (persisted with the config).</remarks>
public class OnboardingHintService : IOnboardingHintService
{
    private readonly IConfigService _config;

    public OnboardingHintService(IConfigService config)
    {
        _config = config;
    }

    public bool TryShow(string hintKey)
    {
        var dismissed = _config.Config.DismissedHints;
        if (dismissed.Contains(hintKey)) return false;
        dismissed.Add(hintKey);
        _ = _config.SaveAsync();
        return true;
    }

    public void Dismiss(string hintKey)
    {
        _config.Config.DismissedHints.Add(hintKey);
        _ = _config.SaveAsync();
    }

    public async Task ResetAllAsync()
    {
        _config.Config.DismissedHints.Clear();
        await _config.SaveAsync();
    }
}
