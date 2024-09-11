using UnityEngine;
using Cinemachine;


namespace ArcadeBP_Pro
{
    public class CameraController : MonoBehaviour
    {
        [Tooltip("Array of Cinemachine virtual cameras.")]
        public CinemachineVirtualCamera[] cameras;

        [Tooltip("Reference to the bike controller.")]
        public ArcadeBikeControllerPro bikeController;

        [Tooltip("Speed threshold above which the camera shake effect starts.")]
        public float speedThresholdForShake = 50f;

        [Tooltip("Amplitude of the camera shake effect.")]
        public float shakeAmplitude = 1.2f;

        [Tooltip("Frequency of the camera shake effect.")]
        public float shakeFrequency = 2.0f;

        [Tooltip("Key to switch between different cameras.")]
        public KeyCode switchCameraKey = KeyCode.C;

        [Tooltip("Minimum camera FOV value.")]
        public float minFOV = 60f;

        [Tooltip("Maximum camera FOV value.")]
        public float maxFOV = 80f;

        [Tooltip("Smoothing factor for the camera FOV value.")]
        public float FOV_smoother = 5f;


        private CinemachineBasicMultiChannelPerlin[] cameraNoise;
        private int currentCameraIndex = 0;
        private bool isShaking = false;

        Transform[] initialCameraFollowTargets;
        Transform[] initialCameraLookAtTargets;

        private void Awake()
        {
            transform.parent = null;
            smoothFOV = 60f;

            transform.name = "CameraController_" + RemovePrefix(bikeController.transform.name, "ABP_Pro");
        }

        void Start()
        {
            cameraNoise = new CinemachineBasicMultiChannelPerlin[cameras.Length];

            initialCameraFollowTargets = new Transform[cameras.Length];
            initialCameraLookAtTargets = new Transform[cameras.Length];

            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].gameObject.SetActive(i == currentCameraIndex);
                cameraNoise[i] = cameras[i].GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

                initialCameraFollowTargets[i] = cameras[i].Follow;
                initialCameraLookAtTargets[i] = cameras[i].LookAt;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(switchCameraKey))
            {
                SwitchCamera();
            }

            float bikeSpeed = bikeController.localBikeVelocity.magnitude;

            if (bikeSpeed > speedThresholdForShake && bikeController.isActiveAndEnabled)
            {
                UpdateShake(bikeSpeed);
            }
            else if (isShaking)
            {
                StopShake();
            }

            UpdateFOV(bikeSpeed);
        }

        void SwitchCamera()
        {
            cameras[currentCameraIndex].gameObject.SetActive(false);
            currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
            cameras[currentCameraIndex].gameObject.SetActive(true);


            StopShake();
        }

        void UpdateShake(float bikeSpeed)
        {
            isShaking = true;
            if (cameraNoise[currentCameraIndex] != null)
            {
                float t = Mathf.InverseLerp(speedThresholdForShake, bikeController.bikeSettings.maxSpeed, bikeSpeed);
                cameraNoise[currentCameraIndex].m_AmplitudeGain = Mathf.Lerp(0, shakeAmplitude, t);
                cameraNoise[currentCameraIndex].m_FrequencyGain = Mathf.Lerp(0, shakeFrequency, t);
            }
        }

        void StopShake()
        {
            isShaking = false;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameraNoise[i] != null)
                {
                    cameraNoise[i].m_AmplitudeGain = 0f;
                    cameraNoise[i].m_FrequencyGain = 0f;
                }
            }
        }


        float smoothFOV = 60f;
        void UpdateFOV(float bikeSpeed)
        {
            float t = Mathf.InverseLerp(0, bikeController.bikeSettings.maxSpeed, bikeSpeed);
            float newFOV = Mathf.Lerp(minFOV, maxFOV, t);

            smoothFOV = Mathf.Lerp(smoothFOV, newFOV, Time.deltaTime * FOV_smoother);

            cameras[currentCameraIndex].m_Lens.FieldOfView = smoothFOV;
        }


        public void SetCameratarget(Transform followTarget, Transform lookAtTarget)
        {
            foreach (var camera in cameras)
            {
                camera.LookAt = lookAtTarget;
                camera.Follow = followTarget;
            }

            StopShake();
        }

        public void resetCameratarget()
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].Follow = initialCameraFollowTargets[i];
                cameras[i].LookAt = initialCameraLookAtTargets[i];
            }

            StopShake();
        }



        private string RemovePrefix(string originalName, string prefix)
        {
            if (originalName.StartsWith(prefix))
            {
                return originalName.Substring(prefix.Length);
            }
            return originalName;
        }

    }

}
