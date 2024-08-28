using System;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Tools.Runtime.Footsteps
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    
    [Icon(RuntimePaths.GIZMOS + "GizmoStateLocomotion.png")]
    
    public class FootstepsGizmos : MonoBehaviour
    {
        public const string KEY_ANIMATION_CLIP = "gc:tools:footsteps-animation-clip";
        public const string KEY_THRESHOLD = "gc:tools:footsteps-threshold";

        private const float SIZE = 2f;
        
        public static float Threshold
        {
            get
            {
                #if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetFloat(KEY_THRESHOLD, 0.1f);
                #else
                return default;
                #endif
            }
            set
            {
                #if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetFloat(KEY_THRESHOLD, value);
                #endif
            }
        }

        // MEMBERS: -------------------------------------------------------------------------------
        
        [NonSerialized] private Animator m_Animator;
        
        // PUBLIC METHODS: ------------------------------------------------------------------------

        public static FootstepsGizmos Bind(GameObject target)
        {
            FootstepsGizmos component = target.AddComponent<FootstepsGizmos>();
            
            component.hideFlags = HideFlags.DontSave;
            component.m_Animator = component.GetComponentInChildren<Animator>();
            
            return component;
        }

        // GIZMOS: --------------------------------------------------------------------------------
        
        #if UNITY_EDITOR
        
        [UnityEditor.DrawGizmo(
            UnityEditor.GizmoType.InSelectionHierarchy | 
            UnityEditor.GizmoType.NotInSelectionHierarchy
        )]
        private static void DrawGizmos(FootstepsGizmos component, UnityEditor.GizmoType gizmoType)
        {
            if (component.m_Animator == null) return;
            
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawCube(Vector3.up * Threshold, Vector3Plane.NormalUp * SIZE);
            
            Gizmos.color = new Color(1f, 0f, 0f, 1f);
            Gizmos.DrawWireCube(Vector3.up * Threshold, Vector3Plane.NormalUp * SIZE);
            
            if (!component.m_Animator.isHuman) return;

            Transform footL = component.m_Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform footR = component.m_Animator.GetBoneTransform(HumanBodyBones.RightFoot);

            if (footL != null)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.75f);
                
                GizmosExtension.Circle(
                    footL.position, 0.25f, 
                    Vector3.up, footL.position.y <= Threshold
                );
            }
            
            if (footR != null)
            {
                Gizmos.color = new Color(0f, 0f, 1f, 0.75f);
                
                GizmosExtension.Circle(
                    footR.position, 0.25f, 
                    Vector3.up, footR.position.y <= Threshold
                );
            }
        }
        
        #endif
    }
}