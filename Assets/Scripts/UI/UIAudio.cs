using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdequateEnough
{
    [RequireComponent(typeof(Button))]
    public class UIAudio : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private AudioClip hoverSFX;
        [SerializeField] private AudioClip clickSFX;
        [SerializeField] private AudioClip backSFX;
        [SerializeField] private bool isBackButton = false;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hoverSFX != null) AudioManager.Instance?.PlaySFX(hoverSFX);
        }

        private void OnClick()
        {
            AudioClip clip = isBackButton ? backSFX : clickSFX;
            if (clip != null) AudioManager.Instance?.PlaySFX(clip);
        }
    }
}
