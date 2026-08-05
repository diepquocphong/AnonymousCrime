using System;
using System.Collections.Generic;
using UnityEngine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [HelpURL("https://niam.gitbook.io/niam-docs/game-creator-2/tactile")]
    [AddComponentMenu("Game Creator/UI/Tactile Control")]
    [Icon(RuntimePaths.PACKAGES + "Tactile/Editor/Gizmos/GizmoTactile.png")]

    [RequireComponent(typeof(RectTransform))]
    public class TactileControl : MonoBehaviour
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private bool m_Interactable = true;
        [SerializeField] private UniqueID m_UniqueID = new UniqueID();

        [SerializeReference] private TTouchableArea m_TouchableArea = new TouchableAreaTransformRect();
        [SerializeReference] private TControlType m_ControlType = new ControlTypeDefaultNone();

        // PROPERTIES: ----------------------------------------------------------------------------

        public bool Interactable
        {
            get => this.m_Interactable && this.isActiveAndEnabled;

            set
            {
                if (this.m_Interactable && !value) this.ForceEndInteractAll();
                this.m_Interactable = value;
            }
        }

        public TControlType ControlType => this.m_ControlType;

        public TTouchableArea TouchableArea => this.m_TouchableArea;

        public string UniqueID => this.m_UniqueID.Get.String;

        public Args Args { get; private set; }

        // MONOBEHAVIOR: --------------------------------------------------------------------------

        private void Awake()
        {
            this.m_TouchableArea ??= new TouchableAreaTransformRect();
            
            this.InitializeUniqueID();

            this.Args = new Args(this, this);
            this.TouchableArea.Initialize(this);
            this.ControlType.Initialize(this);

            EventInstantiateAny?.Invoke(this.m_UniqueID.Get);
        }
        
        private void Start()
        {
            this.ControlType.Start();
        }

        private void Update()
        {
            this.ControlType.Update();
            this.m_TouchableArea.Update();
        }

        private void OnEnable() 
        {
            TactileManager.Register(this);
            this.ControlType.Enable();
        }

        private void OnDisable()
        {
            this.ForceEndInteractAll();
            
            this.ControlType.Disable();
            TactileManager.Unregister(this);
        }

        private void OnDestroy() 
        {
            this.ControlType.Destroy();
            s_Tactiles.Remove(this.m_UniqueID.Get);
        }

        private void OnApplicationFocus(bool focusStatus) 
        {
            if (!focusStatus) this.ForceEndInteractAll();
        }

        // INTERACTION: ---------------------------------------------------------------------------

        internal void OnInteractBegin(Touch touch)
        {
            if (!this.Interactable) return;
            this.BeginInteract(touch);
        }

        internal void OnInteractDrag(Touch touch)
        {
            if (!this.Interactable) return;

            this.TouchableArea.InteractBeforeDrag(touch);
            if (!this.TouchableArea.ContainsFinger(touch.finger)) return;

            this.ControlType.InteractBeforeDrag(touch);
            this.TouchableArea.InteractAfterDrag(touch);
            this.ControlType.InteractAfterDrag(touch);
        }

        internal void OnInteractEnd(Touch touch)
        {
            if (!this.Interactable) return;
            this.EndInteract(touch);
        }

        // INTERNAL METHODS: ----------------------------------------------------------------------

        internal void BeginInteract(Touch touch)
        {
            if (this.TouchableArea.ContainsFinger(touch.finger)) return;

            this.TouchableArea.InteractBeforeBegin(touch);
            this.ControlType.InteractBeforeBegin(touch);

            this.TouchableArea.InteractAfterBegin(touch);
            this.ControlType.InteractAfterBegin(touch);
        }

        internal void EndInteract(Touch touch)
        {
            if (!this.TouchableArea.ContainsFinger(touch.finger)) return;

            this.TouchableArea.InteractBeforeEnd(touch);
            this.ControlType.InteractBeforeEnd(touch);

            this.TouchableArea.InteractAfterEnd(touch);
            this.ControlType.InteractAfterEnd(touch);
        }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void InitializeUniqueID()
        {
            s_Tactiles[this.m_UniqueID.Get] = this;
        }

        public void ChangeUniqueID(IdString newId)
        {
            IdString prevId = this.m_UniqueID.Get;
            this.m_UniqueID.Set = newId;
            
            s_Tactiles.Remove(prevId);
            s_Tactiles[newId] = this;
        }

        public void ForceEndInteractAll()
        {
            if (!this.TouchableArea.interaction.IsPressed) return;
            
            for (int i = 0; i < this.TouchableArea.fingerCount; i++)
            {
                this.EndInteract(this.TouchableArea.GetFinger(i).currentTouch);
            }
        }

        // GIZMOS: --------------------------------------------------------------------------------

        #if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            if (!UnityEditor.SceneView.currentDrawingSceneView) return;

            using var serializedObject = new UnityEditor.SerializedObject(this);
            if (serializedObject.FindProperty("m_TouchableArea").isExpanded)
                this.TouchableArea?.DrawGizmos(this);

            if (serializedObject.FindProperty("m_ControlType").isExpanded)
                this.ControlType?.DrawGizmos(this.transform);
        }

        #endif

        // STATIC: --------------------------------------------------------------------------------

        internal static Dictionary<IdString, TactileControl> s_Tactiles = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            var tactiles = FindObjectsByType<TactileControl>(
                FindObjectsInactive.Include, FindObjectsSortMode.None
            );

            for (int i = 0; i < tactiles.Length; i++)
            {
                if (tactiles[i].isActiveAndEnabled) continue;
                tactiles[i].InitializeUniqueID();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemsInit()
        {
            s_Tactiles.Clear();
        }

        public static event Action<IdString> EventInstantiateAny;

        public static TactileControl GetControlByID(string tactileControlID)
        {
            return GetControlByID(new IdString(tactileControlID));
        }

        public static TactileControl GetControlByID(IdString tactileControlID)
        {
            return s_Tactiles.TryGetValue(tactileControlID, out TactileControl tactileControl)
                ? tactileControl : null;
        }

    }
}