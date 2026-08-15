using System;
using System.Reflection;
using FranklinGame.Melee;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Melee;
using GameCreator.Runtime.Stats;
using GameCreator.Runtime.VisualScripting;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.Melee.Editor
{
    /// <summary>
    /// Builds the GC2 assets from individual Fighting Animset clips and embeds the player/UI
    /// wiring in the two project prefabs supplied for this integration.
    /// </summary>
    [InitializeOnLoad]
    public static class FranklinMeleeInstaller
    {
        private const string ROOT = "Assets/MeleeSystemGC2";
        private const string PUNCHES_PATH = ROOT + "/Animations/KB_Punches.fbx";
        private const string KICKS_PATH = ROOT + "/Animations/KB_Kicks.fbx";
        private const string HITS_PATH = ROOT + "/Animations/KB_Hits.fbx";
        private const string MOVEMENT_PATH = ROOT + "/Animations/KB_Movement.fbx";
        private const string FIGHT_SPRITE_PATH = ROOT + "/UI/fight-button.png";
        private const string SIDESTEP_LEFT_SPRITE_PATH = ROOT + "/UI/sidestep-left.png";
        private const string SIDESTEP_RIGHT_SPRITE_PATH = ROOT + "/UI/sidestep-right.png";
        private const string STRIKER_PREFAB_PATH = ROOT + "/Striker.prefab";
        private const string PLAYER_PREFAB_PATH = "Assets/Prefab/Player.prefab";
        private const string NPC_PREFAB_PATH = "Assets/Prefab/NPC.prefab";
        private const string CANVAS_PREFAB_PATH = "Assets/Prefab/CanvasPlayerControl.prefab";
        private const string HP_ATTRIBUTE_PATH =
            "Assets/Plugins/GameCreator/Installs/Stats.Classes@1.3.7/_Stats/Health/HP.asset";

        private const string COMBOS_PATH = ROOT + "/Franklin Unarmed Combos.asset";
        private const string WEAPON_PATH = ROOT + "/Franklin Unarmed Weapon.asset";
        private const string STATES_FOLDER = ROOT + "/States";
        private const string UNARMED_LOCOMOTION_STATE_PATH =
            STATES_FOLDER + "/Franklin Unarmed Combat Locomotion.asset";
        private const string BASIC_LOCOMOTION_CONTROLLER_PATH =
            "Assets/Plugins/GameCreator/Packages/Core/Runtime/Characters/Assets/" +
            "Controllers/BasicLocomotion.controller";
        private const string REACTIONS_FOLDER = ROOT + "/Reactions";
        private const string HIT_REACTION_PATH =
            REACTIONS_FOLDER + "/Franklin Hit Reactions.asset";
        private const string AUDIO_ROOT = ROOT + "/Audio";
        private const string FIGHT_BUTTON_NAME = "Fight";
        private const string SIDESTEP_LEFT_BUTTON_NAME = "Sidestep Left";
        private const string SIDESTEP_RIGHT_BUTTON_NAME = "Sidestep Right";
        private const string ON_FOOT_GROUP = "Franklin On Foot Controls";
        private const string ANIMATION_BRIDGE_TYPE =
            "FranklinGame.Animations.FranklinAnimationBridge";

        private const int COMBAT_LOCOMOTION_LAYER = 1;
        private const int FAST_LOCOMOTION_LAYER = 2;
        private const float COMBAT_LOCOMOTION_IDLE_TIMEOUT = 2f;

        private const string LEFT_HAND_ID = "franklin-left-hand";
        private const string RIGHT_HAND_ID = "franklin-right-hand";
        private const string LEFT_FOOT_ID = "franklin-left-foot";
        private const string RIGHT_FOOT_ID = "franklin-right-foot";

        private const string LEFT_HAND_STRIKER = "Melee Striker - Left Hand";
        private const string RIGHT_HAND_STRIKER = "Melee Striker - Right Hand";
        private const string LEFT_FOOT_STRIKER = "Melee Striker - Left Foot";
        private const string RIGHT_FOOT_STRIKER = "Melee Striker - Right Foot";

        private const int MELEE_TARGET_LAYERS = (1 << 0) | (1 << 11);

        private readonly struct SkillDefinition
        {
            public readonly string AssetName;
            public readonly string Title;
            public readonly string Description;
            public readonly string ModelPath;
            public readonly string ClipName;
            public readonly string StrikerId;
            public readonly MeleeKey Key;
            public readonly float Damage;
            public readonly float Power;
            public readonly float PoiseDamage;

            public SkillDefinition(
                string assetName,
                string title,
                string description,
                string modelPath,
                string clipName,
                string strikerId,
                MeleeKey key,
                float damage,
                float power,
                float poiseDamage)
            {
                this.AssetName = assetName;
                this.Title = title;
                this.Description = description;
                this.ModelPath = modelPath;
                this.ClipName = clipName;
                this.StrikerId = strikerId;
                this.Key = key;
                this.Damage = damage;
                this.Power = power;
                this.PoiseDamage = poiseDamage;
            }
        }

        private static readonly SkillDefinition[] SKILLS =
        {
            new(
                "01 Jab Left",
                "Jab Left",
                "Single left jab from Fighting Animset Pro.",
                PUNCHES_PATH,
                "KB_p_Jab_L_1",
                LEFT_HAND_ID,
                MeleeKey.A,
                7f,
                2f,
                1f
            ),
            new(
                "02 Jab Right",
                "Jab Right",
                "Single right jab from Fighting Animset Pro.",
                PUNCHES_PATH,
                "KB_p_Jab_R_1",
                RIGHT_HAND_ID,
                MeleeKey.B,
                8f,
                2.5f,
                1.25f
            ),
            new(
                "03 Hook Left",
                "Hook Left",
                "Single left hook from Fighting Animset Pro.",
                PUNCHES_PATH,
                "KB_p_Hook_L",
                LEFT_HAND_ID,
                MeleeKey.C,
                11f,
                5f,
                2.5f
            ),
            new(
                "04 Front Kick Right",
                "Front Kick Right",
                "Single right front kick from Fighting Animset Pro.",
                KICKS_PATH,
                "KB_p_MidKickFront_R",
                RIGHT_FOOT_ID,
                MeleeKey.D,
                17f,
                8f,
                4f
            ),
            new(
                "05 Hook Right",
                "Hook Right",
                "Single right hook from Fighting Animset Pro.",
                PUNCHES_PATH,
                "KB_p_Hook_R",
                RIGHT_HAND_ID,
                MeleeKey.E,
                12f,
                5.5f,
                2.75f
            ),
            new(
                "06 Uppercut Left",
                "Uppercut Left",
                "Single left uppercut from Fighting Animset Pro.",
                PUNCHES_PATH,
                "KB_p_Uppercut_L",
                LEFT_HAND_ID,
                MeleeKey.F,
                14f,
                6.5f,
                3f
            ),
            new(
                "07 Uppercut Right",
                "Uppercut Right",
                "Single right uppercut from Fighting Animset Pro.",
                PUNCHES_PATH,
                "KB_p_Uppercut_R",
                RIGHT_HAND_ID,
                MeleeKey.G,
                15f,
                7f,
                3.25f
            ),
            new(
                "08 Front Kick Left",
                "Front Kick Left",
                "Single left front kick from Fighting Animset Pro.",
                KICKS_PATH,
                "KB_p_MidKickFront_L",
                LEFT_FOOT_ID,
                MeleeKey.H,
                16f,
                7.5f,
                3.75f
            )
        };

        private static readonly string[] SWING_AUDIO_PATHS =
        {
            AUDIO_ROOT + "/Swings/soft-swing-01.wav",
            AUDIO_ROOT + "/Swings/soft-swing-02.wav",
            AUDIO_ROOT + "/Swings/soft-swing-03.wav",
            AUDIO_ROOT + "/Swings/soft-swing-04.wav",
            AUDIO_ROOT + "/Swings/soft-swing-05.wav",
            AUDIO_ROOT + "/Swings/soft-swing-06.wav",
            AUDIO_ROOT + "/Swings/soft-swing-07.wav",
            AUDIO_ROOT + "/Swings/soft-swing-08.wav"
        };

        private static readonly string[] HIT_AUDIO_PATHS =
        {
            AUDIO_ROOT + "/Impacts/soft-impact-01.wav",
            AUDIO_ROOT + "/Impacts/soft-impact-02.wav",
            AUDIO_ROOT + "/Impacts/soft-impact-03.wav",
            AUDIO_ROOT + "/Impacts/soft-impact-04.wav",
            AUDIO_ROOT + "/Impacts/soft-impact-05.wav",
            AUDIO_ROOT + "/Impacts/soft-impact-06.wav",
            AUDIO_ROOT + "/Impacts/soft-impact-07.wav",
            AUDIO_ROOT + "/Impacts/soft-impact-08.wav"
        };

        private static readonly string[] BLOCK_AUDIO_PATHS =
        {
            AUDIO_ROOT + "/Blocks/soft-block-01.wav",
            AUDIO_ROOT + "/Blocks/soft-block-02.wav",
            AUDIO_ROOT + "/Blocks/soft-block-03.wav",
            AUDIO_ROOT + "/Blocks/soft-block-04.wav"
        };

        private static readonly string[] VOICE_AUDIO_PATHS =
        {
            AUDIO_ROOT + "/Voices/soft-effort-01.wav",
            AUDIO_ROOT + "/Voices/soft-effort-02.wav",
            AUDIO_ROOT + "/Voices/soft-effort-03.wav",
            AUDIO_ROOT + "/Voices/soft-effort-06.wav",
            AUDIO_ROOT + "/Voices/soft-effort-04.wav",
            AUDIO_ROOT + "/Voices/soft-effort-05.wav",
            AUDIO_ROOT + "/Voices/soft-effort-06.wav",
            AUDIO_ROOT + "/Voices/soft-effort-05.wav"
        };

        private static readonly string[] PAIN_AUDIO_PATHS =
        {
            AUDIO_ROOT + "/Pain/soft-pain-01.wav",
            AUDIO_ROOT + "/Pain/soft-pain-02.wav",
            AUDIO_ROOT + "/Pain/soft-pain-03.wav",
            AUDIO_ROOT + "/Pain/soft-pain-04.wav",
            AUDIO_ROOT + "/Pain/soft-pain-05.wav",
            AUDIO_ROOT + "/Pain/soft-pain-06.wav",
            AUDIO_ROOT + "/Pain/soft-pain-07.wav",
            AUDIO_ROOT + "/Pain/soft-pain-08.wav"
        };

        static FranklinMeleeInstaller()
        {
            EditorApplication.delayCall += InstallIfNeeded;
        }

        [MenuItem("Tools/Franklin Game/Melee GC2/Install or Refresh")]
        public static void Install()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Install;
                return;
            }

            EnsureUISpriteImporter(FIGHT_SPRITE_PATH);
            EnsureUISpriteImporter(SIDESTEP_LEFT_SPRITE_PATH);
            EnsureUISpriteImporter(SIDESTEP_RIGHT_SPRITE_PATH);
            EnsureMobileAnimationImporter(PUNCHES_PATH);
            EnsureMobileAnimationImporter(KICKS_PATH);
            EnsureMobileAnimationImporter(HITS_PATH);
            EnsureMobileAnimationImporter(MOVEMENT_PATH);
            EnsureMobileAudioImporters(SWING_AUDIO_PATHS);
            EnsureMobileAudioImporters(HIT_AUDIO_PATHS);
            EnsureMobileAudioImporters(BLOCK_AUDIO_PATHS);
            EnsureMobileAudioImporters(VOICE_AUDIO_PATHS);
            EnsureMobileAudioImporters(PAIN_AUDIO_PATHS);

            Sprite fightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FIGHT_SPRITE_PATH);
            Sprite sidestepLeftSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                SIDESTEP_LEFT_SPRITE_PATH
            );
            Sprite sidestepRightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                SIDESTEP_RIGHT_SPRITE_PATH
            );
            GameObject strikerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                STRIKER_PREFAB_PATH
            );
            GameCreator.Runtime.Stats.Attribute hp =
                AssetDatabase.LoadAssetAtPath<GameCreator.Runtime.Stats.Attribute>(
                    HP_ATTRIBUTE_PATH
                );

            if (fightSprite == null || sidestepLeftSprite == null ||
                sidestepRightSprite == null || strikerPrefab == null || hp == null)
            {
                Debug.LogError(
                    "Franklin Melee GC2 installation is waiting for its Fight/Sidestep " +
                    "sprites, Striker prefab, or HP asset."
                );
                return;
            }

            AudioClip[] painVoices = new AudioClip[PAIN_AUDIO_PATHS.Length];
            for (int i = 0; i < PAIN_AUDIO_PATHS.Length; ++i)
            {
                painVoices[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    PAIN_AUDIO_PATHS[i]
                );
                if (painVoices[i] != null) continue;

                Debug.LogError($"Missing damage pain voice at '{PAIN_AUDIO_PATHS[i]}'.");
                return;
            }

            Skill[] skills = new Skill[SKILLS.Length];
            for (int i = 0; i < SKILLS.Length; ++i)
            {
                AudioClip voiceAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    VOICE_AUDIO_PATHS[i]
                );
                AudioClip swingAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    SWING_AUDIO_PATHS[i]
                );
                AudioClip hitAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    HIT_AUDIO_PATHS[i]
                );
                AudioClip blockedAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    BLOCK_AUDIO_PATHS[i % BLOCK_AUDIO_PATHS.Length]
                );
                AudioClip parriedAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    BLOCK_AUDIO_PATHS[(i + 1) % BLOCK_AUDIO_PATHS.Length]
                );
                if (voiceAudio == null || swingAudio == null || hitAudio == null ||
                    blockedAudio == null || parriedAudio == null)
                {
                    Debug.LogError(
                        $"Missing melee audio for Skill '{SKILLS[i].AssetName}'."
                    );
                    return;
                }

                skills[i] = CreateOrUpdateSkill(
                    SKILLS[i],
                    fightSprite,
                    hp,
                    voiceAudio,
                    swingAudio,
                    hitAudio,
                    blockedAudio,
                    parriedAudio
                );
                if (skills[i] == null) return;
            }

            MeleeReaction hitReaction = CreateOrUpdateHitReaction(
                out AnimationClip[] reactionAnimations
            );
            if (hitReaction == null) return;

            StateBasicLocomotion locomotionState =
                CreateOrUpdateUnarmedLocomotionState();
            if (locomotionState == null) return;

            AnimationClip sidestepLeft = LoadClip(MOVEMENT_PATH, "KB_Sidestep_L");
            AnimationClip sidestepRight = LoadClip(MOVEMENT_PATH, "KB_Sidestep_R");
            if (sidestepLeft == null || sidestepRight == null)
            {
                Debug.LogError("Missing KB_Sidestep_L/R in the melee movement FBX.");
                return;
            }

            Combos combos = CreateOrUpdateCombos(skills);
            MeleeWeapon weapon = CreateOrUpdateWeapon(
                combos,
                fightSprite,
                hitReaction
            );

            IntegratePlayerPrefab(
                weapon,
                strikerPrefab,
                skills,
                locomotionState,
                sidestepLeft,
                sidestepRight,
                hitReaction,
                reactionAnimations,
                hp,
                painVoices
            );
            IntegrateReactionPrefab(
                NPC_PREFAB_PATH,
                hitReaction,
                reactionAnimations,
                hp,
                painVoices
            );
            IntegrateCanvasPrefab(
                fightSprite,
                sidestepLeftSprite,
                sidestepRightSprite
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Franklin Melee GC2 installed: 8 single-animation skills, directional " +
                "hit reactions, an 8-direction unarmed locomotion State, GC2 selector, " +
                "Player strikers/controller, Fight button, and GC2 left/right sidestep dodge."
            );
        }

        private static void InstallIfNeeded()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += InstallIfNeeded;
                return;
            }

            if (IsInstalled()) return;
            Install();
        }

        private static bool IsInstalled()
        {
            MeleeWeapon weapon = AssetDatabase.LoadAssetAtPath<MeleeWeapon>(WEAPON_PATH);
            MeleeReaction reaction = AssetDatabase.LoadAssetAtPath<MeleeReaction>(
                HIT_REACTION_PATH
            );
            StateBasicLocomotion locomotionState =
                AssetDatabase.LoadAssetAtPath<StateBasicLocomotion>(
                    UNARMED_LOCOMOTION_STATE_PATH
                );
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            GameObject npc = AssetDatabase.LoadAssetAtPath<GameObject>(NPC_PREFAB_PATH);
            GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(CANVAS_PREFAB_PATH);

            if (weapon == null || reaction == null || locomotionState == null ||
                player == null ||
                npc == null || canvas == null)
            {
                return false;
            }

            FieldInfo stateField = FindField(weapon.GetType(), "m_State");
            if (stateField == null) return false;
            StateData weaponState = (StateData)stateField.GetValue(weapon);

            FranklinMeleeController controller =
                player.GetComponent<FranklinMeleeController>();
            MonoBehaviour animationBridge = FindAnimationBridge(player);
            if (controller == null || animationBridge == null) return false;

            SerializedObject serializedController = new SerializedObject(controller);
            SerializedObject serializedBridge = new SerializedObject(animationBridge);
            Transform onFoot = canvas.transform.Find(ON_FOOT_GROUP);
            Transform fight = onFoot != null ? onFoot.Find(FIGHT_BUTTON_NAME) : null;
            Transform sidestepLeft = onFoot != null
                ? onFoot.Find(SIDESTEP_LEFT_BUTTON_NAME)
                : null;
            Transform sidestepRight = onFoot != null
                ? onFoot.Find(SIDESTEP_RIGHT_BUTTON_NAME)
                : null;

            return
                   weaponState.Type == StateData.StateType.State &&
                   weaponState.State == null &&
                   serializedController.FindProperty("m_UnarmedCombatLocomotion")
                       .objectReferenceValue == locomotionState &&
                   serializedController.FindProperty("m_SidestepLeft")
                       .objectReferenceValue != null &&
                   serializedController.FindProperty("m_SidestepRight")
                       .objectReferenceValue != null &&
                   serializedController.FindProperty("m_CombatLocomotionLayer")
                       .intValue == COMBAT_LOCOMOTION_LAYER &&
                   serializedController.FindProperty("m_JogSprintLayer")
                       .intValue == FAST_LOCOMOTION_LAYER &&
                   Mathf.Approximately(
                       serializedController.FindProperty(
                           "m_CombatLocomotionIdleTimeout"
                       ).floatValue,
                       COMBAT_LOCOMOTION_IDLE_TIMEOUT
                   ) &&
                   serializedBridge.FindProperty("m_SprintLayer").intValue ==
                       FAST_LOCOMOTION_LAYER &&
                   player.GetComponent<FranklinMeleeReactionEase>() == null &&
                   npc.GetComponent<FranklinMeleeReactionEase>() != null &&
                   player.GetComponent<FranklinDamagePainAudio>() != null &&
                   npc.GetComponent<FranklinDamagePainAudio>() != null &&
                   player.GetComponent<Character>()?.Animim.Reaction == reaction &&
                   npc.GetComponent<Character>()?.Animim.Reaction == reaction &&
                   FindDescendant(player.transform, LEFT_HAND_STRIKER) != null &&
                   FindDescendant(player.transform, RIGHT_HAND_STRIKER) != null &&
                   FindDescendant(player.transform, LEFT_FOOT_STRIKER) != null &&
                   FindDescendant(player.transform, RIGHT_FOOT_STRIKER) != null &&
                   fight != null &&
                   fight.GetComponent<FranklinFightButton>() != null &&
                   fight.GetComponent<Button>() != null &&
                   sidestepLeft != null &&
                   sidestepLeft.GetComponent<FranklinSidestepButton>() != null &&
                   sidestepLeft.GetComponent<Button>() != null &&
                   sidestepRight != null &&
                   sidestepRight.GetComponent<FranklinSidestepButton>() != null &&
                   sidestepRight.GetComponent<Button>() != null &&
                   onFoot.GetComponent<FranklinSidestepVisibility>() != null;
        }

        private static Skill CreateOrUpdateSkill(
            SkillDefinition definition,
            Sprite icon,
            GameCreator.Runtime.Stats.Attribute hp,
            AudioClip voiceAudio,
            AudioClip swingAudio,
            AudioClip hitAudio,
            AudioClip blockedAudio,
            AudioClip parriedAudio)
        {
            AnimationClip animation = LoadClip(definition.ModelPath, definition.ClipName);
            if (animation == null)
            {
                Debug.LogError(
                    $"Missing single animation '{definition.ClipName}' in {definition.ModelPath}."
                );
                return null;
            }

            string path = $"{ROOT}/Skills/{definition.AssetName}.asset";
            Skill skill = AssetDatabase.LoadAssetAtPath<Skill>(path);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<Skill>();
                AssetDatabase.CreateAsset(skill, path);
            }

            SkillStrike strike = new SkillStrike();
            SetField(strike, "m_Direction", MeleeDirection.Forward);
            SetField(strike, "m_Predictions", 1);
            SetField(strike, "m_UseStrikers", MeleeStrikers.ById);
            SetField(strike, "m_Id", new IdString(definition.StrikerId));

            SetField(skill, "m_Title", new PropertyGetString(definition.Title));
            SetField(skill, "m_Description", new PropertyGetString(definition.Description));
            SetField(skill, "m_Icon", new PropertyGetSprite(icon));
            SetField(skill, "m_Animation", animation);
            SetField(skill, "m_Strike", strike);

            FieldInfo effectsField = FindField(skill.GetType(), "m_Effects");
            SkillEffects effects = effectsField?.GetValue(skill) as SkillEffects ??
                                   new SkillEffects();
            SetField(effects, "m_SoundUse", new PropertyGetAudio(voiceAudio));
            SetField(effects, "m_SoundStrike", new PropertyGetAudio(swingAudio));
            SetField(effects, "m_SoundHit", new PropertyGetAudio(hitAudio));
            SetField(effects, "m_SoundBlocked", new PropertyGetAudio(blockedAudio));
            SetField(effects, "m_SoundParried", new PropertyGetAudio(parriedAudio));
            SetField(skill, "m_Effects", effects);

            SetField(skill, "m_Motion", MeleeMotion.None);
            SetField(skill, "m_Gravity", 1f);
            SetField(skill, "m_TransitionIn", 0.08f);
            SetField(skill, "m_TransitionOut", 0.1f);
            SetField(skill, "m_MeleeSequence", new RunMeleeSequence());
            SetField(skill, "m_SpeedAnticipation", new PropertyGetDecimal(0.6f));
            SetField(skill, "m_SpeedStrike", new PropertyGetDecimal(1.4f / 3f));
            SetField(skill, "m_SpeedRecovery", new PropertyGetDecimal(1f));
            SetField(skill, "m_PoiseArmor", new PropertyGetDecimal(2f));
            SetField(skill, "m_PoiseDamage", new PropertyGetDecimal(definition.PoiseDamage));
            SetField(skill, "m_Power", new PropertyGetDecimal(definition.Power));
            SetField(
                skill,
                "m_OnHit",
                new RunInstructionsList(CreateDamageInstruction(hp, definition.Damage))
            );
            skill.EditorModelPath = definition.ModelPath;

            EditorUtility.SetDirty(skill);
            return skill;
        }

        private static Instruction CreateDamageInstruction(
            GameCreator.Runtime.Stats.Attribute hp,
            float damage)
        {
            GetAttributeInstance getHp = new GetAttributeInstance();
            SetField(getHp, "m_Attribute", hp);

            ChangeDecimal change = new ChangeDecimal(damage);
            FieldInfo operation = FindField(change.GetType(), "m_Operation");
            operation.SetValue(change, Enum.Parse(operation.FieldType, "Subtract"));

            InstructionStatsChangeAttribute instruction =
                new InstructionStatsChangeAttribute();
            SetField(instruction, "m_Target", GetGameObjectTarget.Create());
            SetField(instruction, "m_Attribute", new PropertyGetAttribute(getHp));
            SetField(instruction, "m_Change", change);
            return instruction;
        }

        private static MeleeReaction CreateOrUpdateHitReaction(
            out AnimationClip[] reactionAnimations)
        {
            string[] clipNames =
            {
                "KB_Hit_m_MidFront_Stagger",
                "KB_Hit_m_MidLeft_Stagger",
                "KB_Hit_m_MidRight_Stagger",
                "KB_Hit_m_HighBack_Stagger",
                "KB_Hit_p_MidFront_Weak",
                "KB_Hit_m_MidFront_Weak",
                "KB_Hit_p_MidLeft_Weak",
                "KB_Hit_m_MidLeft_Weak",
                "KB_Hit_p_MidRight_Weak",
                "KB_Hit_m_MidRight_Weak",
                "KB_Hit_m_HighBack_Weak",
                "KB_Hit_p_HighFront_Weak"
            };

            reactionAnimations = new AnimationClip[clipNames.Length];
            for (int i = 0; i < clipNames.Length; ++i)
            {
                reactionAnimations[i] = LoadClip(HITS_PATH, clipNames[i]);
                if (reactionAnimations[i] != null) continue;

                Debug.LogError(
                    $"Missing single reaction animation '{clipNames[i]}' in {HITS_PATH}."
                );
                return null;
            }

            if (!AssetDatabase.IsValidFolder(REACTIONS_FOLDER))
            {
                AssetDatabase.CreateFolder(ROOT, "Reactions");
            }

            MeleeReaction reaction = AssetDatabase.LoadAssetAtPath<MeleeReaction>(
                HIT_REACTION_PATH
            );
            if (reaction == null)
            {
                reaction = ScriptableObject.CreateInstance<MeleeReaction>();
                AssetDatabase.CreateAsset(reaction, HIT_REACTION_PATH);
            }

            ReactionItem[] items =
            {
                CreateReactionItem(
                    ReactionDirection.FromFront,
                    7f,
                    0.48f,
                    reactionAnimations[0]
                ),
                CreateReactionItem(
                    ReactionDirection.FromLeft,
                    7f,
                    0.48f,
                    reactionAnimations[1]
                ),
                CreateReactionItem(
                    ReactionDirection.FromRight,
                    7f,
                    0.48f,
                    reactionAnimations[2]
                ),
                CreateReactionItem(
                    ReactionDirection.FromBack,
                    7f,
                    0.48f,
                    reactionAnimations[3]
                ),
                CreateReactionItem(
                    ReactionDirection.FromFront,
                    null,
                    0.32f,
                    reactionAnimations[4],
                    reactionAnimations[5]
                ),
                CreateReactionItem(
                    ReactionDirection.FromLeft,
                    null,
                    0.32f,
                    reactionAnimations[6],
                    reactionAnimations[7]
                ),
                CreateReactionItem(
                    ReactionDirection.FromRight,
                    null,
                    0.32f,
                    reactionAnimations[8],
                    reactionAnimations[9]
                ),
                CreateReactionItem(
                    ReactionDirection.FromBack,
                    null,
                    0.32f,
                    reactionAnimations[10]
                ),
                CreateReactionItem(
                    ReactionDirection.FromAny,
                    null,
                    0.32f,
                    reactionAnimations[11],
                    reactionAnimations[5]
                )
            };

            SetField(reaction, "m_TransitionIn", 0.08f);
            SetField(reaction, "m_TransitionOut", 0.12f);
            SetField(reaction, "m_UseRootMotion", false);
            SetField(reaction, "m_Speed", new PropertyGetDecimal(1f));
            SetField(reaction, "m_ReactionList", new ReactionList(items));
            EditorUtility.SetDirty(reaction);
            return reaction;
        }

        private static ReactionItem CreateReactionItem(
            ReactionDirection direction,
            float? minimumPower,
            float cancelTime,
            params AnimationClip[] animations)
        {
            ReactionAnimations reactionAnimations = new ReactionAnimations();
            SetField(reactionAnimations, "m_Animations", animations);

            ReactionItem item = new ReactionItem();
            SetField(
                item,
                "m_MinPower",
                new EnablerFloat(minimumPower.HasValue, minimumPower ?? 0f)
            );
            SetField(item, "m_Direction", direction);
            SetField(item, "m_CancelTime", new EnablerFloat(true, cancelTime));
            SetField(item, "m_Rotation", ReactionRotation.None);
            SetField(item, "m_Gravity", 1f);
            SetField(item, "m_Animations", reactionAnimations);
            return item;
        }

        private static Combos CreateOrUpdateCombos(Skill[] skills)
        {
            Combos combos = AssetDatabase.LoadAssetAtPath<Combos>(COMBOS_PATH);
            if (combos == null)
            {
                combos = ScriptableObject.CreateInstance<Combos>();
                AssetDatabase.CreateAsset(combos, COMBOS_PATH);
            }

            ComboTree tree = new ComboTree();
            for (int i = 0; i < skills.Length; ++i)
            {
                ComboItem item = new ComboItem();
                SetField(item, "m_Key", SKILLS[i].Key);
                SetField(item, "m_Mode", MeleeMode.Tap);
                SetField(item, "m_When", MeleeExecute.AnyTime);
                SetField(item, "m_Skill", skills[i]);
                tree.AddToRoot(item);
            }

            SetField(combos, "m_Combos", tree);
            EditorUtility.SetDirty(combos);
            return combos;
        }

        private static StateBasicLocomotion CreateOrUpdateUnarmedLocomotionState()
        {
            AnimationClip idle = LoadClip(MOVEMENT_PATH, "KB_Idle_1");
            AnimationClip forward = LoadClip(MOVEMENT_PATH, "KB_WalkFwd1");
            AnimationClip backward = LoadClip(MOVEMENT_PATH, "KB_WalkBwd");
            AnimationClip right = LoadClip(MOVEMENT_PATH, "KB_Sidestep_R");
            AnimationClip left = LoadClip(MOVEMENT_PATH, "KB_Sidestep_L");
            AnimationClip forwardRight = LoadClip(MOVEMENT_PATH, "KB_WalkRight45");
            AnimationClip forwardLeft = LoadClip(MOVEMENT_PATH, "KB_WalkLeft45");
            AnimationClip backwardRight = LoadClip(MOVEMENT_PATH, "KB_WalkRight135");
            AnimationClip backwardLeft = LoadClip(MOVEMENT_PATH, "KB_WalkLeft135");

            if (idle == null || forward == null || backward == null ||
                right == null || left == null || forwardRight == null ||
                forwardLeft == null || backwardRight == null || backwardLeft == null)
            {
                Debug.LogError(
                    "Franklin Unarmed Combat Locomotion is missing one or more " +
                    "KB_Movement clips from Fighting Animset Pro."
                );
                return null;
            }

            if (!AssetDatabase.IsValidFolder(STATES_FOLDER))
            {
                AssetDatabase.CreateFolder(ROOT, "States");
            }

            StateBasicLocomotion state =
                AssetDatabase.LoadAssetAtPath<StateBasicLocomotion>(
                    UNARMED_LOCOMOTION_STATE_PATH
                );
            if (state == null)
            {
                state = ScriptableObject.CreateInstance<StateBasicLocomotion>();
                state.name = "Franklin Unarmed Combat Locomotion";
                AssetDatabase.CreateAsset(state, UNARMED_LOCOMOTION_STATE_PATH);
            }

            RuntimeAnimatorController baseController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    BASIC_LOCOMOTION_CONTROLLER_PATH
                );
            if (baseController == null)
            {
                Debug.LogError(
                    $"Missing GC2 Basic Locomotion controller at " +
                    $"'{BASIC_LOCOMOTION_CONTROLLER_PATH}'."
                );
                return null;
            }

            AnimatorOverrideController overrideController = null;
            foreach (UnityEngine.Object asset in
                     AssetDatabase.LoadAllAssetsAtPath(UNARMED_LOCOMOTION_STATE_PATH))
            {
                if (asset is not AnimatorOverrideController candidate) continue;
                overrideController = candidate;
                break;
            }

            if (overrideController == null)
            {
                overrideController = new AnimatorOverrideController(baseController)
                {
                    name = "Franklin Unarmed Combat Locomotion Controller",
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(overrideController, state);
            }
            else
            {
                overrideController.runtimeAnimatorController = baseController;
            }

            Stand8Points stand = new Stand8Points
            {
                m_Idle = idle,
                m_Forward = forward,
                m_Backward = backward,
                m_Right = right,
                m_Left = left,
                m_ForwardRight = forwardRight,
                m_ForwardLeft = forwardLeft,
                m_BackwardRight = backwardRight,
                m_BackwardLeft = backwardLeft
            };

            SetField(state, "m_Stand8Points", stand);
            SetField(state, "m_Controller", overrideController);

            overrideController["Human@Stand_Idle"] = idle;
            overrideController["Human@Stand_Fast_F"] = forward;
            overrideController["Human@Stand_Fast_B"] = backward;
            overrideController["Human@Stand_Fast_R"] = right;
            overrideController["Human@Stand_Fast_L"] = left;
            overrideController["Human@Stand_Fast_FR"] = forwardRight;
            overrideController["Human@Stand_Fast_FL"] = forwardLeft;
            overrideController["Human@Stand_Fast_BR"] = backwardRight;
            overrideController["Human@Stand_Fast_BL"] = backwardLeft;

            EditorUtility.SetDirty(overrideController);
            EditorUtility.SetDirty(state);
            return state;
        }

        private static MeleeWeapon CreateOrUpdateWeapon(
            Combos combos,
            Sprite icon,
            MeleeReaction hitReaction)
        {
            MeleeWeapon weapon = AssetDatabase.LoadAssetAtPath<MeleeWeapon>(WEAPON_PATH);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<MeleeWeapon>();
                AssetDatabase.CreateAsset(weapon, WEAPON_PATH);
            }

            ComboSelector selector = new ComboSelector();
            SetField(selector, "m_CombosAsset", combos);

            SetField(weapon, "m_Id", new UniqueID("franklin-unarmed"));
            SetField(weapon, "m_Title", new PropertyGetString("Franklin Unarmed"));
            SetField(
                weapon,
                "m_Description",
                new PropertyGetString(
                    "Eight single-clip attacks selected through the GC2 Melee combo tree."
                )
            );
            SetField(weapon, "m_Icon", new PropertyGetSprite(icon));
            SetField(weapon, "m_Combos", selector);
            SetField(weapon, "m_HitReaction", hitReaction);
            // Combat locomotion is driven exclusively by FranklinMeleeController.Fight().
            // Keeping the weapon State empty prevents equip-on-start from activating it.
            SetField(weapon, "m_State", new StateData(StateData.StateType.State));
            SetField(weapon, "m_Layer", new PropertyGetInteger(COMBAT_LOCOMOTION_LAYER));

            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static void IntegratePlayerPrefab(
            MeleeWeapon weapon,
            GameObject strikerPrefab,
            Skill[] skills,
            StateBasicLocomotion locomotionState,
            AnimationClip sidestepLeft,
            AnimationClip sidestepRight,
            MeleeReaction hitReaction,
            AnimationClip[] reactionAnimations,
            GameCreator.Runtime.Stats.Attribute healthAttribute,
            AudioClip[] painVoices)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);
            try
            {
                Character character = root.GetComponent<Character>();
                if (character == null)
                {
                    Debug.LogError($"{PLAYER_PREFAB_PATH} has no GC2 Character component.");
                    return;
                }

                Traits traits = root.GetComponent<Traits>();
                if (traits == null)
                {
                    Debug.LogError($"{PLAYER_PREFAB_PATH} has no GC2 Traits component.");
                    return;
                }

                FranklinMeleeController controller =
                    root.GetComponent<FranklinMeleeController>();
                bool isNewController = controller == null;
                if (isNewController)
                {
                    controller = root.AddComponent<FranklinMeleeController>();
                }

                FranklinMeleeReactionEase reactionEase =
                    root.GetComponent<FranklinMeleeReactionEase>();
                if (reactionEase != null)
                {
                    UnityEngine.Object.DestroyImmediate(reactionEase, true);
                }

                character.Animim.Reaction = hitReaction;

                SerializedObject serializedController = new SerializedObject(controller);
                serializedController.FindProperty("m_Character").objectReferenceValue = character;
                serializedController.FindProperty("m_UnarmedWeapon").objectReferenceValue = weapon;
                serializedController.FindProperty("m_UnarmedCombatLocomotion")
                    .objectReferenceValue = locomotionState;
                serializedController.FindProperty("m_SidestepLeft")
                    .objectReferenceValue = sidestepLeft;
                serializedController.FindProperty("m_SidestepRight")
                    .objectReferenceValue = sidestepRight;
                serializedController.FindProperty("m_CombatLocomotionLayer").intValue =
                    COMBAT_LOCOMOTION_LAYER;
                serializedController.FindProperty("m_JogSprintLayer").intValue =
                    FAST_LOCOMOTION_LAYER;
                serializedController.FindProperty("m_CombatLocomotionIdleTimeout").floatValue =
                    COMBAT_LOCOMOTION_IDLE_TIMEOUT;
                SerializedProperty attackSkills =
                    serializedController.FindProperty("m_AttackSkills");
                attackSkills.arraySize = skills.Length;
                for (int i = 0; i < skills.Length; ++i)
                {
                    attackSkills.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
                }
                SerializedProperty globalSkillSpeed =
                    serializedController.FindProperty("m_GlobalSkillSpeed");
                if (globalSkillSpeed.floatValue <= 0f)
                {
                    globalSkillSpeed.floatValue = 1f;
                }
                if (isNewController)
                {
                    serializedController.FindProperty("m_UseGlobalSkillTrail").boolValue = true;
                    serializedController.FindProperty("m_AttackMovementSpeed").floatValue = 0.5f;
                    serializedController.FindProperty("m_InputBuffer").floatValue = 1.65f;
                    serializedController.FindProperty("m_AttackStateTimeout").floatValue = 3.3f;
                    serializedController.FindProperty("m_EquipOnStart").boolValue = true;
                    serializedController.FindProperty("m_UseAnimationEase").boolValue = true;
                    serializedController.FindProperty("m_AnimationEase").enumValueIndex =
                        (int)Easing.Type.QuadInOut;
                    serializedController.FindProperty("m_EaseInDuration").floatValue = 0.3f;
                    serializedController.FindProperty("m_EaseOutDuration").floatValue = 0.42f;
                    serializedController.FindProperty("m_EaseEdgeSpeedMultiplier").floatValue =
                        0.6f;
                }
                serializedController.FindProperty("m_HitReaction").objectReferenceValue =
                    hitReaction;
                SerializedProperty controllerReactionAnimations =
                    serializedController.FindProperty("m_ReactionAnimations");
                controllerReactionAnimations.arraySize = reactionAnimations.Length;
                for (int i = 0; i < reactionAnimations.Length; ++i)
                {
                    controllerReactionAnimations.GetArrayElementAtIndex(i)
                        .objectReferenceValue = reactionAnimations[i];
                }
                if (isNewController)
                {
                    serializedController.FindProperty("m_UseReactionEase").boolValue = true;
                    serializedController.FindProperty("m_ReactionEase").enumValueIndex =
                        (int)Easing.Type.QuadInOut;
                    serializedController.FindProperty("m_ReactionEaseInDuration").floatValue =
                        0.08f;
                    serializedController.FindProperty("m_ReactionEaseOutDuration").floatValue =
                        0.12f;
                    serializedController.FindProperty("m_ReactionEdgeSpeedMultiplier").floatValue =
                        0.65f;
                    serializedController.FindProperty("m_EaseUpdatesPerSecond").intValue = 30;
                }
                SerializedProperty globalReactionSpeed =
                    serializedController.FindProperty("m_GlobalReactionSpeed");
                if (globalReactionSpeed.floatValue <= 0f)
                {
                    globalReactionSpeed.floatValue = 1f;
                }
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(character);

                MonoBehaviour animationBridge = FindAnimationBridge(root);
                if (animationBridge == null)
                {
                    Debug.LogError(
                        $"{PLAYER_PREFAB_PATH} has no {ANIMATION_BRIDGE_TYPE} component."
                    );
                    return;
                }

                SerializedObject serializedBridge = new SerializedObject(animationBridge);
                SerializedProperty sprintLayer =
                    serializedBridge.FindProperty("m_SprintLayer");
                if (sprintLayer == null)
                {
                    Debug.LogError(
                        $"{ANIMATION_BRIDGE_TYPE} has no serialized m_SprintLayer field."
                    );
                    return;
                }
                sprintLayer.intValue = FAST_LOCOMOTION_LAYER;
                serializedBridge.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(animationBridge);

                FranklinDamagePainAudio painAudio =
                    root.GetComponent<FranklinDamagePainAudio>();
                bool isNewPainAudio = painAudio == null;
                if (isNewPainAudio)
                {
                    painAudio = root.AddComponent<FranklinDamagePainAudio>();
                }
                ConfigureDamagePainAudio(
                    painAudio,
                    traits,
                    healthAttribute,
                    painVoices,
                    isNewPainAudio
                );
                EditorUtility.SetDirty(painAudio);

                SpawnStrikerPrefab(
                    root.transform,
                    strikerPrefab,
                    "hand.l",
                    LEFT_HAND_STRIKER,
                    LEFT_HAND_ID,
                    0.22f
                );
                SpawnStrikerPrefab(
                    root.transform,
                    strikerPrefab,
                    "hand.r",
                    RIGHT_HAND_STRIKER,
                    RIGHT_HAND_ID,
                    0.22f
                );
                SpawnStrikerPrefab(
                    root.transform,
                    strikerPrefab,
                    "foot.l",
                    LEFT_FOOT_STRIKER,
                    LEFT_FOOT_ID,
                    0.27f
                );
                SpawnStrikerPrefab(
                    root.transform,
                    strikerPrefab,
                    "foot.r",
                    RIGHT_FOOT_STRIKER,
                    RIGHT_FOOT_ID,
                    0.27f
                );

                PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void IntegrateReactionPrefab(
            string prefabPath,
            MeleeReaction hitReaction,
            AnimationClip[] reactionAnimations,
            GameCreator.Runtime.Stats.Attribute healthAttribute,
            AudioClip[] painVoices)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Character character = root.GetComponent<Character>();
                if (character == null)
                {
                    Debug.LogError($"{prefabPath} has no GC2 Character component.");
                    return;
                }

                Traits traits = root.GetComponent<Traits>();
                if (traits == null)
                {
                    Debug.LogError($"{prefabPath} has no GC2 Traits component.");
                    return;
                }

                character.Animim.Reaction = hitReaction;
                FranklinMeleeReactionEase reactionEase =
                    root.GetComponent<FranklinMeleeReactionEase>() ??
                    root.AddComponent<FranklinMeleeReactionEase>();
                reactionEase.hideFlags = HideFlags.HideInInspector;
                ConfigureReactionEaseDriver(
                    reactionEase,
                    character,
                    reactionAnimations
                );

                FranklinDamagePainAudio painAudio =
                    root.GetComponent<FranklinDamagePainAudio>();
                bool isNewPainAudio = painAudio == null;
                if (isNewPainAudio)
                {
                    painAudio = root.AddComponent<FranklinDamagePainAudio>();
                }
                ConfigureDamagePainAudio(
                    painAudio,
                    traits,
                    healthAttribute,
                    painVoices,
                    isNewPainAudio
                );

                EditorUtility.SetDirty(character);
                EditorUtility.SetDirty(reactionEase);
                EditorUtility.SetDirty(painAudio);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureReactionEaseDriver(
            FranklinMeleeReactionEase reactionEase,
            Character character,
            AnimationClip[] reactionAnimations)
        {
            SerializedObject serialized = new SerializedObject(reactionEase);
            serialized.FindProperty("m_Character").objectReferenceValue = character;

            SerializedProperty animations = serialized.FindProperty("m_ReactionAnimations");
            animations.arraySize = reactionAnimations.Length;
            for (int i = 0; i < reactionAnimations.Length; ++i)
            {
                animations.GetArrayElementAtIndex(i).objectReferenceValue =
                    reactionAnimations[i];
            }

            serialized.FindProperty("m_UseEase").boolValue = true;
            serialized.FindProperty("m_Ease").enumValueIndex = (int)Easing.Type.QuadInOut;
            serialized.FindProperty("m_EaseInDuration").floatValue = 0.08f;
            serialized.FindProperty("m_EaseOutDuration").floatValue = 0.12f;
            serialized.FindProperty("m_EdgeSpeedMultiplier").floatValue = 0.65f;
            serialized.FindProperty("m_GlobalReactionSpeed").floatValue = 1f;
            serialized.FindProperty("m_UpdatesPerSecond").intValue = 30;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDamagePainAudio(
            FranklinDamagePainAudio painAudio,
            Traits traits,
            GameCreator.Runtime.Stats.Attribute healthAttribute,
            AudioClip[] painVoices,
            bool useDefaults)
        {
            SerializedObject serialized = new SerializedObject(painAudio);
            serialized.FindProperty("m_Traits").objectReferenceValue = traits;
            serialized.FindProperty("m_HealthAttribute").objectReferenceValue =
                healthAttribute;

            SerializedProperty voices = serialized.FindProperty("m_PainVoices");
            voices.arraySize = painVoices.Length;
            for (int i = 0; i < painVoices.Length; ++i)
            {
                voices.GetArrayElementAtIndex(i).objectReferenceValue = painVoices[i];
            }

            if (useDefaults)
            {
                serialized.FindProperty("m_Volume").floatValue = 0.55f;
                serialized.FindProperty("m_PitchRange").vector2Value =
                    new Vector2(0.96f, 1.04f);
                serialized.FindProperty("m_MinInterval").floatValue = 0.22f;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SpawnStrikerPrefab(
            Transform root,
            GameObject strikerPrefab,
            string boneName,
            string instanceName,
            string strikerId,
            float radius)
        {
            Transform bone = FindDescendant(root, boneName);
            if (bone == null)
            {
                Debug.LogError($"Could not find Player bone '{boneName}' for melee striker.");
                return;
            }

            Striker legacyStriker = bone.GetComponent<Striker>();
            if (legacyStriker != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyStriker, true);
            }

            Transform existing = bone.Find(instanceName);
            GameObject strikerObject = existing != null ? existing.gameObject : null;
            if (strikerObject == null)
            {
                strikerObject = PrefabUtility.InstantiatePrefab(strikerPrefab, bone) as GameObject;
            }

            if (strikerObject == null)
            {
                Debug.LogError(
                    $"Could not spawn {STRIKER_PREFAB_PATH} under Player bone '{boneName}'."
                );
                return;
            }

            strikerObject.name = instanceName;
            strikerObject.transform.SetParent(bone, false);
            strikerObject.transform.localPosition = Vector3.zero;
            strikerObject.transform.localRotation = Quaternion.identity;
            strikerObject.transform.localScale = Vector3.one;

            Striker striker = strikerObject.GetComponent<Striker>();
            if (striker == null)
            {
                Debug.LogError($"{STRIKER_PREFAB_PATH} has no GC2 Striker component.");
                return;
            }

            StrikerSphere sphere = new StrikerSphere();
            SetField(sphere, "m_Radius", radius);

            SetField(striker, "m_ID", new UniqueID(strikerId));
            SetField(striker, "m_LayerMask", (LayerMask)MELEE_TARGET_LAYERS);
            SetField(
                striker,
                "m_Section",
                GetGameObjectInstance.Create(strikerObject)
            );
            SetField(striker, "m_Shape", sphere);
            EditorUtility.SetDirty(striker);
        }

        private static void IntegrateCanvasPrefab(
            Sprite fightSprite,
            Sprite sidestepLeftSprite,
            Sprite sidestepRightSprite)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CANVAS_PREFAB_PATH);
            try
            {
                Transform onFoot = root.transform.Find(ON_FOOT_GROUP);
                if (onFoot == null)
                {
                    Debug.LogError($"{CANVAS_PREFAB_PATH} has no '{ON_FOOT_GROUP}' group.");
                    return;
                }

                GameObject fight = GetOrCreateMobileButton(
                    onFoot,
                    FIGHT_BUTTON_NAME,
                    fightSprite,
                    new Vector2(-310f, 175f),
                    new Vector2(185f, 185f),
                    out _
                );
                FranklinFightButton fightButton =
                    fight.GetComponent<FranklinFightButton>() ??
                    fight.AddComponent<FranklinFightButton>();
                Button fightUIButton = fight.GetComponent<Button>();
                RemovePersistentPressListener(fightUIButton, fightButton);
                UnityEventTools.AddPersistentListener(
                    fightUIButton.onClick,
                    fightButton.Press
                );
                fight.SetActive(true);

                RectTransform fightRect = fight.GetComponent<RectTransform>();
                Vector2 sidestepSize = new Vector2(140f, 140f);
                Vector2 leftPosition = fightRect.anchoredPosition + new Vector2(-190f, 0f);
                Vector2 rightPosition = fightRect.anchoredPosition + new Vector2(190f, 0f);

                GameObject sidestepLeft = ConfigureSidestepButton(
                    onFoot,
                    SIDESTEP_LEFT_BUTTON_NAME,
                    sidestepLeftSprite,
                    leftPosition,
                    sidestepSize,
                    FranklinSidestepButton.Direction.Left
                );
                GameObject sidestepRight = ConfigureSidestepButton(
                    onFoot,
                    SIDESTEP_RIGHT_BUTTON_NAME,
                    sidestepRightSprite,
                    rightPosition,
                    sidestepSize,
                    FranklinSidestepButton.Direction.Right
                );

                FranklinSidestepVisibility visibility =
                    onFoot.GetComponent<FranklinSidestepVisibility>() ??
                    onFoot.gameObject.AddComponent<FranklinSidestepVisibility>();
                SerializedObject serializedVisibility = new SerializedObject(visibility);
                SerializedProperty sidestepButtons =
                    serializedVisibility.FindProperty("m_SidestepButtons");
                sidestepButtons.arraySize = 2;
                sidestepButtons.GetArrayElementAtIndex(0).objectReferenceValue =
                    sidestepLeft;
                sidestepButtons.GetArrayElementAtIndex(1).objectReferenceValue =
                    sidestepRight;
                serializedVisibility.ApplyModifiedPropertiesWithoutUndo();

                // Object Direction starts disabled, so sidestep controls must not flash
                // on screen before the runtime facing state is resolved.
                sidestepLeft.SetActive(false);
                sidestepRight.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, CANVAS_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject GetOrCreateMobileButton(
            Transform parent,
            string objectName,
            Sprite sprite,
            Vector2 defaultPosition,
            Vector2 defaultSize,
            out bool isNew)
        {
            Transform existing = parent.Find(objectName);
            isNew = existing == null;
            GameObject gameObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button)
                );
            gameObject.layer = 5;

            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (isNew)
            {
                rect.anchoredPosition = defaultPosition;
                rect.sizeDelta = defaultSize;
                rect.localScale = Vector3.one;
            }

            Image image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;

            Button button = gameObject.GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.92f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.65f, 0.9f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            return gameObject;
        }

        private static GameObject ConfigureSidestepButton(
            Transform parent,
            string objectName,
            Sprite sprite,
            Vector2 defaultPosition,
            Vector2 defaultSize,
            FranklinSidestepButton.Direction direction)
        {
            GameObject gameObject = GetOrCreateMobileButton(
                parent,
                objectName,
                sprite,
                defaultPosition,
                defaultSize,
                out _
            );
            FranklinSidestepButton sidestepButton =
                gameObject.GetComponent<FranklinSidestepButton>() ??
                gameObject.AddComponent<FranklinSidestepButton>();

            SerializedObject serializedButton = new SerializedObject(sidestepButton);
            serializedButton.FindProperty("m_Direction").enumValueIndex = (int)direction;
            serializedButton.ApplyModifiedPropertiesWithoutUndo();

            Button button = gameObject.GetComponent<Button>();
            RemovePersistentPressListener(button, sidestepButton);
            UnityEventTools.AddPersistentListener(button.onClick, sidestepButton.Press);
            return gameObject;
        }

        private static void RemovePersistentPressListener(
            Button button,
            MonoBehaviour target)
        {
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; --i)
            {
                if (button.onClick.GetPersistentTarget(i) == target &&
                    button.onClick.GetPersistentMethodName(i) == "Press")
                {
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                }
            }
        }

        private static void EnsureUISpriteImporter(string spritePath)
        {
            AssetDatabase.ImportAsset(
                spritePath,
                ImportAssetOptions.ForceSynchronousImport
            );
            if (AssetImporter.GetAtPath(spritePath) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Compressed;
            ConfigureMobileTexture(importer, "Android");
            ConfigureMobileTexture(importer, "iPhone");
            importer.SaveAndReimport();
        }

        private static void ConfigureMobileTexture(
            TextureImporter importer,
            string platform)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = 256;
            settings.format = TextureImporterFormat.ASTC_6x6;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.compressionQuality = 50;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }

        private static void EnsureMobileAnimationImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) return;

            bool changed = false;
            changed |= SetImporterValue(
                importer.materialImportMode,
                ModelImporterMaterialImportMode.None,
                value => importer.materialImportMode = value
            );
            changed |= SetImporterValue(importer.importCameras, false, value =>
                importer.importCameras = value
            );
            changed |= SetImporterValue(importer.importLights, false, value =>
                importer.importLights = value
            );
            changed |= SetImporterValue(importer.importBlendShapes, false, value =>
                importer.importBlendShapes = value
            );
            changed |= SetImporterValue(importer.isReadable, false, value =>
                importer.isReadable = value
            );
            changed |= SetImporterValue(importer.optimizeGameObjects, true, value =>
                importer.optimizeGameObjects = value
            );
            changed |= SetImporterValue(
                importer.animationCompression,
                ModelImporterAnimationCompression.Optimal,
                value => importer.animationCompression = value
            );

            if (changed) importer.SaveAndReimport();
        }

        private static void EnsureMobileAudioImporters(string[] paths)
        {
            foreach (string path in paths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;

                bool changed = false;
                changed |= SetImporterValue(importer.forceToMono, true, value =>
                    importer.forceToMono = value
                );
                changed |= SetImporterValue(importer.loadInBackground, false, value =>
                    importer.loadInBackground = value
                );
                changed |= SetImporterValue(importer.ambisonic, false, value =>
                    importer.ambisonic = value
                );

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    changed = true;
                }
                if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
                {
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    changed = true;
                }
                if (!Mathf.Approximately(settings.quality, 0.45f))
                {
                    settings.quality = 0.45f;
                    changed = true;
                }
                if (settings.sampleRateSetting != AudioSampleRateSetting.OptimizeSampleRate)
                {
                    settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                    changed = true;
                }
                if (!settings.preloadAudioData)
                {
                    settings.preloadAudioData = true;
                    changed = true;
                }

                SerializedObject serializedImporter = new SerializedObject(importer);
                SerializedProperty normalize = serializedImporter.FindProperty("m_Normalize");
                if (normalize != null && normalize.boolValue)
                {
                    normalize.boolValue = false;
                    serializedImporter.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (!changed) continue;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static bool SetImporterValue<T>(
            T current,
            T expected,
            Action<T> setter)
        {
            if (Equals(current, expected)) return false;
            setter(expected);
            return true;
        }

        private static AnimationClip LoadClip(string modelPath, string clipName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip && clip.name == clipName) return clip;
            }

            return null;
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == childName) return candidate;
            }

            return null;
        }

        private static MonoBehaviour FindAnimationBridge(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().FullName == ANIMATION_BRIDGE_TYPE) return behaviour;
            }

            return null;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                if (field != null) return field;
                type = type.BaseType;
            }

            return null;
        }
    }
}
