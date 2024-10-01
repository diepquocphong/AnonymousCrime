using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Inventory;
using NinjutsuGames.StateMachine.Runtime.Common;
using NinjutsuGames.StateMachine.Runtime.Variables;

namespace NinjutsuGames.StateMachine.Runtime.Inventory
{
    [Title("State Machine Variable")]
    [Category("State Machine Variable")]
    
    [Description("Sets the Item value on a State Machine Variable")]
    [Image(typeof(IconStateMachine), ColorTheme.Type.Blue)]

    [Serializable] [HideLabelsInEditor]
    public class SetItemStateMachine : PropertyTypeSetItem
    {
        [SerializeField]
        protected FieldSetStateMachine m_Variable = new FieldSetStateMachine(ValueItem.TYPE_ID);

        public override void Set(Item value, Args args) => this.m_Variable.Set(value, args);
        public override Item Get(Args args) => this.m_Variable.Get(args) as Item;

        public static PropertySetItem Create => new PropertySetItem(
            new SetItemStateMachine()
        );
        
        public override string String => this.m_Variable.ToString();
    }
}