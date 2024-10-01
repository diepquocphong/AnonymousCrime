using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Inventory;
using GameCreator.Runtime.Variables;
using NinjutsuGames.StateMachine.Runtime.Common;
using NinjutsuGames.StateMachine.Runtime.Variables;

namespace NinjutsuGames.StateMachine.Runtime.Inventory
{
    [Title("State Machine Runner Variable")]
    [Category("Variables/State Machine Runner Variable")]
    
    [Image(typeof(IconStateMachine), ColorTheme.Type.Yellow, typeof(OverlayBolt))]
    [Description("Returns the Item value of a State Machine Runner Variable")]

    [Serializable] [HideLabelsInEditor]
    public class GetItemStateMachineRunner : PropertyTypeGetItem
    {
        [SerializeField]
        protected FieldGetStateMachineRunner m_Variable = new FieldGetStateMachineRunner(ValueItem.TYPE_ID);

        public override Item Get(Args args) => this.m_Variable.Get<Item>(args);

        public override string String => this.m_Variable.ToString();
    }
}