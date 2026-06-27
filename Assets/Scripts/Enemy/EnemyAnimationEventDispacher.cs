using UnityEngine;

namespace AdequateEnough
{
    public class EnemyAnimationEventDispatcher : MonoBehaviour
    {
        private Enemy parentEnemy;

        private void Awake()
        {
            // Find the Enemy script on the parent object
            parentEnemy = GetComponentInParent<Enemy>();
        }

        // This is the function you will select in your Animation Event!
        public void TriggerRangedAttack()
        {
            if (parentEnemy != null)
            {
                parentEnemy.ExecuteProjectileSpawn();
            }
        }
    }
}