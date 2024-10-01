using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Inventory;
using NinjutsuGames.StateMachine.Runtime.Common;
using NinjutsuGames.StateMachine.Runtime.Variables;

namespace NinjutsuGames.StateMachine.Runtime.Inventory
{
    [Title("State Machine Runner Variable")]
    [Category("State Machine Runner Variable")]
    
    [Description("Sets the Item value on a State Machine Runner Variable")]
    [Image(typeof(IconStateMachine), ColorTheme.Type.Yellow, typeof(OverlayBolt))]

    [Serializable] [HideLabelsInEditor]
    public class SetRuntimeItemStateMachineRunner : PropertyTypeSetRuntimeItem
    {
        [SerializeField]
        protected FieldSetStateMachineRunner m_Variable = new(ValueItem.TYPE_ID);

        public override void Set(RuntimeItem value, Args args) => this.m_Variable.Set(value, args);
        public override RuntimeItem Get(Args args) => this.m_Variable.Get(args) as RuntimeItem;

        public static PropertySetRuntimeItem Create => new PropertySetRuntimeItem(
            new SetRuntimeItemStateMachineRunner()
        );
        
        public override string String => this.m_Variable.ToString();
    }
}