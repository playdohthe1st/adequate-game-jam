using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum AudioChannelType { Music, SFX, Voice }

[System.Serializable]
public class FloorMusicEntry
{
    public int floorIndex;
    public AudioClip music;
}

public class AudioManager : MonoBehaviour
{
    #region Singleton

    public static AudioManager Instance { get; private set; }
    public AudioMixerGroup SFXMixerGroup => sfxMixerGroup;

    #endregion

    #region Serialized Fields

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private AudioMixerGroup voMixerGroup;
    [Header("Default Settings")]
    [SerializeField][Range(0f, 1f)] private float defaultMasterVolume = 0.8f;
    [SerializeField][Range(0f, 1f)] private float defaultMusicVolume = 0.8f;
    [SerializeField][Range(0f, 1f)] private float defaultSFXVolume = 0.8f;
    [SerializeField][Range(0f, 1f)] private float defaultVOVolume = 1f;

    [Header("Default Gameplay Music")]
    [Tooltip("Music that plays by default when entering gameplay scenes (can be changed via dialogue nodes)")]
    [SerializeField] private AudioClip defaultGameplayMusic;
    [Tooltip("Music that plays on non-gameplay scenes (e.g. main menu)")]
    [SerializeField] private AudioClip mainMenuMusic;
    [Tooltip("Scene names that should NOT trigger default gameplay music (e.g. MainMenu)")]
    [SerializeField] private string[] nonGameplayScenes = { "MainMenu" };

    [Header("Floor Music")]
    [Tooltip("Per-floor music clips. floorIndex should match whatever floor ID you pass to PlayMusicForFloor.")]
    [SerializeField] private FloorMusicEntry[] floorMusicEntries;
    [Tooltip("Default crossfade duration when transitioning between floor tracks.")]
    [SerializeField] private float floorMusicFadeTime = 1f;

    #endregion

    #region Audio Channels

    private AudioChannel musicChannel;
    private AudioChannel sfxChannel;
    private AudioChannel voiceChannel;
    // Tracked separately so we can cancel a pending delayed VO if a new one is requested
    private Coroutine voDelayCoroutine;
    private int currentFloorIndex = -1;

    #endregion

    #region Constants

    // These strings must match the exposed parameter names in the AudioMixer asset
    private const string MASTER_VOLUME_PARAM = "MasterVolume";
    private const string MUSIC_VOLUME_PARAM = "MusicVolume";
    private const string SFX_VOLUME_PARAM = "SFXVolume";
    private const string VO_VOLUME_PARAM = "VOVolume";

    #endregion

    #region Events

    public event Action<AudioClip> OnMusicStarted;
    public event Action OnMusicStopped;
    public event Action<AudioClip> OnSFXPlayed;
    public event Action<AudioClip> OnVOStarted;
    public event Action OnVOStopped;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton pattern - only one AudioManager should exist at a time
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Keeps this object alive when loading new scenes so music continues uninterrupted
        DontDestroyOnLoad(gameObject);

        // Ensure there's always an AudioListener in the scene to actually hear the audio
        if (FindFirstObjectByType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }

        InitializeChannels();
        InitializeVolumes();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsGameplayScene(scene.name))
        {
            // When entering a gameplay scene, start the default music if nothing is already playing.
            // Floor-specific music will override this once the player enters a floor trigger.
            if (defaultGameplayMusic != null && (!IsMusicPlaying() || musicChannel.CurrentClip != defaultGameplayMusic))
            {
                currentFloorIndex = -1;
                PlayMusic(defaultGameplayMusic, loop: true, fadeTime: 1f);
            }
        }
        else
        {
            if (mainMenuMusic != null && (!IsMusicPlaying() || musicChannel.CurrentClip != mainMenuMusic))
                PlayMusic(mainMenuMusic, loop: true, fadeTime: 1f);
        }
    }

    // Returns false if sceneName matches any entry in the nonGameplayScenes list (case-insensitive)
    private bool IsGameplayScene(string sceneName)
    {
        if (nonGameplayScenes == null) return true;

        foreach (var nonGameplayScene in nonGameplayScenes)
        {
            if (sceneName.Equals(nonGameplayScene, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    #endregion

    #region Initialization

    private void InitializeChannels()
    {
        if (musicMixerGroup == null || sfxMixerGroup == null || voMixerGroup == null)
        {
            Debug.LogError("AudioManager: Mixer groups not assigned! Please assign all mixer groups in the inspector.");
            return;
        }

        // Each channel gets its own child GameObject so Unity can route them through separate mixer groups
        musicChannel = new AudioChannel(CreateChannelObject("MusicSource"), musicMixerGroup, this);
        sfxChannel = new AudioChannel(CreateChannelObject("SFXSource"), sfxMixerGroup, this);
        voiceChannel = new AudioChannel(CreateChannelObject("VOSource"), voMixerGroup, this);

        Debug.Log("AudioManager: Audio channels initialized successfully.");
    }

    private GameObject CreateChannelObject(string name)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform);
        return obj;
    }

    private void InitializeVolumes()
    {
        if (audioMixer == null)
        {
            Debug.LogError("AudioManager: AudioMixer not assigned! Please assign the mixer in the inspector.");
            return;
        }

        SetMasterVolume(defaultMasterVolume);
        SetMusicVolume(defaultMusicVolume);
        SetSFXVolume(defaultSFXVolume);
        SetVOVolume(defaultVOVolume);
    }

    #endregion

    #region Music Control

    public void PlayMusic(AudioClip clip, bool loop = true, float fadeTime = 0f)
    {
        if (clip == null)
        {
            Debug.LogWarning("AudioManager: Cannot play null music clip.");
            return;
        }

        if (musicChannel == null)
        {
            Debug.LogError("AudioManager: Music channel not initialized.");
            return;
        }

        musicChannel.Play(clip, loop, volumeScale: 1f, fadeTime);
        OnMusicStarted?.Invoke(clip);
        Debug.Log($"AudioManager: Playing music '{clip.name}' (loop: {loop}, fade: {fadeTime}s)");
    }

    // fadeTime defaults to 1f so music doesn't cut out abruptly
    public void StopMusic(float fadeTime = 1f)
    {
        if (musicChannel == null) return;

        musicChannel.Stop(fadeTime, () => OnMusicStopped?.Invoke());
        Debug.Log($"AudioManager: Stopping music (fade: {fadeTime}s)");
    }

    public void PauseMusic()
    {
        if (musicChannel == null) return;

        musicChannel.Pause();
        Debug.Log("AudioManager: Music paused");
    }

    public void ResumeMusic()
    {
        if (musicChannel == null) return;

        musicChannel.Resume();
        Debug.Log("AudioManager: Music resumed");
    }

    public bool IsMusicPlaying()
    {
        return musicChannel != null && musicChannel.IsPlaying();
    }

    #endregion

    #region SFX Control

    // SFX are played as one-shots so multiple sounds can overlap (footsteps, hits, etc.)
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("AudioManager: Cannot play null SFX clip.");
            return;
        }

        if (sfxChannel == null)
        {
            Debug.LogError("AudioManager: SFX channel not initialized.");
            return;
        }

        sfxChannel.PlayOneShot(clip, volumeScale);
        OnSFXPlayed?.Invoke(clip);
    }

    public void StopAllSFX()
    {
        if (sfxChannel == null) return;

        sfxChannel.StopAllSounds();
        Debug.Log("AudioManager: All SFX stopped");
    }

    #endregion

    #region Voice-Over Control

    public void PlayVO(AudioClip clip, float delay = 0f)
    {
        if (clip == null)
        {
            Debug.LogWarning("AudioManager: Cannot play null VO clip.");
            return;
        }

        if (voiceChannel == null)
        {
            Debug.LogError("AudioManager: VO channel not initialized.");
            return;
        }

        if (delay > 0f)
        {
            // Cancel any VO that was already waiting to play before starting a new one
            if (voDelayCoroutine != null) StopCoroutine(voDelayCoroutine);
            voDelayCoroutine = StartCoroutine(PlayVODelayed(clip, delay));
        }
        else
        {
            voiceChannel.Play(clip, loop: false, volumeScale: 1f, fadeTime: 0f);
            OnVOStarted?.Invoke(clip);
            Debug.Log($"AudioManager: Playing VO '{clip.name}'");
        }
    }

    private IEnumerator PlayVODelayed(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        voDelayCoroutine = null;
        voiceChannel.Play(clip, loop: false, volumeScale: 1f, fadeTime: 0f);
        OnVOStarted?.Invoke(clip);
        Debug.Log($"AudioManager: Playing VO '{clip.name}' after {delay}s delay");
    }

    public void StopVO()
    {
        // Also cancel the delay coroutine so a queued VO doesn't play after stopping
        if (voDelayCoroutine != null)
        {
            StopCoroutine(voDelayCoroutine);
            voDelayCoroutine = null;
        }

        if (voiceChannel == null) return;

        voiceChannel.Stop(fadeTime: 0f);
        OnVOStopped?.Invoke();
    }

    public bool IsVOPlaying()
    {
        return voiceChannel != null && voiceChannel.IsPlaying();
    }

    #endregion

    #region Volume Control

    // Unity's AudioMixer works in decibels (dB), not linear 0-1 values,
    // so we convert before passing values to SetFloat.
    public void SetMasterVolume(float volume)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("AudioManager: Cannot set master volume - audioMixer is null");
            return;
        }

        volume = Mathf.Clamp01(volume);
        float db = LinearToDecibel(volume);
        bool success = audioMixer.SetFloat(MASTER_VOLUME_PARAM, db);
        if (!success)
        {
            Debug.LogError($"AudioManager: Failed to set '{MASTER_VOLUME_PARAM}'. Make sure this parameter is exposed in the AudioMixer!");
        }
    }

    public void SetMusicVolume(float volume)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("AudioManager: Cannot set music volume - audioMixer is null");
            return;
        }

        volume = Mathf.Clamp01(volume);
        float db = LinearToDecibel(volume);
        bool success = audioMixer.SetFloat(MUSIC_VOLUME_PARAM, db);
        if (!success)
        {
            Debug.LogError($"AudioManager: Failed to set '{MUSIC_VOLUME_PARAM}'. Make sure this parameter is exposed in the AudioMixer!");
        }
    }

    public void SetSFXVolume(float volume)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("AudioManager: Cannot set SFX volume - audioMixer is null");
            return;
        }

        volume = Mathf.Clamp01(volume);
        float db = LinearToDecibel(volume);
        bool success = audioMixer.SetFloat(SFX_VOLUME_PARAM, db);
        if (!success)
        {
            Debug.LogError($"AudioManager: Failed to set '{SFX_VOLUME_PARAM}'. Make sure this parameter is exposed in the AudioMixer!");
        }
    }

    public void SetVOVolume(float volume)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("AudioManager: Cannot set VO volume - audioMixer is null");
            return;
        }

        volume = Mathf.Clamp01(volume);
        float db = LinearToDecibel(volume);
        bool success = audioMixer.SetFloat(VO_VOLUME_PARAM, db);
        if (!success)
        {
            Debug.LogError($"AudioManager: Failed to set '{VO_VOLUME_PARAM}'. Make sure this parameter is exposed in the AudioMixer!");
        }
    }

    public float GetMasterVolume()
    {
        if (audioMixer == null) return 0f;

        audioMixer.GetFloat(MASTER_VOLUME_PARAM, out float db);
        return DecibelToLinear(db);
    }

    public float GetMusicVolume()
    {
        if (audioMixer == null) return 0f;

        audioMixer.GetFloat(MUSIC_VOLUME_PARAM, out float db);
        return DecibelToLinear(db);
    }

    public float GetSFXVolume()
    {
        if (audioMixer == null) return 0f;

        audioMixer.GetFloat(SFX_VOLUME_PARAM, out float db);
        return DecibelToLinear(db);
    }

    public float GetVOVolume()
    {
        if (audioMixer == null) return 0f;

        audioMixer.GetFloat(VO_VOLUME_PARAM, out float db);
        return DecibelToLinear(db);
    }

    // Plays a clip on a specific channel. Dialogue nodes use this to trigger audio manually
    // without going through the higher-level PlayMusic/PlayVO helpers.
    public void PlayOnChannel(AudioChannelType channelType, AudioClip clip, bool loop, float volumeScale, float fadeTime)
    {
        if (clip == null)
        {
            Debug.LogWarning("AudioManager: Cannot play null audio clip.");
            return;
        }

        volumeScale = Mathf.Clamp01(volumeScale);

        switch (channelType)
        {
            case AudioChannelType.Music:
                if (musicChannel == null)
                {
                    Debug.LogError("AudioManager: Music channel not initialized.");
                    return;
                }
                // Use PlayWithFadeIn to avoid automatic crossfade - dialogue nodes handle transitions manually
                musicChannel.PlayWithFadeIn(clip, loop, volumeScale, fadeTime);
                OnMusicStarted?.Invoke(clip);
                Debug.Log($"AudioManager: Playing music '{clip.name}' on Music channel (loop: {loop}, volume: {volumeScale}, fade: {fadeTime}s)");
                break;

            case AudioChannelType.SFX:
                PlaySFX(clip, volumeScale);
                break;

            case AudioChannelType.Voice:
                if (voiceChannel == null)
                {
                    Debug.LogError("AudioManager: VO channel not initialized.");
                    return;
                }
                // Use PlayWithFadeIn to avoid automatic crossfade - dialogue nodes handle transitions manually
                voiceChannel.PlayWithFadeIn(clip, loop, volumeScale, fadeTime);
                OnVOStarted?.Invoke(clip);
                Debug.Log($"AudioManager: Playing audio '{clip.name}' on Voice channel (loop: {loop}, volume: {volumeScale}, fade: {fadeTime}s)");
                break;
        }
    }

    // Smoothly changes the volume of a channel without stopping playback.
    // Useful for ducking music under dialogue or fading SFX in cutscenes.
    public void FadeChannelVolume(AudioChannelType channelType, float targetVolume, float duration)
    {
        targetVolume = Mathf.Clamp01(targetVolume);

        AudioChannel channel = channelType switch
        {
            AudioChannelType.Music => musicChannel,
            AudioChannelType.SFX => sfxChannel,
            AudioChannelType.Voice => voiceChannel,
            _ => null
        };

        if (channel == null)
        {
            Debug.LogWarning($"AudioManager: Channel '{channelType}' not initialized.");
            return;
        }

        if (duration <= 0f)
        {
            channel.SetVolume(targetVolume);
        }
        else
        {
            StartCoroutine(FadeChannelVolumeCoroutine(channel, targetVolume, duration));
        }
    }

    private IEnumerator FadeChannelVolumeCoroutine(AudioChannel channel, float targetVolume, float duration)
    {
        float startVolume = channel.GetVolume();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float newVolume = Mathf.Lerp(startVolume, targetVolume, t);
            channel.SetVolume(newVolume);
            yield return null;
        }

        channel.SetVolume(targetVolume);
    }

    public void StopChannel(AudioChannelType channelType, float fadeTime = 0f)
    {
        switch (channelType)
        {
            case AudioChannelType.Music:
                StopMusic(fadeTime);
                break;
            case AudioChannelType.SFX:
                StopAllSFX();
                break;
            case AudioChannelType.Voice:
                StopVO();
                break;
        }
    }

    public float GetMusicPlaybackTime()
    {
        return musicChannel?.GetPlaybackTime() ?? 0f;
    }

    public void PlayMusicFromTime(AudioClip clip, bool loop, float fadeTime, float startTime)
    {
        if (clip == null || musicChannel == null) return;

        musicChannel.PlayFromTime(clip, loop, 1f, startTime);
        OnMusicStarted?.Invoke(clip);
        Debug.Log($"AudioManager: Playing music '{clip.name}' from {startTime}s (loop: {loop})");
    }

    // Looks up the music clip for a given floor and crossfades to it.
    // Calling this with the same floor index while that track is already playing is a no-op.
    public void PlayMusicForFloor(int floorIndex, float fadeTime = -1f)
    {
        if (floorMusicEntries == null || floorMusicEntries.Length == 0) return;

        // A negative fadeTime means "use the default" set in the inspector
        float fade = fadeTime < 0f ? floorMusicFadeTime : fadeTime;

        foreach (var entry in floorMusicEntries)
        {
            if (entry.floorIndex != floorIndex) continue;

            if (entry.music == null)
            {
                Debug.LogWarning($"AudioManager: FloorMusicEntry for floor {floorIndex} has no clip assigned.");
                return;
            }

            // Already on this floor's track, don't restart
            if (currentFloorIndex == floorIndex && musicChannel?.CurrentClip == entry.music && IsMusicPlaying())
                return;

            currentFloorIndex = floorIndex;
            PlayMusic(entry.music, loop: true, fadeTime: fade);
            return;
        }

        Debug.LogWarning($"AudioManager: No FloorMusicEntry found for floor {floorIndex}.");
    }

    // Saves music playback position to PlayerPrefs so it can be restored after a scene transition
    public void SaveMusicPlaybackTime(string variableName = "music_playback_time")
    {
        if (musicChannel != null)
        {
            float time = musicChannel.GetPlaybackTime();
            PlayerPrefs.SetFloat(variableName, time);
            Debug.Log($"AudioManager: Saved music playback time {time}s to '{variableName}'");
        }
    }

    #endregion

    #region Utility Methods

    // Unity's AudioMixer expects volume values in decibels (dB), not 0-1.
    // Decibel scale is logarithmic, so 0.5 linear is around -6 dB, not -40 dB.
    // We clamp to -80 dB at zero because log10(0) is undefined (negative infinity).
    private float LinearToDecibel(float linear)
    {
        if (linear <= 0f) return -80f;
        return Mathf.Log10(linear) * 20f;
    }

    private float DecibelToLinear(float decibel)
    {
        return Mathf.Pow(10f, decibel / 20f);
    }

    #endregion

    // Manages a single AudioSource with fade and crossfade support.
    // Kept as a nested class because nothing outside AudioManager should interact with it directly.
    private class AudioChannel
    {
        private AudioSource source;
        private Coroutine fadeCoroutine;
        private MonoBehaviour owner;

        public AudioClip CurrentClip => source.clip;

        public AudioChannel(GameObject parent, AudioMixerGroup mixerGroup, MonoBehaviour owner)
        {
            source = parent.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = mixerGroup;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D audio - not affected by the listener's position in the world
            this.owner = owner;
        }

        // Plays with an optional crossfade: if something is already playing it fades out
        // before fading the new clip in. Use PlayWithFadeIn if you don't want the crossfade.
        public void Play(AudioClip clip, bool loop, float volumeScale, float fadeTime)
        {
            if (fadeCoroutine != null)
            {
                owner.StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            if (fadeTime > 0f)
            {
                fadeCoroutine = owner.StartCoroutine(FadeToClip(clip, loop, volumeScale, fadeTime));
            }
            else
            {
                PlayImmediate(clip, loop, volumeScale);
            }
        }

        // Stops whatever is playing and fades the new clip in from silence.
        // Used by dialogue nodes that manage their own transitions.
        public void PlayWithFadeIn(AudioClip clip, bool loop, float volumeScale, float fadeTime)
        {
            if (fadeCoroutine != null)
            {
                owner.StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            source.Stop();

            if (fadeTime > 0f)
            {
                fadeCoroutine = owner.StartCoroutine(FadeInClip(clip, loop, volumeScale, fadeTime));
            }
            else
            {
                PlayImmediate(clip, loop, volumeScale);
            }
        }

        // One-shot fires and forgets - multiple one-shots can overlap on the same source
        public void PlayOneShot(AudioClip clip, float volumeScale)
        {
            source.PlayOneShot(clip, volumeScale);
        }

        public void Stop(float fadeTime, Action onComplete = null)
        {
            if (fadeCoroutine != null)
            {
                owner.StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            if (fadeTime > 0f && source.isPlaying)
            {
                fadeCoroutine = owner.StartCoroutine(FadeOut(fadeTime, onComplete));
            }
            else
            {
                source.Stop();
                onComplete?.Invoke();
            }
        }

        // PlayOneShot sounds can't be stopped via source.Stop() alone, so we disable
        // and re-enable the component to force Unity to drop all pending one-shot audio.
        public void StopAllSounds()
        {
            if (fadeCoroutine != null)
            {
                owner.StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
            source.Stop();
            source.enabled = false;
            source.enabled = true;
        }

        public void Pause()
        {
            source.Pause();
        }

        public void Resume()
        {
            source.UnPause();
        }

        public bool IsPlaying()
        {
            return source.isPlaying;
        }

        public float GetVolume()
        {
            return source.volume;
        }

        public void SetVolume(float volume)
        {
            source.volume = Mathf.Clamp01(volume);
        }

        public float GetPlaybackTime()
        {
            return source.time;
        }

        public void SetPlaybackTime(float time)
        {
            if (source.clip != null)
            {
                source.time = Mathf.Clamp(time, 0f, source.clip.length);
            }
        }

        public void PlayFromTime(AudioClip clip, bool loop, float volumeScale, float startTime)
        {
            source.clip = clip;
            source.loop = loop;
            source.volume = volumeScale;
            source.Play();
            // Must set time AFTER Play() for it to work correctly in Unity
            source.time = Mathf.Clamp(startTime, 0f, clip.length);
        }

        private void PlayImmediate(AudioClip clip, bool loop, float volumeScale)
        {
            source.clip = clip;
            source.loop = loop;
            source.volume = volumeScale;
            source.Play();
        }

        private IEnumerator FadeToClip(AudioClip clip, bool loop, float volumeScale, float duration)
        {
            // If something is playing, crossfade: spend the first half fading out, second half fading in.
            // If nothing is playing, just fade in over the full duration.
            bool wasPlaying = source.isPlaying;
            float fadeInDuration = duration;

            if (wasPlaying)
            {
                float halfDuration = duration / 2f;
                float startVolume = source.volume;
                float elapsed = 0f;

                while (elapsed < halfDuration)
                {
                    elapsed += Time.deltaTime;
                    source.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfDuration);
                    yield return null;
                }

                fadeInDuration = halfDuration;
            }

            PlayImmediate(clip, loop, 0f);

            float elapsed2 = 0f;
            while (elapsed2 < fadeInDuration)
            {
                elapsed2 += Time.deltaTime;
                source.volume = Mathf.Lerp(0f, volumeScale, elapsed2 / fadeInDuration);
                yield return null;
            }

            source.volume = volumeScale;
            fadeCoroutine = null;
        }

        private IEnumerator FadeInClip(AudioClip clip, bool loop, float volumeScale, float duration)
        {
            PlayImmediate(clip, loop, 0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(0f, volumeScale, elapsed / duration);
                yield return null;
            }

            source.volume = volumeScale;
            fadeCoroutine = null;
        }

        private IEnumerator FadeOut(float duration, Action onComplete = null)
        {
            float startVolume = source.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            source.Stop();
            source.volume = startVolume; // Restore volume so the next Play() starts at full level
            fadeCoroutine = null;
            onComplete?.Invoke();
        }
    }
}
