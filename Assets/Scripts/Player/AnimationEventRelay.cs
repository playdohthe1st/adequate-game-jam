using UnityEngine;

namespace AdequateEnough
{
    public class AnimationEventRelay : MonoBehaviour
    {
        private PlayerController player;

        private void Awake()
        {
            player = GetComponentInParent<PlayerController>();
        }

        public void PlayFootstepSFX()
        {
            player?.PlayFootstepSFX();
        }
    }
}
