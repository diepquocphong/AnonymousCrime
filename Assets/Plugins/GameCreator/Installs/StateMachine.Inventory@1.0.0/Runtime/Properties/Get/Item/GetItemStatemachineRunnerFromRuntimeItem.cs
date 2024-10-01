using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Inventory;
using GameCreator.Runtime.Variables;
using NinjutsuGames.StateMachine.Runtime.Common;
using NinjutsuGames.StateMachine.Runtime.Variables;

namespace NinjutsuGames.StateMachine.Runtime.Inventory
{
    [Title("State Machine Runner Variable from Runtime Item")]
    [Category("Variables/State Machine Runner Variable from Runtime Item")]
    
    [Image(typeof(IconStateMachine), ColorTheme.Type.Yellow, typeof(OverlayBolt))]
    [Description("Returns the Item value of a State Machine Runner Variable with a Runtime Item")]

    [Serializable] [HideLabelsInEditor]
    public class GetItemStateMachineRunnerFromRuntimeItem : PropertyTypeGetItem
    {
        [SerializeField]
        protected FieldGetStateMachineRunner m_Variable = new(ValueItem.TYPE_ID);

        public override Item Get(Args args) => this.m_Variable.Get<RuntimeItem>(args)?.Item;

        public override string String => this.m_Variable.ToString();
    }
}