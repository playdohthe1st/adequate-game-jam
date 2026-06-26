using UnityEngine;

public class LorePaperItem : MonoBehaviour
{
    public int paperID;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Enter trigger ");
        if (other.CompareTag("Player"))
        {
            LoreManager manager = FindFirstObjectByType<LoreManager>();
            if (manager != null)
            {
                manager.CollectPaper(paperID);
                Destroy(gameObject); 
            }
        }
    }
}
