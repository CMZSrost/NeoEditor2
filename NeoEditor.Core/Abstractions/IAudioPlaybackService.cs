namespace NeoEditor.Core.Abstractions;

/// <summary>
/// R42: plays game sound assets (extracted from NEOScavenger.swf by
/// player-tools/extract-sounds.js into {GameRootDir}/sounds/*.mp3).
/// Cue names (aSounds / strSnd values like "cueRiflePickup") are matched
/// against the extracted asset names — visualizers can offer a play button
/// without depending on the platform audio backend.
/// </summary>
public interface IAudioPlaybackService
{
    /// <summary>True when a sound index exists for the current game root.</summary>
    bool IsAvailable { get; }

    /// <summary>Play the audio asset matching <paramref name="cueName"/> (stops the
    /// previous clip first). No-op when the cue cannot be matched.</summary>
    void Play(string cueName);

    /// <summary>Stop the currently playing clip.</summary>
    void Stop();
}
