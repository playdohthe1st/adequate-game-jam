using AdequateEnough;
using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    public float health, maxHealth, width, height;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private RectTransform healthBar;
    public void SetMaxHealth(float MaxHealth)
    {
        maxHealth = MaxHealth;
    }

    public void SetHealth(float Health)
    {
        health = Health;
        float newWidth = (health / maxHealth) * width;

        healthBar.sizeDelta = new Vector2 (newWidth, height);
    }
}
