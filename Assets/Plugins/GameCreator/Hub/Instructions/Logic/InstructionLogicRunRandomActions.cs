using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using Random = System.Random;


[Version(1, 0, 0)]

[Title("Run Random Actions")]
[Description("Executes an random Actions in a group of actions.")]

[Category("Logic/Run Random Actions")]

[Keywords("Execute", "Call", "Instruction", "Action", "Random")]
[Image(typeof(IconInstructions), ColorTheme.Type.Purple)]

[Parameter(
    "Actions Group",
    "The Actions Group that is executed"
)]

[Parameter(
    "Wait Until Complete",
    "If true this instruction waits until the Actions object finishes running"
)]

[Serializable]
public class InstructionLogicRunRandomActions : Instruction
{
    
    // MEMBERS: -------------------------------------------------------------------------------

    [SerializeField] private PropertyGetGameObject m_ActionsGroup = GetGameObjectInstance.Create();
    [SerializeField] private bool m_WaitToFinish = true;


    // PROPERTIES: ----------------------------------------------------------------------------
    public override string Title => string.Format(
        "Run {0}'s random children actions {1}", 
        this.m_ActionsGroup,
        this.m_WaitToFinish ? "and wait" : string.Empty
    );
    
    
    // RUN METHOD: ----------------------------------------------------------------------------

    protected override async Task Run(Args args)
    {
        var go = this.m_ActionsGroup.Get(args);
        var actions = go.GetComponentsInChildren<Actions>();
        if(actions.Length == 0) return;

        var i = UnityEngine.Random.Range(0, actions.Length);

        if (this.m_WaitToFinish) await actions[i].Run(args);
        else _ = actions[i].Run(args);
    }
}
