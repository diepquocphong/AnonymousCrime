using System;
using GameCreator.Editor.Characters;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Tools.Runtime.Footsteps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameCreator.Tools.Editor.Footsteps
{
    public class FootstepsConfigurationStage : PreviewSceneStage
    {
        private const string HEADER_TEXTURE = RuntimePaths.GIZMOS + "GizmoStateLocomotion.png";
        private const string HEADER_TITLE = "Footsteps Configuration";

        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private string m_AssetPath;
        [NonSerialized] private FootstepsGizmos m_Gizmos;

        // PROPERTIES: ----------------------------------------------------------------------------

        public static bool InStage => Stage != null && StageUtility.GetCurrentStage() == Stage;
        
        [field: NonSerialized] public static FootstepsConfigurationStage Stage { get; private set; }
        [field: NonSerialized] public static GameObject CharacterReference { get; private set; }
        
        public override string assetPath => this.m_AssetPath;
        
        [field: NonSerialized] public GameObject Character { get; private set; }

        public Animator Animator => this.Character != null
            ? this.Character.GetComponent<Animator>()
            : null;

        // EVENTS: --------------------------------------------------------------------------------

        public static Action EventOpenStage;
        public static Action EventCloseStage;

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public static void CreateStage(string footstepSettings)
        {
            if (Stage != null) DestroyImmediate(Stage);
            Stage = CreateInstance<FootstepsConfigurationStage>();

            Stage.m_AssetPath = footstepSettings;
            StageUtility.GoToStage(Stage, true);

            Stage.Character = GetTarget();
            if (Stage.Character == null) return;
            
            Stage.m_Gizmos = FootstepsGizmos.Bind(Stage.Character);
            StageUtility.PlaceGameObjectInCurrentStage(Stage.Character);
        }

        public static void ChangeCharacter(GameObject reference)
        {
            if (!InStage) return;

            CharacterReference = reference;
            FootstepRepository.EditorModelPath = AssetDatabase.GetAssetPath(reference);
            GameObject character = GetTarget();

            if (Stage.Character != null) DestroyImmediate(Stage.Character);
            Stage.Character = character;
            
            Stage.m_Gizmos = FootstepsGizmos.Bind(Stage.Character);
            StageUtility.PlaceGameObjectInCurrentStage(Stage.Character);
        }

        // INITIALIZE METHODS: --------------------------------------------------------------------
        
        protected override GUIContent CreateHeaderContent()
        {
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(HEADER_TEXTURE);
            return new GUIContent(HEADER_TITLE, texture);
        }

        protected override bool OnOpenStage()
        {
            if (!base.OnOpenStage()) return false;
            
            EventOpenStage?.Invoke();
            return true;
        }

        protected override void OnCloseStage()
        {
            this.Character = null;
            this.m_AssetPath = string.Empty;
            DestroyImmediate(this.m_Gizmos);
            
            base.OnCloseStage();
            EventCloseStage?.Invoke();
        }

        protected override void OnFirstTimeOpenStageInSceneView(SceneView sceneView)
        {
            if (this.Character != null)
            {
                Bounds bounds = new Bounds();
                
                Renderer[] renderers = this.Character.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

                sceneView.Frame(bounds);
            }

            sceneView.sceneViewState.showFlares = false;
            sceneView.sceneViewState.alwaysRefresh = false;
            sceneView.sceneViewState.showFog = false;
            sceneView.sceneViewState.showSkybox = false;
            sceneView.sceneViewState.showImageEffects = false;
            sceneView.sceneViewState.showParticleSystems = false;
            sceneView.sceneLighting = false;

            SceneVisibilityManager.instance.DisableAllPicking();
        }

        // PRIVATE STATIC METHODS: ----------------------------------------------------------------

        private static GameObject GetTarget()
        {
            string modelPath = FootstepRepository.EditorModelPath;
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                source = CharacterReference == null
                    ? AssetDatabase.LoadAssetAtPath<GameObject>(CharacterEditor.MODEL_PATH)
                    : CharacterReference;
            }

            if (source == null) return null;
            GameObject target = Instantiate(source);

            if (target == null) return null;
            if (target.TryGetComponent(out Character character))
            {
                if (character.Animim.Animator != null)
                {
                    GameObject child = Instantiate(character.Animim.Animator.gameObject);
                    
                    DestroyImmediate(target);
                    target = child;
                }
            }

            if (target == null) return null;
            target.name = source.name;

            return target;
        }
    }
}