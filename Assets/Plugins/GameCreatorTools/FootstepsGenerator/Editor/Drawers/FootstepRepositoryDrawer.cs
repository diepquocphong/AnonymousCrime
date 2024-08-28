using System;
using GameCreator.Editor.Common;
using GameCreator.Runtime.Common;
using GameCreator.Tools.Runtime.Footsteps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCreator.Tools.Editor.Footsteps
{
    [CustomPropertyDrawer(typeof(FootstepRepository))]
    public class FootstepRepositoryDrawer : PropertyDrawer
    {
        private const string USS_PATH = "Assets/Plugins/GameCreatorTools/FootstepsGenerator/Editor/StyleSheets/FootstepRepository";

        private static readonly IIcon ICON_ARROW = new IconDropdown(ColorTheme.Type.TextLight);
        private const string NAME_ARROW = "GC-Tool-Footsteps-Arrow-Down";

        private const float DELTA_TIME = 1f / 30f;
        
        private static readonly EditorCurveBinding CURVE_PHASE_0 = EditorCurveBinding.DiscreteCurve("", typeof(Animator), "Phase-0");
        private static readonly EditorCurveBinding CURVE_PHASE_1 = EditorCurveBinding.DiscreteCurve("", typeof(Animator), "Phase-1");
        private static readonly EditorCurveBinding CURVE_PHASE_2 = EditorCurveBinding.DiscreteCurve("", typeof(Animator), "Phase-2");
        private static readonly EditorCurveBinding CURVE_PHASE_3 = EditorCurveBinding.DiscreteCurve("", typeof(Animator), "Phase-3");

        public static AnimationClip AnimationClip
        {
            get
            {
                string path = EditorPrefs.GetString(FootstepsGizmos.KEY_ANIMATION_CLIP, string.Empty);
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }
            set
            {
                string path = value != null ? AssetDatabase.GetAssetPath(value) : string.Empty;
                EditorPrefs.SetString(FootstepsGizmos.KEY_ANIMATION_CLIP, path);
            }
        }

        public static float Threshold
        {
            get => EditorPrefs.GetFloat(FootstepsGizmos.KEY_THRESHOLD, 0.1f);
            set => EditorPrefs.SetFloat(FootstepsGizmos.KEY_THRESHOLD, value);
        }
        
        // STATIC MEMBERS: ------------------------------------------------------------------------
        
        private static VisualElement Root;
        private static VisualElement Head;
        private static VisualElement Body;
        private static VisualElement Foot;

        private static Button ButtonStage;
        
        private static ObjectField FieldCharacter;
        private static Button ButtonCharacter;

        private static ObjectField FieldAnimation;
        private static Slider SliderAnimation;

        private static Button ButtonMakeGrounded;
        private static Button ButtonMakeAirborne;
        private static Button ButtonMakeCustomCurves;
        private static Button ButtonCalculateCurves;

        private static CurveField FieldCurvePhase0;
        private static CurveField FieldCurvePhase1;
        private static CurveField FieldCurvePhase2;
        private static CurveField FieldCurvePhase3;

        private static Slider SliderThreshold;

        private static SerializedProperty Property;
        
        // PAINT METHODS: -------------------------------------------------------------------------
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            Property = property;
            
            FootstepsConfigurationStage.EventOpenStage -= RefreshStage;
            FootstepsConfigurationStage.EventOpenStage += RefreshStage;
            
            FootstepsConfigurationStage.EventCloseStage -= RefreshStage;
            FootstepsConfigurationStage.EventCloseStage += RefreshStage;
            
            Root = new VisualElement();
            Head = new VisualElement();
            Body = new VisualElement();
            Foot = new VisualElement();
            
            Root.Add(Head);
            Root.Add(Body);
            Root.Add(Foot);
            
            StyleSheet[] styleSheets = StyleSheetUtils.Load(USS_PATH);
            foreach (StyleSheet styleSheet in styleSheets) Root.styleSheets.Add(styleSheet);
            
            ButtonStage = new Button(ToggleStage)
            {
                style = { height = new Length(30f, LengthUnit.Pixel) }
            };
            
            Head.Add(ButtonStage);
            
            FieldCharacter = new ObjectField(string.Empty)
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
                style =
                {
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                }
            };

            ButtonCharacter = new Button(ChangeCharacter)
            {
                text = "Change Character",
                style =
                {
                    height = new Length(22f, LengthUnit.Pixel),
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                }
            };

            PadBox characterBox = new PadBox();
            characterBox.Add(FieldCharacter);
            characterBox.Add(new SpaceSmaller());
            characterBox.Add(ButtonCharacter);

            Image arrowDown = new Image
            {
                image = ICON_ARROW.Texture,
                name = NAME_ARROW
            };
            
            Body.Add(new SpaceSmall());
            Body.Add(characterBox);
            Body.Add(arrowDown);

            FieldAnimation = new ObjectField(string.Empty)
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
                style =
                {
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                },
                value = AnimationClip
            };

            SliderAnimation = new Slider(string.Empty, 0f, 1f)
            {
                showInputField = true,
                style = { marginTop = new Length(5f, LengthUnit.Pixel) }
            };

            FieldAnimation.RegisterValueChangedCallback(changeEvent =>
            {
                AnimationClip = changeEvent.newValue as AnimationClip;
                RefreshStage();
            });
            
            SliderAnimation.RegisterCallback<BlurEvent>(_ =>
            {
                if (!AnimationMode.InAnimationMode()) return;
                AnimationMode.StopAnimationMode();
            });

            SliderAnimation.RegisterValueChangedCallback(changeEvent =>
            {
                if (!FootstepsConfigurationStage.InStage) return;
                
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                }
                
                AnimationMode.BeginSampling();
                
                AnimationMode.SampleAnimationClip(
                    FootstepsConfigurationStage.Stage.Character,
                    FieldAnimation.value as AnimationClip, 
                    changeEvent.newValue
                );

                AnimationMode.EndSampling();
            });

            PadBox animationBox = new PadBox();
            animationBox.Add(FieldAnimation);
            animationBox.Add(SliderAnimation);
            Body.Add(animationBox);
            
            Image arrowDown2 = new Image
            {
                image = ICON_ARROW.Texture,
                name = NAME_ARROW
            };
            
            Body.Add(arrowDown2);
            
            ButtonMakeGrounded = new Button(SetCurvesGrounded)
            {
                text = "Set as Grounded",
                style =
                {
                    height = new Length(22f, LengthUnit.Pixel),
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                }
            };
            
            ButtonMakeAirborne = new Button(SetCurvesAirborne)
            {
                text = "Set as Airborne",
                style =
                {
                    height = new Length(22f, LengthUnit.Pixel),
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                }
            };

            ButtonMakeCustomCurves = new Button(SetCurvesCustom)
            {
                text = "Set Animation Curves",
                style =
                {
                    height = new Length(22f, LengthUnit.Pixel),
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                }
            };

            FieldCurvePhase0 = new CurveField("Phase 0");
            FieldCurvePhase1 = new CurveField("Phase 1");
            FieldCurvePhase2 = new CurveField("Phase 2");
            FieldCurvePhase3 = new CurveField("Phase 3");
            
            Foot.Add(ButtonMakeGrounded);
            Foot.Add(ButtonMakeAirborne);
            Foot.Add(new SpaceSmaller());
            Foot.Add(new TextSeparator("or"));
            Foot.Add(new SpaceSmaller());
            Foot.Add(FieldCurvePhase0);
            Foot.Add(FieldCurvePhase1);
            Foot.Add(FieldCurvePhase2);
            Foot.Add(FieldCurvePhase3);
            Foot.Add(new SpaceSmall());
            Foot.Add(ButtonMakeCustomCurves);
            
            ButtonCalculateCurves = new Button(CalculateHumanoidCurves)
            {
                text = "Automatic Human Footsteps",
                style =
                {
                    height = new Length(22f, LengthUnit.Pixel),
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                }
            };
            
            SliderThreshold = new Slider("Ground Threshold", -1f, 1f)
            {
                value = Threshold,
                showInputField = true,
                style = { marginTop = new Length(5f, LengthUnit.Pixel) }
            };

            SliderThreshold.RegisterValueChangedCallback(changeEvent =>
            {
                Threshold = changeEvent.newValue;
                SceneView.RepaintAll();
            });
            
            PadBox analyzeBox = new PadBox();
            analyzeBox.Add(ButtonCalculateCurves);
            analyzeBox.Add(SliderThreshold);

            Foot.Add(new SpaceSmall());
            Foot.Add(analyzeBox);

            RefreshStage();

            return Root;
        }

        ~FootstepRepositoryDrawer()
        {
            FootstepsConfigurationStage.EventOpenStage -= RefreshStage;
            FootstepsConfigurationStage.EventCloseStage -= RefreshStage;
        }
        
        // PRIVATE METHODS: -----------------------------------------------------------------------
        
        private static void ToggleStage()
        {
            if (FootstepsConfigurationStage.InStage)
            {
                StageUtility.GoToMainStage();
                RefreshStage();
                return;
            }
            
            UnityEngine.Object asset = Property.serializedObject.targetObject;
            string path = AssetDatabase.GetAssetPath(asset);
            
            FootstepsConfigurationStage.CreateStage(path);
        }
        
        private static void ChangeCharacter()
        {
            GameObject character = FieldCharacter?.value as GameObject;
            FootstepsConfigurationStage.ChangeCharacter(character);
        }
        
        private static void RefreshStage()
        {
            bool inFootstepMode = FootstepsConfigurationStage.InStage;
            ButtonStage.text = inFootstepMode
                ? "Close Footsteps Mode" 
                : "Enter Footsteps Mode";

            Color borderColor = inFootstepMode
                ? ColorTheme.Get(ColorTheme.Type.Green)
                : ColorTheme.Get(ColorTheme.Type.Dark);
            
            ButtonStage.style.borderTopColor = borderColor;
            ButtonStage.style.borderBottomColor = borderColor;
            ButtonStage.style.borderLeftColor = borderColor;
            ButtonStage.style.borderRightColor = borderColor;

            ButtonStage.style.color = inFootstepMode
                ? ColorTheme.Get(ColorTheme.Type.Green)
                : ColorTheme.Get(ColorTheme.Type.TextNormal);
            
            Body.SetEnabled(inFootstepMode);
            Foot.SetEnabled(inFootstepMode && FieldAnimation.value != null);
            
            if (inFootstepMode)
            {
                FieldCharacter.value = FootstepsConfigurationStage.CharacterReference;
            }

            AnimationClip animationClip = FieldAnimation.value as AnimationClip;
            SliderAnimation.style.display = animationClip != null
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            SliderAnimation.highValue = animationClip != null
                ? animationClip.length
                : 1f;

            if (FieldAnimation.value != null)
            {
                FieldCurvePhase0.value = AnimationUtility.GetEditorCurve(animationClip, CURVE_PHASE_0);
                FieldCurvePhase1.value = AnimationUtility.GetEditorCurve(animationClip, CURVE_PHASE_1);
                FieldCurvePhase2.value = AnimationUtility.GetEditorCurve(animationClip, CURVE_PHASE_2);
                FieldCurvePhase3.value = AnimationUtility.GetEditorCurve(animationClip, CURVE_PHASE_3);
            }
        }
        
        private static void SetCurvesGrounded()
        {
            AnimationClip animationClip = FieldAnimation.value as AnimationClip;
            if (animationClip == null) return;

            AnimationCurve animationCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(animationClip.length, 1f)
            );

            FieldCurvePhase0.value = animationCurve;
            FieldCurvePhase1.value = animationCurve;
            FieldCurvePhase2.value = animationCurve;
            FieldCurvePhase3.value = animationCurve;

            SetCurvesCustom();
        }
        
        private static void SetCurvesAirborne()
        {
            AnimationClip animationClip = FieldAnimation.value as AnimationClip;
            if (animationClip == null) return;

            AnimationCurve animationCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(animationClip.length, 0f)
            );

            FieldCurvePhase0.value = animationCurve;
            FieldCurvePhase1.value = animationCurve;
            FieldCurvePhase2.value = animationCurve;
            FieldCurvePhase3.value = animationCurve;
            
            SetCurvesCustom();
        }
        
        private static void SetCurvesCustom()
        {
            AnimationClip animationClip = FieldAnimation.value as AnimationClip;
            if (animationClip == null) return;
            
            AnimationUtility.SetEditorCurve(animationClip, CURVE_PHASE_0, FieldCurvePhase0.value);
            AnimationUtility.SetEditorCurve(animationClip, CURVE_PHASE_1, FieldCurvePhase1.value);
            AnimationUtility.SetEditorCurve(animationClip, CURVE_PHASE_2, FieldCurvePhase2.value);
            AnimationUtility.SetEditorCurve(animationClip, CURVE_PHASE_3, FieldCurvePhase3.value);
        }

        private static void CalculateHumanoidCurves()
        {
            if (!FootstepsConfigurationStage.InStage) return;
            
            AnimationClip animationClip = FieldAnimation.value as AnimationClip;
            if (animationClip == null) return;

            Animator animator = FootstepsConfigurationStage.Stage.Animator;
            if (animator == null || !animator.isHuman) return;

            // float groundLevel = GetGroundLevel(animator, animationClip);
            CalculateHumanCurves(animator, animationClip);
        }

        // private static float GetGroundLevel(Animator animator, AnimationClip animationClip)
        // {
        //     float groundLevel = 999f;
        //     float time = 0f;
        //     
        //     AnimationMode.StartAnimationMode();
        //     
        //     while (time <= animationClip.length)
        //     {
        //         AnimationMode.BeginSampling();
        //         
        //         AnimationMode.SampleAnimationClip(
        //             FootstepsConfigurationStage.Stage.Character,
        //             FieldAnimation.value as AnimationClip, 
        //             time
        //         );
        //         
        //         AnimationMode.EndSampling();
        //
        //         float footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y;
        //         float footR = animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y;
        //
        //         if (groundLevel > footL) groundLevel = footL;
        //         if (groundLevel > footR) groundLevel = footR;
        //         
        //         time += DELTA_TIME;
        //     }
        //     
        //     AnimationMode.StopAnimationMode();
        //
        //     return groundLevel;
        // }
        
        private static void CalculateHumanCurves(Animator animator, AnimationClip animationClip)
        {
            float time = 0f;
            
            AnimationMode.StartAnimationMode();

            AnimationCurve phase0 = new AnimationCurve();
            AnimationCurve phase1 = new AnimationCurve();
            
            while (time <= animationClip.length)
            {
                AnimationMode.BeginSampling();
                
                AnimationMode.SampleAnimationClip(
                    FootstepsConfigurationStage.Stage.Character,
                    FieldAnimation.value as AnimationClip, 
                    time
                );
                
                AnimationMode.EndSampling();

                float footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y;
                float footR = animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y;
                
                Keyframe keyframePhase0 = new Keyframe(
                    Math.Min(time, animationClip.length),
                    footL <= Threshold ? 1f : 0f,
                    0f, 0f, 0f, 0f
                );
                
                Keyframe keyframePhase1 = new Keyframe(
                    Math.Min(time, animationClip.length),
                    footR <= Threshold ? 1f : 0f,
                    0f, 0f, 0f, 0f
                );

                phase0.AddKey(keyframePhase0);
                phase1.AddKey(keyframePhase1);
                
                time += DELTA_TIME;
            }
            
            AnimationMode.StopAnimationMode();

            SetCurvesAirborne();

            for (int i = phase0.keys.Length - 2; i >= 1; --i)
            {
                if (Math.Abs(phase0.keys[i].value - phase0.keys[i - 1].value) >= 0.5f) continue;
                if (Math.Abs(phase0.keys[i].value - phase0.keys[i + 1].value) >= 0.5f) continue;
                
                phase0.RemoveKey(i);
            }
            
            for (int i = phase1.keys.Length - 2; i >= 1; --i)
            {
                if (Math.Abs(phase1.keys[i].value - phase1.keys[i - 1].value) >= 0.5f) continue;
                if (Math.Abs(phase1.keys[i].value - phase1.keys[i + 1].value) >= 0.5f) continue;
                
                phase1.RemoveKey(i);
            }
            
            FieldCurvePhase0.value = phase0;
            FieldCurvePhase1.value = phase1;
            
            SetCurvesCustom();
        }
    }
}