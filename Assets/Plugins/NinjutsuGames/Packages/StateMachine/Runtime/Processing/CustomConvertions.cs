using System;
using System.Collections.Generic;
using GraphProcessor;

namespace NinjutsuGames.StateMachine.Runtime
{
    public class CustomConvertions : ITypeAdapter
    {
        public static EntryPort EntryFromAction(ActionPortIn from) => new();
        public static EntryPort EntryFromCondition(ConditionsPort from) => new();
        public static EntryPort EntryFromSm(StateMachinePortIn from) => new();
        public static EntryPort EntryFromSm(BranchPortIn from) => new();
        public static EntryPort EntryFromExit(ExitPort from) => new();
        // public static EntryPort EntryFromRelay(RelayNode.PackedRelayData from) => new();

        public static ActionPortIn ActionFromEntry(EntryPort from) => new();
        public static ActionPortIn ActionFromConditions(ConditionsPortOutFail from) => new();
        public static ActionPortIn ActionFromConditions(ConditionsPortOutSuccess from) => new();
        public static ActionPortIn ActionFromConditions(BranchPortOut from) => new();
        public static ActionPortIn ActionFromConditions(StateMachinePortOut from) => new();
        
        public static ActionPortIn ActionFromOut(ActionPortOut from) => new();
        // public static ActionPortIn ActionFromOut(RelayNode.PackedRelayData from) => new();
        public static ActionPortOut ActionFromIn(ActionPortIn from) => new();
        public static ActionPortOut ActionOutFromConditions(ConditionsPort from) => new();
        public static ActionPortOut ActionOutFromSm(StateMachinePortIn from) => new();
        public static ActionPortOut ActionOutFromSm(BranchPortIn from) => new();
        public static ActionPortOut ActionOutFromExit(ExitPort from) => new();
        // public static ActionPortOut ActionOutFromExit(RelayNode.PackedRelayData from) => new();
        
        public static ConditionsPort ConditionFromEntry(EntryPort from) => new();
        public static ConditionsPort ConditionFromActionOut(ActionPortOut from) => new();
        public static ConditionsPort ConditionsOutFromSm(StateMachinePortIn from) => new();
        public static ConditionsPort ConditionsOutFromSm(ConditionsPortOutFail from) => new();
        public static ConditionsPort ConditionsOutFromSm(ConditionsPortOutSuccess from) => new();
        public static ConditionsPort ConditionsOutFromSm(BranchPortIn from) => new();
        public static ConditionsPort ConditionsOutFromSm(BranchPortOut from) => new();
        public static ConditionsPort ConditionsOutFromSm(StateMachinePortOut from) => new();
        // public static ConditionsPort ConditionsOutFromSm(RelayNode.PackedRelayData from) => new();
        
        public static ConditionsPortOutFail ConditionFromFail(ActionPortIn from) => new();
        public static ConditionsPortOutFail ConditionFromFail(StateMachinePortIn from) => new();
        public static ConditionsPortOutFail ConditionFromFail(ConditionsPort from) => new();
        public static ConditionsPortOutFail ConditionFromFail(BranchPortIn from) => new();
        public static ConditionsPortOutFail ConditionFromFail(ExitPort from) => new();
        // public static ConditionsPortOutFail ConditionFromFail(RelayNode.PackedRelayData from) => new();
        
        public static ConditionsPortOutSuccess ConditionFromSuccess(ActionPortIn from) => new();
        public static ConditionsPortOutSuccess ConditionFromSuccess(StateMachinePortIn from) => new();
        public static ConditionsPortOutSuccess ConditionFromSuccess(ConditionsPort from) => new();
        public static ConditionsPortOutSuccess ConditionFromSuccess(BranchPortIn from) => new();
        public static ConditionsPortOutSuccess ConditionFromSuccess(ExitPort from) => new();
        // public static ConditionsPortOutSuccess ConditionFromSuccess(RelayNode.PackedRelayData from) => new();

        
        public static TriggerPortIn TriggerFromOut(TriggerPortOut from) => new();
        public static TriggerPortIn TriggerFromOut(StateMachinePortIn from) => new();
        // public static TriggerPortIn TriggerFromOut(RelayNode.PackedRelayData from) => new();
        
        public static TriggerPortOut TriggerFromIn(TriggerPortIn from) => new();
        public static TriggerPortOut TriggerFromSm(StateMachinePortIn from) => new();
        public static TriggerPortOut TriggerFromSm(BranchPortIn from) => new();
        public static TriggerPortOut TriggerFromSm(ExitPort from) => new();
        // public static TriggerPortOut TriggerFromSm(RelayNode.PackedRelayData from) => new();
        
        public static StateMachinePortIn SmFromTrigger(TriggerPortIn from) => new();
        public static StateMachinePortIn SmFromTrigger(TriggerPortOut from) => new();
        public static StateMachinePortIn SmFromActions(ActionPortOut from) => new();
        public static StateMachinePortIn SmFromConditions(ConditionsPort from) => new();
        public static StateMachinePortIn SmFromConditions(ConditionsPortOutFail from) => new();
        public static StateMachinePortIn SmFromConditions(ConditionsPortOutSuccess from) => new();
        public static StateMachinePortIn SmFromEntry(EntryPort from) => new();
        public static StateMachinePortIn SmFromEntry(BranchPortOut from) => new();
        public static StateMachinePortIn SmFromEntry(StateMachinePortOut from) => new();
        public static StateMachinePortIn SmFromEntry(BranchPortIn from) => new();
        // public static StateMachinePortIn SmFromEntry(RelayNode.PackedRelayData from) => new();
        
        public static StateMachinePortOut SmTo(ActionPortIn from) => new();
        public static StateMachinePortOut SmTo(StateMachinePortIn from) => new();
        public static StateMachinePortOut SmTo(ConditionsPort from) => new();
        public static StateMachinePortOut SmTo(BranchPortIn from) => new();
        public static StateMachinePortOut SmTo(ExitPort from) => new();
        // public static StateMachinePortOut SmTo(RelayNode.PackedRelayData from) => new();
        
        public static BranchPortIn BranchFrom(TriggerPortOut from) => new();
        public static BranchPortIn BranchFrom(ActionPortOut from) => new();
        public static BranchPortIn BranchFrom(ConditionsPort from) => new();
        public static BranchPortIn BranchFrom(ConditionsPortOutFail from) => new();
        public static BranchPortIn BranchFrom(ConditionsPortOutSuccess from) => new();
        public static BranchPortIn BranchFrom(EntryPort from) => new();
        public static BranchPortIn BranchFrom(BranchPortOut from) => new();
        public static BranchPortIn BranchFrom(StateMachinePortIn from) => new();
        public static BranchPortIn BranchFrom(StateMachinePortOut from) => new();
        // public static BranchPortIn BranchFrom(RelayNode.PackedRelayData from) => new();
        
        public static BranchPortOut BranchTo(BranchPortIn from) => new();
        public static BranchPortOut BranchTo(ActionPortIn from) => new();
        public static BranchPortOut BranchTo(ConditionsPort from) => new();
        public static BranchPortOut BranchTo(StateMachinePortIn from) => new();
        public static BranchPortOut BranchTo(ExitPort from) => new();
        // public static BranchPortOut BranchTo(RelayNode.PackedRelayData from) => new();
        
        public static ExitPort ExitFromEntry(EntryPort from) => new(); 
        public static ExitPort ExitFromActionIn(ActionPortOut from) => new();
        public static ExitPort Exit(BranchPortOut from) => new();
        public static ExitPort Exit(TriggerPortOut from) => new();
        public static ExitPort Exit(ConditionsPortOutFail from) => new();
        public static ExitPort Exit(ConditionsPortOutSuccess from) => new();
        public static ExitPort Exit(StateMachinePortOut from) => new();
        // public static ExitPort Exit(RelayNode.PackedRelayData from) => new();
        
        /*public static RelayNode.PackedRelayData RelayFromRelayData(RelayNode.PackedRelayData from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(EntryPort from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(ActionPortOut from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(ActionPortIn from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(StateMachinePortIn from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(StateMachinePortOut from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(TriggerPortOut from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(TriggerPortIn from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(BranchPortIn from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(BranchPortOut from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(ConditionsPort from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(ConditionsPortOutFail from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(ConditionsPortOutSuccess from) => new();
        public static RelayNode.PackedRelayData RelayFromRelayData(ExitPort from) => new();*/

        public override IEnumerable<(Type, Type)> GetIncompatibleTypes()
        {
            yield return (typeof(RelayNode.PackedRelayData), typeof(object));
        }
    }
}