using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    #region Singleton

    /// <summary>
    /// Singleton instance of the AudioManager.
    /// </summary>
    public static AudioManager Instance { get; private set; }

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
    [Tooltip("Scene names that should NOT trigger default gameplay music (e.g. MainMenu)")]
    [SerializeField] private string[] nonGameplayScenes = { "MainMenu" };

    #endregion

    #region Audio Channels

    private AudioChannel musicChannel;
    private AudioChannel sfxChannel;
    private AudioChannel voiceChannel;

    #endregion

    #region Constants

    private const string MASTER_VOLUME_PARAM = "MasterVolume";
    private const string MUSIC_VOLUME_PARAM = "MusicVolume";
    private const string SFX_VOLUME_PARAM = "SFXVolume";
    private const string VO_VOLUME_PARAM = "VOVolume";

    #endregion

    #region Events

    /// <summary>Fired when music starts playing.</summary>
    public event Action<AudioClip> OnMusicStarted;

    /// <summary>Fired when music stops.</summary>
    public event Action OnMusicStopped;

    /// <summary>Fired when a sound effect plays.</summary>
    public event Action<AudioClip> OnSFXPlayed;

    /// <summary>Fired when voice-over starts playing.</summary>
    public event Action<AudioClip> OnVOStarted;

    /// <summary>Fired when voice-over stops.</summary>
    public event Action OnVOStopped;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure there's always an AudioListener
        if (FindFirstObjectByType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }

        InitializeChannels();
        InitializeVolumes();
    }

    private void OnEnable()
    {
        // Subscribe to scene loaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // Unsubscribe from scene loaded event
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }



    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Called when a new scene is loaded. Finds DialogueManager in the new scene.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Play default gameplay music if this is a gameplay scene
        if (defaultGameplayMusic != null && IsGameplayScene(scene.name))
        {
            PlayMusic(defaultGameplayMusic, loop: true, fadeTime: 1f);
        }
    }

    /// <summary>
    /// Checks if the given scene name is a gameplay scene (not in the nonGameplayScenes list).
    /// </summary>
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

    /// <summary>
    /// Plays a music track.
    /// </summary>
    /// <param name="clip">The audio clip to play.</param>
    /// <param name="loop">Whether to loop the music.</param>
    /// <param name="fadeTime">Fade duration in seconds (0 for instant).</param>
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

    /// <summary>
    /// Stops the currently playing music.
    /// </summary>
    /// <param name="fadeTime">Fade out duration in seconds (0 for instant).</param>
    public void StopMusic(float fadeTime = 1f)
    {
        if (musicChannel == null) return;

        musicChannel.Stop(fadeTime);
        OnMusicStopped?.Invoke();
        Debug.Log($"AudioManager: Stopping music (fade: {fadeTime}s)");
    }

    /// <summary>
    /// Pauses the currently playing music.
    /// </summary>
    public void PauseMusic()
    {
        if (musicChannel == null) return;

        musicChannel.Pause();
        Debug.Log("AudioManager: Music paused");
    }

    /// <summary>
    /// Resumes the paused music.
    /// </summary>
    public void ResumeMusic()
    {
        if (musicChannel == null) return;

        musicChannel.Resume();
        Debug.Log("AudioManager: Music resumed");
    }

    /// <summary>
    /// Checks if music is currently playing.
    /// </summary>
    public bool IsMusicPlaying()
    {
        return musicChannel != null && musicChannel.IsPlaying();
    }

    #endregion

    #region SFX Control

    /// <summary>
    /// Plays a sound effect (one-shot).
    /// </summary>
    /// <param name="clip">The audio clip to play.</param>
    /// <param name="volumeScale">Volume multiplier (0-1).</param>
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

    /// <summary>
    /// Stops all currently playing sound effects.
    /// </summary>
    public void StopAllSFX()
    {
        if (sfxChannel == null) return;

        sfxChannel.Stop(fadeTime: 0f);
        Debug.Log("AudioManager: All SFX stopped");
    }

    #endregion

    #region Voice-Over Control

    /// <summary>
    /// Plays a voice-over clip.
    /// </summary>
    /// <param name="clip">The audio clip to play.</param>
    /// <param name="delay">Delay before playing in seconds.</param>
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
            StartCoroutine(PlayVODelayed(clip, delay));
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
        voiceChannel.Play(clip, loop: false, volumeScale: 1f, fadeTime: 0f);
        OnVOStarted?.Invoke(clip);
        Debug.Log($"AudioManager: Playing VO '{clip.name}' after {delay}s delay");
    }

    /// <summary>
    /// Stops the currently playing voice-over.
    /// </summary>
    public void StopVO()
    {
        if (voiceChannel == null) return;

        voiceChannel.Stop(fadeTime: 0f);
        OnVOStopped?.Invoke();
    }

    /// <summary>
    /// Checks if voice-over is currently playing.
    /// </summary>
    public bool IsVOPlaying()
    {
        return voiceChannel != null && voiceChannel.IsPlaying();
    }

    #endregion

    #region Volume Control

    /// <summary>
    /// Sets the master volume (affects all audio).
    /// </summary>
    /// <param name="volume">Volume (0-1 linear scale).</param>
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

    /// <summary>
    /// Sets the music volume.
    /// </summary>
    /// <param name="volume">Volume (0-1 linear scale).</param>
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

    /// <summary>
    /// Sets the SFX volume.
    /// </summary>
    /// <param name="volume">Volume (0-1 linear scale).</param>
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

    /// <summary>
    /// Sets the voice-over volume.
    /// </summary>
    /// <param name="volume">Volume (0-1 linear scale).</param>
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

    /// <summary>
    /// Gets the current master volume.
    /// </summary>
    /// <returns>Volume (0-1 linear scale).</returns>
    public float GetMasterVolume()
    {
        if (audioMixer == null) return 0f;

        audioMixer.GetFloat(MASTER_VOLUME_PARAM, out float db);
        return DecibelToLinear(db);
    }

    /// <summary>
    /// Gets the current music volume.
    /// </summary>
    /// <returns>Volume (0-1 linear scale).</returns>
    public float GetMusicVolume()
    {
        if (audioMixer == null) return 0f;

        audioMixer.GetFloat(MUSIC_VOLUME_PARAM, out float db);
        return DecibelToLinear(db);
    }

    /// <summary>
    /// Gets the current SFX volume.
    /// </summary>
    /// <returns>Volume (0-1 linear scale).</returns>
    public float GetSFXVolume()
    {
        if (audioMixer == null) return 0f;

        audioMixer.GetFloat(SFX_VOLUME_PARAM, out float db);
        return DecibelToLinear(db);
    }

    /// <summary>
    /// Gets the current voice-over volume.
    /// </summary>
    /// <returns>Volume (0-1 linear scale).</returns>
    public float GetVOVolume()
    {
        if (audioMixer == null) return 0f;

        audioMixer.GetFloat(VO_VOLUME_PARAM, out float db);
        return DecibelToLinear(db);
    }

    /// <summary>
    /// Plays an audio clip on the specified channel with optional fade.
    /// When fadeTime > 0, the audio will start at volume 0 and fade to the target volume.
    /// </summary>
    /// <param name="channelType">Which audio channel to play on (Music, SFX, or Voice).</param>
    /// <param name="clip">The audio clip to play.</param>
    /// <param name="loop">Whether the audio should loop.</param>
    /// <param name="volumeScale">Target volume (0-1 linear scale).</param>
    /// <param name="fadeTime">Fade duration in seconds (0 for instant).</param>
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

    /// <summary>
    /// Smoothly fades a channel's volume to a target value over the specified duration.
    /// </summary>
    /// <param name="channelType">Which audio channel to fade (Music, SFX, or Voice).</param>
    /// <param name="targetVolume">Target volume (0-1 linear scale).</param>
    /// <param name="duration">Fade duration in seconds.</param>
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

        // If duration is 0 or negative, set immediately
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

    /// <summary>
    /// Stops audio on the specified channel with optional fade out.
    /// This will stop the audio even if it's set to loop.
    /// </summary>
    /// <param name="channelType">Which audio channel to stop (Music, SFX, or Voice).</param>
    /// <param name="fadeTime">Fade out duration in seconds (0 for instant stop).</param>
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

    /// <summary>
    /// Gets the current playback time of the music channel in seconds.
    /// </summary>
    public float GetMusicPlaybackTime()
    {
        return musicChannel?.GetPlaybackTime() ?? 0f;
    }

    /// <summary>
    /// Plays music starting from a specific time position.
    /// </summary>
    public void PlayMusicFromTime(AudioClip clip, bool loop, float fadeTime, float startTime)
    {
        if (clip == null || musicChannel == null) return;

        musicChannel.PlayFromTime(clip, loop, 1f, startTime);
        OnMusicStarted?.Invoke(clip);
        Debug.Log($"AudioManager: Playing music '{clip.name}' from {startTime}s (loop: {loop})");
    }

    /// <summary>
    /// Saves the current music playback time to GlobalVariables.
    /// Call this before a scene transition to preserve playback position.
    /// </summary>
    public void SaveMusicPlaybackTime(string variableName = "music_playback_time")
    {
        if (GlobalVariables.Instance != null && musicChannel != null)
        {
            float time = musicChannel.GetPlaybackTime();
            GlobalVariables.Instance.SetFloat(variableName, time);
            Debug.Log($"AudioManager: Saved music playback time {time}s to '{variableName}'");
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Converts linear volume (0-1) to decibel scale (-80 to 0).
    /// </summary>
    private float LinearToDecibel(float linear)
    {
        if (linear <= 0f) return -80f;
        return Mathf.Log10(linear) * 20f;
    }

    /// <summary>
    /// Converts decibel volume to linear scale (0-1).
    /// </summary>
    private float DecibelToLinear(float decibel)
    {
        return Mathf.Pow(10f, decibel / 20f);
    }

    #endregion

    

    /// <summary>
    /// Internal class that manages a single audio channel with fade support.
    /// </summary>
    private class AudioChannel
    {
        private AudioSource source;
        private Coroutine fadeCoroutine;
        private MonoBehaviour owner;

        public AudioChannel(GameObject parent, AudioMixerGroup mixerGroup, MonoBehaviour owner)
        {
            source = parent.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = mixerGroup;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D audio
            this.owner = owner;
        }

        /// <summary>
        /// Plays an audio clip with optional fade (includes crossfade if something is playing).
        /// </summary>
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

        /// <summary>
        /// Plays an audio clip with fade-in from 0 to target volume (no crossfade).
        /// Use this when you want manual control over fade transitions.
        /// </summary>
        public void PlayWithFadeIn(AudioClip clip, bool loop, float volumeScale, float fadeTime)
        {
            if (fadeCoroutine != null)
            {
                owner.StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            // Stop any currently playing audio immediately
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

        /// <summary>
        /// Plays a one-shot audio clip (for SFX).
        /// </summary>
        public void PlayOneShot(AudioClip clip, float volumeScale)
        {
            source.PlayOneShot(clip, volumeScale);
        }

        /// <summary>
        /// Stops the audio with optional fade out.
        /// </summary>
        public void Stop(float fadeTime)
        {
            if (fadeCoroutine != null)
            {
                owner.StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            if (fadeTime > 0f && source.isPlaying)
            {
                fadeCoroutine = owner.StartCoroutine(FadeOut(fadeTime));
            }
            else
            {
                source.Stop();
            }
        }

        /// <summary>
        /// Pauses the audio.
        /// </summary>
        public void Pause()
        {
            source.Pause();
        }

        /// <summary>
        /// Resumes the paused audio.
        /// </summary>
        public void Resume()
        {
            source.UnPause();
        }

        /// <summary>
        /// Checks if audio is currently playing.
        /// </summary>
        public bool IsPlaying()
        {
            return source.isPlaying;
        }

        /// <summary>
        /// Gets the current volume of this channel.
        /// </summary>
        public float GetVolume()
        {
            return source.volume;
        }

        /// <summary>
        /// Sets the volume of this channel directly.
        /// </summary>
        public void SetVolume(float volume)
        {
            source.volume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// Gets the current playback time in seconds.
        /// </summary>
        public float GetPlaybackTime()
        {
            return source.time;
        }

        /// <summary>
        /// Sets the playback time in seconds.
        /// </summary>
        public void SetPlaybackTime(float time)
        {
            if (source.clip != null)
            {
                source.time = Mathf.Clamp(time, 0f, source.clip.length);
            }
        }

        /// <summary>
        /// Plays an audio clip starting from a specific time position.
        /// </summary>
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
            // If something is already playing, do a crossfade (half out, half in)
            // Otherwise, just fade in from 0 over the full duration
            bool wasPlaying = source.isPlaying;
            float fadeInDuration = duration;

            if (wasPlaying)
            {
                // Crossfade: fade out current over half the duration
                float halfDuration = duration / 2f;
                float startVolume = source.volume;
                float elapsed = 0f;

                while (elapsed < halfDuration)
                {
                    elapsed += Time.deltaTime;
                    source.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfDuration);
                    yield return null;
                }

                fadeInDuration = halfDuration; // Fade in over the remaining half
            }

            // Switch to new clip at volume 0
            PlayImmediate(clip, loop, 0f);

            // Fade in new clip to target volume
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

        /// <summary>
        /// Fades in a new clip from 0 to target volume (no crossfade).
        /// </summary>
        private IEnumerator FadeInClip(AudioClip clip, bool loop, float volumeScale, float duration)
        {
            // Start playing at volume 0
            PlayImmediate(clip, loop, 0f);

            // Fade in to target volume over the full duration
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

        private IEnumerator FadeOut(float duration)
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
            source.volume = startVolume; // Restore volume for next play
            fadeCoroutine = null;
        }
    }
}