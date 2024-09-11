using UnityEngine;
using UnityEngine.UI;


namespace ArcadeBP_Pro
{
    public class ResetBike : MonoBehaviour
    {
        public BikeSwitcher bikeSwitcher;
        public Button UnRagdollBikeButton;
        public Button resetBikeButton;

        private void Start()
        {
            // Add listeners to the buttons
            if (UnRagdollBikeButton != null)
            {
                UnRagdollBikeButton.onClick.AddListener(UnRagdollBike);
            }

            if (resetBikeButton != null)
            {
                resetBikeButton.onClick.AddListener(() => ResetCurrentBike());
            }
        }

        private void UnRagdollBike()
        {
            ArcadeBikeControllerPro currentBike = bikeSwitcher.GetCurrentBike();
            if (currentBike != null)
            {
                RagdollActivator ragdollActivator = currentBike.bikeReferences.ragdollActivator;
                if (ragdollActivator != null)
                {
                    ragdollActivator.ReEnableBike();
                }
            }
        }

        private void ResetCurrentBike()
        {
            ArcadeBikeControllerPro currentBike = bikeSwitcher.GetCurrentBike();
            if (currentBike != null)
            {
                currentBike.bikeReferences.BikeRb.velocity = Vector3.zero;
                currentBike.bikeReferences.BikeRb.angularVelocity = Vector3.zero;
                currentBike.bikeReferences.Rotator.transform.localRotation = Quaternion.identity;
                currentBike.transform.localPosition = Vector3.zero;

                RagdollActivator ragdollActivator = currentBike.bikeReferences.ragdollActivator;
                if (ragdollActivator != null)
                {
                    ragdollActivator.ReEnableBike();
                }
            }
        }
    }

}
