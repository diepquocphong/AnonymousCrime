using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using Event = GameCreator.Runtime.VisualScripting.Event;

namespace Niam.Runtime.Tactile
{
    [Parameter("Tactile Control", "The game object with Tactile Control component attached")]

    [Serializable]
    public abstract class TTactileEvent : Event
    {
        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] 
        protected PropertyGetGameObject m_TactileControl = GetGameObjectInstance.Create();

        [NonSerialized]
        protected TactileControl m_Control;

        // METHODS: -------------------------------------------------------------------------------

        protected override void OnEnable(Trigger trigger)
        {
            base.OnEnable(trigger);
            this.m_Trigger = trigger;

            this.m_Control = this.m_TactileControl.Get<TactileControl>(trigger);
            if (this.m_Control == null)
            {
                TactileControl.EventInstantiateAny -= this.WaitForControlToInstantiate;
                TactileControl.EventInstantiateAny += this.WaitForControlToInstantiate;
                return;
            }

            this.WhenEnabled(trigger);
        }

        protected override void OnDisable(Trigger trigger)
        {
            base.OnDisable(trigger);

            if (this.m_Control == null)
            {
                this.m_Control = this.m_TactileControl.Get<TactileControl>(trigger);
            }

            if (this.m_Control == null)
            {
                return;
            }

            this.WhenDisabled(trigger);
        }

        private void WaitForControlToInstantiate(IdString controlId)
        {
            this.m_Control = this.m_TactileControl.Get<TactileControl>(this.m_Trigger);
            if (this.m_Control == null) return;

            this.WhenEnabled(this.m_Trigger);
            TactileControl.EventInstantiateAny -= this.WaitForControlToInstantiate;
        }

        // ABSTRACT METHODS: ----------------------------------------------------------------------

        protected abstract void WhenEnabled(Trigger trigger);
        protected abstract void WhenDisabled(Trigger trigger);
    }
}