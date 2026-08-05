using System;
using UnityEngine;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile 
{
    [Title("Skill Button")]
    [Category("Skill Button")]

    [Description(
        "A Skill and Button control type used for skill without directional aiming. " +
        "Supports skill activation, cooldowns, and cancellation."
    )]

    [Parameter(
        "Input Simulate",
        "The button control path of the input control to be simulate"
    )]
    [Parameter(
        "Input Execution", 
        "Defines how the value transitions or updates during button press and release"
    )]
    [Parameter(
        "↳ Is Usable",
        "<indent=1.2em>Determines whether the Skill Button can be used"
    )]

    [Parameter(
        "Cooldown", 
        "Section for configuring the cooldown"
    )]
    [Parameter(
        "↳ Fill",
        "<indent=1.2em>A reference to a image component with image type set to filled"
    )]
    [Parameter(
        "↳ Text", 
        "<indent=1.2em>A reference to a text or TMP component to display the remaining cooldown"
    )]
    [Parameter(
        "↳ Format",
        "<indent=1.2em>The number format used when displaying the cooldown as text." +
        "\ne.g." +
        "\n    {0} → 12" +
        "\n    {0.0} or {1} → 12.3" +
        "\n    {0.00} or {2} → 12.34"
    )]
    [Parameter(
        "↳ Duration", 
        "<indent=1.2em>The total time duration to finish the cooldown"
    )]
    [Parameter(
        "↳ Time Mode", 
        "<indent=1.2em>The time scale that affects the duration of the cooldown"
    )]
    [Parameter(
        "↳ Is Manual",
        "<indent=1.2em>Determines whether the cooldown is managed manually (via Instruction " +
        "or scripting). If set to false, the cooldown starts automatically after casting."
    )]

    [Parameter(
        "Cancellation", 
        "Section for configuring the skill cancellation"
    )]
    [Parameter(
        "↳ Cancel Area", 
        "<indent=1.2em>A reference to a Tactile Control component. Interactions such as press, " +
        "release, etc. of a Cancel Area will also trigger regardless if its interactable or not " +
        "when using the Skill Button"
    )]
    [Parameter(
        "↳ Is Inverted",
        "<indent=1.2em>If true, cancellation is triggered outside the Cancel Area. " +
        "If false, it is triggered inside the area."
    )]

    [Image(typeof(IconPushButton), typeof(OverlayBolt))]

    [Serializable]
    public class ControlTypeSkillButton : TButtonType, ISkillType
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private bool              m_IsUsable = true;
        [SerializeField] private SkillCooldown     m_Cooldown = new SkillCooldown();
        [SerializeField] private SkillCancellation m_Cancellation = new SkillCancellation(true);

        [NonSerialized] private bool m_PressedOnCooldown;

        // PROPERTIES: ----------------------------------------------------------------------------

        public bool IsUsable
        {
            get => this.m_IsUsable;
            set
            {
                if (this.m_IsUsable && !value)
                {
                    this.Value = 0;
                    this.m_Control.ForceEndInteractAll();
                }

                this.m_IsUsable = value;
            }
        }

        public bool IsCooldown => this.m_Cooldown.IsCooldown;
        public float CooldownRatio => this.m_Cooldown.Ratio;
        public float CooldownRemaining => this.m_Cooldown.Remaining;

        // EVENTS: -------------------------------------------------------------------------------

        public event Action EventActivateSkill;

        public event Action EventStartCooldown
        {
            add => this.m_Cooldown.EventStart += value;
            remove => this.m_Cooldown.EventStart -= value;
        }
        
        public event Action EventResetCooldown
        {
            add => this.m_Cooldown.EventReset += value;
            remove => this.m_Cooldown.EventReset -= value;
        }

        // BEHAVIORS: -----------------------------------------------------------------------------

        protected internal override void Start()
        {
            this.m_Cooldown.Setup(this.Args);
            this.m_Cancellation.Setup(this.m_Control);
        }

        protected internal override void Update()
        {
            this.m_Cooldown.Update();
        }

        // INTERACTION: ---------------------------------------------------------------------------

        protected internal override void InteractBeforeBegin(Touch touch)
        {
            if (!this.IsUsable) return;

            this.m_PressedOnCooldown = this.IsCooldown;
            if (this.m_PressedOnCooldown) return;
            
            if (this.FingerCount > 1) return;
            if (!this.HasPressInArea) return;

            if (this.m_InputExecution != InputExecution.ReleasePulse)
            {
                this.Value = 1;
            }
        }

        protected internal override void InteractAfterBegin(Touch touch)
        {
            if (!this.IsUsable || this.FingerCount > 1) return;
            if (this.IsCooldown || !this.HasPressInArea) return;
            if (this.m_InputExecution == InputExecution.PressPulse)
            {
                this.EventActivateSkill?.Invoke();
                this.Value = 0;

                this.m_Cooldown.Start();
            }
        }

        protected internal override void InteractBeforeEnd(Touch touch)
        {
            if (!this.IsUsable) return;

            if (this.IsCooldown)
            {
                this.Value = 0;
                return;
            }

            if (this.FingerCount > 1) return;
            if (this.m_InputExecution == InputExecution.PressPulse) return;

            if (!this.m_Cancellation.IsWithinArea(touch.screenPosition) &&
                !this.m_PressedOnCooldown)
            {
                if (this.m_InputExecution == InputExecution.ReleasePulse)
                {
                    this.Value = 1;
                }

                this.EventActivateSkill?.Invoke();
                this.Value = 0;

                this.m_Cooldown.Start();
            }
            else
            {
                this.Value = 0;
            }
        }

        protected internal override void InteractAfterEnd(Touch touch)
        { }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void StartCooldown(bool force = false)
        {
            this.m_Cooldown.Start(force);

            this.Value = 0;
            this.m_Control.ForceEndInteractAll();
        }

        public void ResetCooldown() => this.m_Cooldown.Reset();

        // GIZMOS: --------------------------------------------------------------------------------
        
        #if UNITY_EDITOR

        protected internal override void DrawGizmos(Transform transform)
        {
            this.m_Cancellation.DrawGizmos(transform);
        }

        #endif

    }
}