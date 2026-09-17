using Android.Media;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Android implementation of <see cref="IChatSoundPlayer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="SoundPool"/> rather than <see cref="MediaPlayer"/>: the alert is a short,
/// fire-and-forget sound, and <see cref="SoundPool"/> has no prepare step to stall on (the audio
/// is decoded once, up front) and no per-play object to release — the design that previously
/// crashed the music player by reading a released <see cref="MediaPlayer"/> cannot occur here.
/// </para>
/// <para>
/// Playback is tagged with <c>USAGE_NOTIFICATION</c> so the ding obeys the same ringer/silent mode
/// and volume rules as a system notification instead of blasting out of the media stream.
/// </para>
/// <para>
/// The bundled sound is the same <c>chat-ding.mp3</c> the web client plays, packaged as the
/// Android raw resource <c>raw/chat_ding.mp3</c>.
/// </para>
/// </remarks>
internal sealed class AndroidChatSoundPlayer : IChatSoundPlayer, IDisposable
{
    /// <summary>Raw resource name of the bundled ding (without extension).</summary>
    private const string SoundResourceName = "chat_ding";

    private readonly IAppPreferences _preferences;
    private readonly ILogger<AndroidChatSoundPlayer> _logger;
    private readonly Lock _gate = new();

    private global::Android.Media.SoundPool? _pool;
    private int _soundId;
    private bool _loaded;
    private bool _playWhenLoaded;
    private bool _unavailable;

    /// <summary>Initializes a new <see cref="AndroidChatSoundPlayer"/>.</summary>
    public AndroidChatSoundPlayer(IAppPreferences preferences, ILogger<AndroidChatSoundPlayer> logger)
    {
        _preferences = preferences;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _preferences.Get(ChatSoundSettings.PreferenceKey, ChatSoundSettings.DefaultEnabled);

    /// <inheritdoc />
    public void Prepare()
    {
        if (!IsEnabled)
            return;

        try
        {
            lock (_gate)
            {
                EnsurePoolLocked();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prepare the chat alert sound.");
        }
    }

    /// <inheritdoc />
    public void PlayMessageAlert()
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Chat alert sound is disabled; skipping the ding.");
            return;
        }

        try
        {
            lock (_gate)
            {
                EnsurePoolLocked();

                if (_soundId == 0)
                    return; // Unavailable — already logged when the pool failed to build.

                if (!_loaded)
                {
                    // Still decoding: remember the request so the ding is not lost.
                    _playWhenLoaded = true;
                    return;
                }

                PlayLocked();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to play the chat alert sound.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            try
            {
                _pool?.Release();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Releasing the chat alert SoundPool failed.");
            }

            _pool = null;
            _soundId = 0;
            _loaded = false;
        }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the <see cref="SoundPool"/> and starts loading the ding. Caller must hold
    /// <see cref="_gate"/>.
    /// </summary>
    private void EnsurePoolLocked()
    {
        if (_pool is not null || _unavailable)
            return;

        var context = global::Android.App.Application.Context;
        var resourceId = context.Resources?.GetIdentifier(SoundResourceName, "raw", context.PackageName) ?? 0;
        if (resourceId == 0)
        {
            _unavailable = true;
            _logger.LogWarning(
                "Chat alert sound resource '{Resource}' missing from the package; dings will be silent.",
                SoundResourceName);
            return;
        }

        try
        {
            // The Android AudioAttributes.Builder chain is annotated nullable in the .NET binding
            // but always returns non-null at runtime; apply null-forgiving (same as MainApplication).
            var attributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Notification)!
                .SetContentType(AudioContentType.Sonification)!
                .Build()!;

            var pool = new SoundPool.Builder()
                .SetMaxStreams(2)!
                .SetAudioAttributes(attributes)!
                .Build()!;

            pool.SetOnLoadCompleteListener(new LoadCompleteListener(this));
            _pool = pool;
            _soundId = pool.Load(context, resourceId, 1);
            _logger.LogInformation("Chat alert sound loaded (soundId={SoundId}).", _soundId);
        }
        catch (Exception ex)
        {
            _unavailable = true;
            _logger.LogWarning(ex, "Chat alert sound could not be initialised; dings will be silent.");
        }
    }

    /// <summary>Plays the loaded ding. Caller must hold <see cref="_gate"/>.</summary>
    private void PlayLocked()
    {
        // streamId is 0 when playback could not start (released pool / sample not ready); that is
        // not an error worth surfacing — the next message simply dings again.
        var streamId = _pool?.Play(_soundId, 1.0f, 1.0f, 1, 0, 1.0f) ?? 0;
        _logger.LogDebug("Chat alert sound played (streamId={StreamId}).", streamId);
    }

    /// <summary>Handles the asynchronous completion of the sample load.</summary>
    private void OnSoundLoaded(int sampleId, int status)
    {
        lock (_gate)
        {
            if (sampleId != _soundId)
                return;

            if (status != 0)
            {
                _unavailable = true;
                _logger.LogWarning("Chat alert sound failed to decode (status={Status}); dings will be silent.", status);
                return;
            }

            _loaded = true;

            if (!_playWhenLoaded)
                return;

            _playWhenLoaded = false;
            try
            {
                PlayLocked();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to play the chat alert sound after loading.");
            }
        }
    }

    private sealed class LoadCompleteListener(AndroidChatSoundPlayer owner)
        : Java.Lang.Object, global::Android.Media.SoundPool.IOnLoadCompleteListener
    {
        public void OnLoadComplete(global::Android.Media.SoundPool? soundPool, int sampleId, int status)
            => owner.OnSoundLoaded(sampleId, status);
    }
}
