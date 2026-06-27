using UnityEngine;
using UnityEngine.Video;

namespace AdequateEnough
{
    [RequireComponent(typeof(VideoPlayer))]
    public class StreamingVideoPlayer : MonoBehaviour
    {
        [SerializeField] private string fileName;

        private void Awake()
        {
            VideoPlayer vp = GetComponent<VideoPlayer>();
            vp.source = VideoSource.Url;
            vp.url = Application.streamingAssetsPath + "/" + fileName;
        }
    }
}
