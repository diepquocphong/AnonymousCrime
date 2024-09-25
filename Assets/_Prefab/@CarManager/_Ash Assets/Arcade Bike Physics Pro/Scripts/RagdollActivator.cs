using UnityEngine;
using UnityEngine.Events;


namespace ArcadeBP_Pro
{
    public class RagdollActivator : MonoBehaviour
    {
        [Tooltip("Reference to the ArcadeBikeControllerPro script.")]
        public ArcadeBikeControllerPro bikeController;

        [Tooltip("Reference to the CameraController script.")]
        public CameraController cameraController;

        [Tooltip("Prefab for the Dummy bike.")]
        public GameObject dummyBikePrefab;

        [Tooltip("Prefab for the character ragdoll.")]
        public GameObject characterRagdollPrefab;

        [Tooltip("Animator component of the animated character.")]
        public Animator characterAnimator;

        [Tooltip("Threshold of impact force to activate ragdoll.")]
        public float impactThreshold = 10f;

        [Tooltip("Ignore collisions with the bottom part of the bike collider.")]
        public bool IgnoreBottomCollision = true;

        [Tooltip("Event triggered when the ragdoll is activated.")]
        public UnityEvent onRagdollActivated;

        [Tooltip("Event triggered when the bike is re-enabled.")]
        public UnityEvent onBikeReEnabled;


        private Rigidbody bikeRigidbody;
        private bool isRagdollActivated = false;
        private GameObject bikeRagdollInstance;
        public GameObject characterRagdollInstance { get; private set; }
        private Transform hipTransform;
        private Collider bikeCollider;

        void Start()
        {
            bikeRigidbody = GetComponent<Rigidbody>();
            bikeCollider = bikeController.bikeReferences.collider;

            if (onRagdollActivated == null)
            {
                onRagdollActivated = new UnityEvent();
            }

            if (onBikeReEnabled == null)
            {
                onBikeReEnabled = new UnityEvent();
            }


            onRagdollActivated.AddListener(setCameraTargetToRagdoll);
            onBikeReEnabled.AddListener(resetCameratoBike);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (isRagdollActivated) return;

            Vector3 localContactPoint = bikeController.bikeReferences.LeanTransform.InverseTransformPoint(collision.contacts[0].point);
            Vector3 bikeCenter = bikeController.bikeReferences.LeanTransform.InverseTransformPoint(bikeCollider.bounds.center);

            if (IgnoreBottomCollision)
            {
                if (localContactPoint.y > bikeCenter.y)
                {
                    // Check if the impact is strong enough
                    if (collision.impulse.magnitude / bikeRigidbody.mass > impactThreshold)
                    {
                        ActivateRagdoll();
                    }
                }
            }
            else
            {
                // Check if the impact is strong enough
                if (collision.impulse.magnitude / bikeRigidbody.mass > impactThreshold)
                {
                    ActivateRagdoll();
                }
            }
        }

        void ActivateRagdoll()
        {
            isRagdollActivated = true;

            Transform bikeTransform = bikeController.bikeReferences.LeanTransform;

            // Instantiate the ragdolls
            bikeRagdollInstance = Instantiate(dummyBikePrefab, bikeTransform.position, bikeTransform.rotation);
            characterRagdollInstance = Instantiate(characterRagdollPrefab, bikeTransform.position, bikeTransform.rotation);

            // Match the ragdoll bones' rotations to the character's bones
            Animator ragdollAnimator = characterRagdollInstance.GetComponent<Animator>();
            foreach (HumanBodyBones bone in (HumanBodyBones[])System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;

                Transform characterBoneTransform = characterAnimator.GetBoneTransform(bone);
                Transform ragdollBoneTransform = ragdollAnimator.GetBoneTransform(bone);

                if (characterBoneTransform != null && ragdollBoneTransform != null)
                {
                    ragdollBoneTransform.rotation = characterBoneTransform.rotation;
                }
            }

            // Match velocities and forces
            Rigidbody[] bikeRagdollRigidbodies = bikeRagdollInstance.GetComponentsInChildren<Rigidbody>();
            Rigidbody[] characterRagdollRigidbodies = characterRagdollInstance.GetComponentsInChildren<Rigidbody>();

            Vector3 bikeVelocity = bikeRigidbody.velocity;
            Vector3 bikeAngularVelocity = bikeRigidbody.angularVelocity;

            foreach (Rigidbody rb in bikeRagdollRigidbodies)
            {
                rb.velocity = bikeVelocity;
                rb.angularVelocity = bikeAngularVelocity;
            }

            foreach (Rigidbody rb in characterRagdollRigidbodies)
            {
                rb.velocity = bikeVelocity;
                rb.angularVelocity = bikeAngularVelocity;
            }

            // Deactivate the original bike
            gameObject.SetActive(false);

            // Invoke the event
            onRagdollActivated.Invoke();
        }

        public void ReEnableBike()
        {
            if (!isRagdollActivated) return;

            // Destroy the ragdoll instances
            Destroy(bikeRagdollInstance);
            Destroy(characterRagdollInstance);

            // Re-enable the original bike
            gameObject.SetActive(true);
            bikeController.canAccelerate = true;
            bikeController.bikeAudio.engineSound.pitch = bikeController.bikeAudio.minPitch;
            bikeController.bikeReferences.LeanTransform.localRotation = Quaternion.identity;
            bikeRigidbody.velocity = Vector3.zero;
            bikeRigidbody.angularVelocity = Vector3.zero;

            isRagdollActivated = false;

            // Invoke the event
            onBikeReEnabled.Invoke();
        }

        public void setCameraTargetToRagdoll()
        {
            hipTransform = characterRagdollInstance.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
            cameraController.SetCameratarget(hipTransform, hipTransform);
        }

        public void resetCameratoBike()
        {
            cameraController.resetCameratarget();
        }

        public void ForceActivateRagdoll()
        {
            if (!isRagdollActivated)
            {
                ActivateRagdoll();
            }
        }

        public void ResetBike()
        {
            if (isRagdollActivated)
            {
                ReEnableBike();
            }
        }
    }

}
