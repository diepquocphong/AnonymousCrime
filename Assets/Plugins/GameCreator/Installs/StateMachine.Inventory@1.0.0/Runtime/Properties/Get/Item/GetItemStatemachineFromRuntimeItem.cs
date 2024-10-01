using System;
using UnityEngine;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Inventory;
using GameCreator.Runtime.Variables;
using NinjutsuGames.StateMachine.Runtime.Common;
using NinjutsuGames.StateMachine.Runtime.Variables;

namespace NinjutsuGames.StateMachine.Runtime.Inventory
{
    [Title("State Machine Variable from Runtime Item")]
    [Category("Variables/State Machine Variable from Runtime Item")]
    
    [Image(typeof(IconStateMachine), ColorTheme.Type.Blue)]
    [Description("Returns the Item value of a State Machine Variable with a Runtime Item")]

    [Serializable] [HideLabelsInEditor]
    public class GetItemStateMachineFromRuntimeItem : PropertyTypeGetItem
    {
        [SerializeField]
        protected FieldGetStateMachine m_Variable = new(ValueItem.TYPE_ID);

        public override Item Get(Args args) => this.m_Variable.Get<RuntimeItem>(args)?.Item;


        public override string String => this.m_Variable.ToString();
    }
}