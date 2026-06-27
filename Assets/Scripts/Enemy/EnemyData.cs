using UnityEngine;

namespace AdequateEnough
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "AdequateEnough/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Prefab")]
        public GameObject enemyPrefab;

        [Header("Stats")]
        public float health = 50f;
        public float attackDamage = 10f;

        [Header("Behavior")]
        public bool canMelee = true;
        public bool canShoot = true;

        [Header("Audio")]
        public AudioClip attackSFX;
        public AudioClip hurtSFX;
        public AudioClip deathSFX;

        [Header("Boss")]
        public bool isBoss;
        public GameObject keycardDropPrefab;

        // Boss enemies get 2x health and attack damage, and spawn at 1.5x scale
        public float ResolvedHealth => isBoss ? health * 2f : health;
        public float ResolvedAttackDamage => isBoss ? attackDamage * 2f : attackDamage;
        public float ScaleMultiplier => isBoss ? 2.5f : 1f;
    }
}
