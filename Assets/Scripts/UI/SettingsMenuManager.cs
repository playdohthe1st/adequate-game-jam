using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AdequateEnough
{
    public class SettingsMenuManager : MonoBehaviour
    {
        [Header("Volume Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider voVolumeSlider;

        [Header("Other Settings")]
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Navigation")]
        [SerializeField] private UnityEvent onBack;

        private void Start()
        {
            Sync();
        }

        private void OnEnable()
        {
            Sync();
        }

        private void Sync()
        {
            SyncFromAudioManager();
#if !UNITY_WEBGL
            if (fullscreenToggle != null)
                fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
#else
            if (fullscreenToggle != null)
                fullscreenToggle.gameObject.SetActive(false);
#endif
        }

        private void SyncFromAudioManager()
        {
            if (AudioManager.Instance == null) return;

            if (masterVolumeSlider != null)
                masterVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.GetMasterVolume());
            if (musicVolumeSlider != null)
                musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.GetMusicVolume());
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.GetSFXVolume());
            if (voVolumeSlider != null)
                voVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.GetVOVolume());
        }

        public void OnMasterVolumeChanged(float value)
        {
            AudioManager.Instance?.SetMasterVolume(value);
        }

        public void OnMusicVolumeChanged(float value)
        {
            AudioManager.Instance?.SetMusicVolume(value);
        }

        public void OnSFXVolumeChanged(float value)
        {
            AudioManager.Instance?.SetSFXVolume(value);
        }

        public void OnVOVolumeChanged(float value)
        {
            AudioManager.Instance?.SetVOVolume(value);
        }

        public void OnFullscreenToggled(bool value)
        {
#if !UNITY_WEBGL
            Screen.fullScreen = value;
#endif
        }

        public void OnBackPressed()
        {
            onBack?.Invoke();
        }
    }
}
