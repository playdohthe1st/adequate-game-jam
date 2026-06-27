using UnityEngine;
using UnityEngine.Video;

namespace AdequateEnough
{
    [RequireComponent(typeof(VideoPlayer))]
    public class StreamingVideoPlayer : MonoBehaviour
    {
        [SerializeField] private string fileName;
        [SerializeField] private float musicFadeDuration = 1f;

        private VideoPlayer vp;

        private void Awake()
        {
            vp = GetComponent<VideoPlayer>();
            vp.source = VideoSource.Url;
            vp.url = Application.streamingAssetsPath + "/" + fileName;

            vp.started += OnVideoStarted;
            vp.loopPointReached += OnVideoEnded;
        }

        private void OnDestroy()
        {
            vp.started -= OnVideoStarted;
            vp.loopPointReached -= OnVideoEnded;
        }

        private void OnVideoStarted(VideoPlayer _)
        {
            AudioManager.Instance?.FadeChannelVolume(AudioChannelType.Music, 0f, musicFadeDuration);
        }

        private void OnVideoEnded(VideoPlayer _)
        {
            AudioManager.Instance?.FadeChannelVolume(AudioChannelType.Music, 1f, musicFadeDuration);
        }
    }
}
