using UnityEngine;
using NinjutsuGames.StateMachine.Runtime;
using UnityEngine.UIElements;

namespace NinjutsuGames.StateMachine.Editor
{
    [NodeCustomEditor(typeof(BranchNode))]
    public class BranchNodeView : BaseGameCreatorNodeView
    {
        private Image _conditionIcon;
        private Image _actionIcon;
        public override Texture2D DefaultIcon => ICON_BRANCH.Texture;
        public override string DefaultIconName => ((BranchNode)nodeTarget).branch.GetConditionsList().Length > 0 ? ((BranchNode)nodeTarget).branch.GetConditionsList().Get(0).GetType().Name : null;
        
        private Texture GetConditionIcon()
        {
            var node = (BranchNode) nodeTarget;
            var conditions = node.branch.GetConditionsList();
            return conditions.Length > 0 ? GetIcon(conditions.Get(0).GetType().Name) : ICON_CONDITION.Texture;
        }
        
        private Texture GetInstructionIcon()
        {
            var node = (BranchNode) nodeTarget;
            var instructions = node.branch.GetInstructionsList();
            return instructions.Length > 0 ? GetIcon(instructions.Get(0).GetType().Name) : ICON_INSTRUCTION.Texture;
        }

        public override void Enable()
        {
            base.Enable();
            
            var container = new VisualElement
            {
                name = "BranchContent"
            };

            _conditionIcon = new Image
            {
                image = GetConditionIcon(),
                name = "ConditionIcon"
            };
            container.Add(_conditionIcon);
            
            var arrowIcon = new Image
            {
                image = ICON_ARROW.Texture,
                name = "ArrowIcon"
            };
            container.Add(arrowIcon);
            
            _actionIcon = new Image
            {
                image = GetInstructionIcon(),
                name = "ActionIcon"
            };
            container.Add(_actionIcon);
            
            titleContainer.parent.Insert(1, container);
        }
        
        public override void Update()
        {
            _conditionIcon.image = GetConditionIcon();
            _actionIcon.image = GetInstructionIcon();
        }
    }
}