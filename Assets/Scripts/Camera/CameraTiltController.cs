using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

namespace AdequateEnough
{
    public class CameraTiltController : MonoBehaviour
    {
        public static CameraTiltController Instance { get; private set; }

        // [SerializeField] private float baseTiltAngle = -2f;
        // [SerializeField] private float tiltSpeed = 8f;
        [SerializeField] private float panEnterSpeed = 2f;
        [SerializeField] private float panResetSpeed = 8f;

        // private float targetTilt;
        // private float currentTilt;
        private float targetPanX;
        private float currentPanX;

        private CinemachineVirtualCamera vcam;
        private CinemachineFramingTransposer transposer;

        private void Awake()
        {
            Instance = this;
            vcam = GetComponent<CinemachineVirtualCamera>();
            if (vcam == null)
                vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
            if (vcam != null)
            {
                transposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
                if (transposer != null)
                    transposer.m_XDamping = 0f;
            }
        }

        // private void Start()
        // {
        //     currentTilt = baseTiltAngle;
        //     targetTilt = baseTiltAngle;
        //     ApplyTilt(currentTilt);
        // }

        private bool isResetting;

        private void LateUpdate()
        {
            // currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSpeed * Time.deltaTime);
            // if (Mathf.Abs(currentTilt - targetTilt) < 0.01f) currentTilt = targetTilt;
            // ApplyTilt(currentTilt);

            float panSpeed = isResetting ? panResetSpeed : panEnterSpeed;
            currentPanX = Mathf.Lerp(currentPanX, targetPanX, panSpeed * Time.deltaTime);
            if (Mathf.Abs(currentPanX - targetPanX) < 0.001f) currentPanX = targetPanX;
            if (transposer != null)
                transposer.m_TrackedObjectOffset.x = currentPanX;
        }

        private readonly List<(object id, float tilt, float pan)> activeZones = new();

        public void EnterZone(object id, float angle, float panX)
        {
            activeZones.RemoveAll(z => z.id == id);
            activeZones.Add((id, angle, panX));
            isResetting = false;
            // targetTilt = angle;
            targetPanX = panX;
        }

        public void ExitZone(object id)
        {
            activeZones.RemoveAll(z => z.id == id);

            if (activeZones.Count > 0)
            {
                var last = activeZones[^1];
                isResetting = false;
                // targetTilt = last.tilt;
                targetPanX = last.pan;
            }
            else
            {
                isResetting = true;
                // targetTilt = baseTiltAngle;
                targetPanX = 0f;
            }
        }

        // private void ApplyTilt(float angle)
        // {
        //     if (vcam != null)
        //         vcam.m_Lens.Dutch = angle;
        // }
    }
}
