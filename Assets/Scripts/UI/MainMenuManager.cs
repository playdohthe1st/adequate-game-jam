using UnityEngine;
using UnityEngine.UI;

namespace AdequateEnough
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            ShowMain();
        }

        public void OnStartPressed()
        {
            ScreenFader.Instance.FadeToScene("MainLevel");
        }

        public void OnSettingsPressed()
        {
            mainPanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        public void OnSettingsBackPressed()
        {
            settingsPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void ShowMain()
        {
            mainPanel.SetActive(true);
            settingsPanel.SetActive(false);
        }
    }
}
