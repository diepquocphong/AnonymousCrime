using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Inventory;
using GameCreator.Runtime.Variables;
using NinjutsuGames.StateMachine.Runtime.Common;
using NinjutsuGames.StateMachine.Runtime.Variables;

namespace NinjutsuGames.StateMachine.Runtime.Inventory
{
    [Title("State Machine Variable")]
    [Category("Variables/State Machine Variable")]
    
    [Image(typeof(IconStateMachine), ColorTheme.Type.Blue)]
    [Description("Returns the Item value of a State Machine Variable")]

    [Serializable] [HideLabelsInEditor]
    public class GetItemStateMachine : PropertyTypeGetItem
    {
        [SerializeField]
        protected FieldGetStateMachine m_Variable = new FieldGetStateMachine(ValueItem.TYPE_ID);

        public override Item Get(Args args) => this.m_Variable.Get<Item>(args);

        public override string String => this.m_Variable.ToString();
    }
}