using UnityEngine;
using UnityEditor;
using System.Collections.Generic;


namespace ArcadeBP_Pro
{
    public class RagdollSetupTool : EditorWindow
    {
        public RagdollReference sourceRagdoll;
        public Animator targetAnimator;

        private Dictionary<Transform, (Vector3 axis, Vector3 swingAxis, float lowTwist, float highTwist, float swing1, float swing2)> boneConfigurations;

        [MenuItem("Tools/Ash Tools/Arcade Bike Physics Pro/Ragdoll Setup Tool")]
        public static void ShowWindow()
        {
            GetWindow<RagdollSetupTool>("Ragdoll Setup Tool");
        }

        private void OnGUI()
        {
            GUILayout.Label("Ragdoll Setup", EditorStyles.boldLabel);

            sourceRagdoll = (RagdollReference)EditorGUILayout.ObjectField("Source Ragdoll", sourceRagdoll, typeof(RagdollReference), true);
            targetAnimator = (Animator)EditorGUILayout.ObjectField("Target Animator", targetAnimator, typeof(Animator), true);

            if (GUILayout.Button("Apply Ragdoll Configuration"))
            {
                if (sourceRagdoll != null && targetAnimator != null)
                {
                    ApplyRagdollConfiguration(sourceRagdoll, targetAnimator);
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Please assign both Source Ragdoll and Target Animator.", "OK");
                }
            }
        }

        private void InitializeBoneConfigurations(Animator targetAnimator)
        {
            boneConfigurations = new Dictionary<Transform, (Vector3, Vector3, float, float, float, float)>
        {
            { targetAnimator.GetBoneTransform(HumanBodyBones.Hips), (Vector3.right, Vector3.forward, -45f, 20f, 20f, 20f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), (Vector3.right, Vector3.forward, -45f, 90f, 75f, 30f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg), (Vector3.right, Vector3.forward, -45f, 90f, 75f, 30f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), (Vector3.right, Vector3.forward, 0f, 120f, 0f, 0f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg), (Vector3.right, Vector3.forward, 0f, 120f, 0f, 0f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm), (Vector3.right, Vector3.up, -90f, 90f, 90f, 90f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm), (Vector3.right, Vector3.up, -90f, 90f, 90f, 90f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm), (Vector3.right, Vector3.up, 0f, 150f, 0f, 0f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm), (Vector3.right, Vector3.up, 0f, 150f, 0f, 0f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.Head), (Vector3.right, Vector3.forward, -45f, 45f, 45f, 45f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.Spine), (Vector3.right, Vector3.forward, -30f, 30f, 30f, 30f) },
            { targetAnimator.GetBoneTransform(HumanBodyBones.Chest), (Vector3.right, Vector3.forward, -30f, 30f, 30f, 30f) }
        };
        }

        private void ApplyRagdollConfiguration(RagdollReference sourceRagdoll, Animator targetAnimator)
        {
            targetAnimator.transform.root.name = "Ragdoll_" + targetAnimator.transform.root.name;

            InitializeBoneConfigurations(targetAnimator);

            Dictionary<Rigidbody, Rigidbody> rigidbodyMap = new Dictionary<Rigidbody, Rigidbody>();

            // Apply configuration to each bone and store the resulting rigidbodies in a map
            ApplyRagdollPart(sourceRagdoll.hips, targetAnimator.GetBoneTransform(HumanBodyBones.Hips), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.leftUpperLeg, targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.leftLowerLeg, targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), rigidbodyMap);
            ApplyHandAndFootParts(sourceRagdoll.leftFoot, targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.rightUpperLeg, targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.rightLowerLeg, targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg), rigidbodyMap);
            ApplyHandAndFootParts(sourceRagdoll.rightFoot, targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.chest, targetAnimator.GetBoneTransform(HumanBodyBones.Chest), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.leftUpperArm, targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.leftLowerArm, targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm), rigidbodyMap);
            ApplyHandAndFootParts(sourceRagdoll.leftHand, targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.rightUpperArm, targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.rightLowerArm, targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm), rigidbodyMap);
            ApplyHandAndFootParts(sourceRagdoll.rightHand, targetAnimator.GetBoneTransform(HumanBodyBones.RightHand), rigidbodyMap);
            ApplyRagdollPart(sourceRagdoll.head, targetAnimator.GetBoneTransform(HumanBodyBones.Head), rigidbodyMap);

            // Set connected bodies for the character joints
            SetConnectedBodies(sourceRagdoll.hips, targetAnimator.GetBoneTransform(HumanBodyBones.Hips), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.leftUpperLeg, targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.leftLowerLeg, targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.leftFoot, targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.rightUpperLeg, targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.rightLowerLeg, targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.rightFoot, targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.chest, targetAnimator.GetBoneTransform(HumanBodyBones.Chest), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.leftUpperArm, targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.leftLowerArm, targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.leftHand, targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.rightUpperArm, targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.rightLowerArm, targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.rightHand, targetAnimator.GetBoneTransform(HumanBodyBones.RightHand), rigidbodyMap);
            SetConnectedBodies(sourceRagdoll.head, targetAnimator.GetBoneTransform(HumanBodyBones.Head), rigidbodyMap);

            // Rename bones
            RenameBones(targetAnimator);

            targetAnimator.gameObject.AddComponent<ColliderAdjustmentTool>();

            RagdollReference ragdollReference = targetAnimator.gameObject.AddComponent<RagdollReference>();

            ragdollReference.hips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips).GetComponent<Rigidbody>();
            ragdollReference.leftUpperLeg = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).GetComponent<Rigidbody>();
            ragdollReference.leftLowerLeg = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).GetComponent<Rigidbody>();
            ragdollReference.leftFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).GetComponent<Rigidbody>();
            ragdollReference.rightUpperLeg = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg).GetComponent<Rigidbody>();
            ragdollReference.rightLowerLeg = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg).GetComponent<Rigidbody>();
            ragdollReference.rightFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot).GetComponent<Rigidbody>();
            ragdollReference.chest = targetAnimator.GetBoneTransform(HumanBodyBones.Chest).GetComponent<Rigidbody>();
            ragdollReference.leftUpperArm = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm).GetComponent<Rigidbody>();
            ragdollReference.leftLowerArm = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm).GetComponent<Rigidbody>();
            ragdollReference.leftHand = targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand).GetComponent<Rigidbody>();
            ragdollReference.rightUpperArm = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm).GetComponent<Rigidbody>();
            ragdollReference.rightLowerArm = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm).GetComponent<Rigidbody>();
            ragdollReference.rightHand = targetAnimator.GetBoneTransform(HumanBodyBones.RightHand).GetComponent<Rigidbody>();
            ragdollReference.head = targetAnimator.GetBoneTransform(HumanBodyBones.Head).GetComponent<Rigidbody>();

            EditorUtility.DisplayDialog("Success", "Ragdoll configuration applied successfully.", "OK");
        }

        private void ApplyRagdollPart(Rigidbody sourceRb, Transform targetTransform, Dictionary<Rigidbody, Rigidbody> rigidbodyMap)
        {
            if (sourceRb == null || targetTransform == null)
                return;

            Rigidbody targetRb = targetTransform.gameObject.AddComponent<Rigidbody>();
            CopyComponentValues(sourceRb, targetRb);
            rigidbodyMap[sourceRb] = targetRb;

            Collider sourceCollider = sourceRb.GetComponent<Collider>();
            if (sourceCollider != null)
            {
                Collider targetCollider = null;
                if (sourceCollider is BoxCollider sourceBoxCollider)
                {
                    targetCollider = targetTransform.gameObject.AddComponent<BoxCollider>();
                    ((BoxCollider)targetCollider).center = sourceBoxCollider.center;
                    ((BoxCollider)targetCollider).size = sourceBoxCollider.size;
                }
                else if (sourceCollider is SphereCollider sourceSphereCollider)
                {
                    targetCollider = targetTransform.gameObject.AddComponent<SphereCollider>();
                    ((SphereCollider)targetCollider).center = sourceSphereCollider.center;
                    ((SphereCollider)targetCollider).radius = sourceSphereCollider.radius;
                }
                else if (sourceCollider is CapsuleCollider sourceCapsuleCollider)
                {
                    targetCollider = targetTransform.gameObject.AddComponent<CapsuleCollider>();
                    ((CapsuleCollider)targetCollider).center = sourceCapsuleCollider.center;
                    ((CapsuleCollider)targetCollider).radius = sourceCapsuleCollider.radius;
                    ((CapsuleCollider)targetCollider).height = sourceCapsuleCollider.height;
                    ((CapsuleCollider)targetCollider).direction = sourceCapsuleCollider.direction;
                }
                else if (sourceCollider is MeshCollider sourceMeshCollider)
                {
                    targetCollider = targetTransform.gameObject.AddComponent<MeshCollider>();
                    ((MeshCollider)targetCollider).sharedMesh = sourceMeshCollider.sharedMesh;
                    ((MeshCollider)targetCollider).convex = sourceMeshCollider.convex;
                }

                if (targetCollider != null)
                {
                    targetCollider.isTrigger = sourceCollider.isTrigger;
                }
            }

            CharacterJoint sourceJoint = sourceRb.GetComponent<CharacterJoint>();
            if (sourceJoint != null)
            {
                CharacterJoint targetJoint = targetTransform.gameObject.AddComponent<CharacterJoint>();
                CopyCharacterJointValues(sourceJoint, targetJoint);

                if (boneConfigurations.TryGetValue(targetTransform, out var config))
                {
                    ConfigureJoint(targetJoint, rigidbodyMap.ContainsKey(sourceJoint.connectedBody) ? rigidbodyMap[sourceJoint.connectedBody] : null, config.axis, config.swingAxis, config.lowTwist, config.highTwist, config.swing1, config.swing2);
                }
                else
                {
                    ConfigureJoint(targetJoint, rigidbodyMap.ContainsKey(sourceJoint.connectedBody) ? rigidbodyMap[sourceJoint.connectedBody] : null, sourceJoint.axis, sourceJoint.swingAxis, sourceJoint.lowTwistLimit.limit, sourceJoint.highTwistLimit.limit, sourceJoint.swing1Limit.limit, sourceJoint.swing2Limit.limit);
                }
            }
        }

        private void SetConnectedBodies(Rigidbody sourceRb, Transform targetTransform, Dictionary<Rigidbody, Rigidbody> rigidbodyMap)
        {
            if (sourceRb == null || targetTransform == null)
                return;

            CharacterJoint sourceJoint = sourceRb.GetComponent<CharacterJoint>();
            CharacterJoint targetJoint = targetTransform.GetComponent<CharacterJoint>();

            if (sourceJoint != null && targetJoint != null)
            {
                if (rigidbodyMap.TryGetValue(sourceJoint.connectedBody, out Rigidbody connectedBody))
                {
                    targetJoint.connectedBody = connectedBody;

                    if (boneConfigurations.TryGetValue(targetTransform, out var config))
                    {
                        ConfigureJoint(targetJoint, connectedBody, config.axis, config.swingAxis, config.lowTwist, config.highTwist, config.swing1, config.swing2);
                    }
                    else
                    {
                        ConfigureJoint(targetJoint, connectedBody, sourceJoint.axis, sourceJoint.swingAxis, sourceJoint.lowTwistLimit.limit, sourceJoint.highTwistLimit.limit, sourceJoint.swing1Limit.limit, sourceJoint.swing2Limit.limit);
                    }
                }
            }
        }

        private void ConfigureJoint(CharacterJoint joint, Rigidbody connectedBody, Vector3 axis, Vector3 swingAxis, float lowTwist, float highTwist, float swing1, float swing2)
        {
            joint.connectedBody = connectedBody;
            joint.axis = axis;
            joint.swingAxis = swingAxis;

            joint.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = highTwist };
            joint.swing1Limit = new SoftJointLimit { limit = swing1 };
            joint.swing2Limit = new SoftJointLimit { limit = swing2 };

            joint.twistLimitSpring = new SoftJointLimitSpring { spring = 0, damper = 0 };
            joint.swingLimitSpring = new SoftJointLimitSpring { spring = 0, damper = 0 };
        }

        private void CopyComponentValues(Rigidbody sourceRb, Rigidbody targetRb)
        {
            targetRb.mass = sourceRb.mass;
            targetRb.drag = sourceRb.drag;
            targetRb.angularDrag = sourceRb.angularDrag;
            targetRb.useGravity = sourceRb.useGravity;
            targetRb.isKinematic = sourceRb.isKinematic;
            targetRb.interpolation = sourceRb.interpolation;
            targetRb.collisionDetectionMode = sourceRb.collisionDetectionMode;
            targetRb.constraints = sourceRb.constraints;
        }

        private void CopyCharacterJointValues(CharacterJoint sourceJoint, CharacterJoint targetJoint)
        {
            targetJoint.anchor = sourceJoint.anchor;
            targetJoint.axis = sourceJoint.axis;
            targetJoint.swingAxis = sourceJoint.swingAxis;
            targetJoint.lowTwistLimit = sourceJoint.lowTwistLimit;
            targetJoint.highTwistLimit = sourceJoint.highTwistLimit;
            targetJoint.swing1Limit = sourceJoint.swing1Limit;
            targetJoint.swing2Limit = sourceJoint.swing2Limit;
            targetJoint.breakForce = sourceJoint.breakForce;
            targetJoint.breakTorque = sourceJoint.breakTorque;
            targetJoint.enablePreprocessing = sourceJoint.enablePreprocessing;
        }

        private void RenameBones(Animator animator)
        {
            Transform[] allTransforms = animator.GetComponentsInChildren<Transform>();
            string[] nonMirrorableBones = new string[] { "Spine", "Hips", "Head", "Neck" };

            foreach (Transform t in allTransforms)
            {
                bool isNonMirrorable = false;

                foreach (string bone in nonMirrorableBones)
                {
                    if (t.name.Contains(bone))
                    {
                        isNonMirrorable = true;
                        break;
                    }
                }

                if (!isNonMirrorable)
                {
                    if (t.name.Contains("Hand") || t.name.Contains("Foot") || t.name.Contains("UpperArm") || t.name.Contains("LowerArm") || t.name.Contains("UpperLeg") || t.name.Contains("LowerLeg"))
                    {
                        if (t.parent.name.Contains("Left"))
                        {
                            t.name = "Left_" + t.name;
                        }
                        else if (t.parent.name.Contains("Right"))
                        {
                            t.name = "Right_" + t.name;
                        }
                    }
                }
            }
        }

        private void ApplyHandAndFootParts(Rigidbody sourceRb, Transform targetTransform, Dictionary<Rigidbody, Rigidbody> rigidbodyMap)
        {
            if (sourceRb == null || targetTransform == null)
                return;

            // Apply the main part
            ApplyRagdollPart(sourceRb, targetTransform, rigidbodyMap);

            // Check for child colliders
            foreach (Transform child in sourceRb.transform)
            {
                Collider sourceCollider = child.GetComponent<Collider>();
                if (sourceCollider != null)
                {
                    // Create a new GameObject under the targetTransform to hold the collider
                    GameObject colliderObject = new GameObject(child.name);
                    colliderObject.transform.SetParent(targetTransform);
                    colliderObject.transform.localPosition = child.localPosition;
                    colliderObject.transform.localRotation = child.localRotation;

                    // Copy the collider
                    Collider targetCollider = null;
                    if (sourceCollider is BoxCollider sourceBoxCollider)
                    {
                        targetCollider = colliderObject.AddComponent<BoxCollider>();
                        ((BoxCollider)targetCollider).center = sourceBoxCollider.center;
                        ((BoxCollider)targetCollider).size = sourceBoxCollider.size;
                    }
                    else if (sourceCollider is SphereCollider sourceSphereCollider)
                    {
                        targetCollider = colliderObject.AddComponent<SphereCollider>();
                        ((SphereCollider)targetCollider).center = sourceSphereCollider.center;
                        ((SphereCollider)targetCollider).radius = sourceSphereCollider.radius;
                    }
                    else if (sourceCollider is CapsuleCollider sourceCapsuleCollider)
                    {
                        targetCollider = colliderObject.AddComponent<CapsuleCollider>();
                        ((CapsuleCollider)targetCollider).center = sourceCapsuleCollider.center;
                        ((CapsuleCollider)targetCollider).radius = sourceCapsuleCollider.radius;
                        ((CapsuleCollider)targetCollider).height = sourceCapsuleCollider.height;
                        ((CapsuleCollider)targetCollider).direction = sourceCapsuleCollider.direction;
                    }
                    else if (sourceCollider is MeshCollider sourceMeshCollider)
                    {
                        targetCollider = colliderObject.AddComponent<MeshCollider>();
                        ((MeshCollider)targetCollider).sharedMesh = sourceMeshCollider.sharedMesh;
                        ((MeshCollider)targetCollider).convex = sourceMeshCollider.convex;
                    }

                    if (targetCollider != null)
                    {
                        targetCollider.isTrigger = sourceCollider.isTrigger;
                    }
                }
            }
        }
    }

}
