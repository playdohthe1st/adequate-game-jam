using UnityEngine;
using UnityEngine.SceneManagement;

public class Scenes : MonoBehaviour
{
    public void LoadLevelOne()
    {
        SceneManager.LoadScene("LevelOneTest");
    }
}
