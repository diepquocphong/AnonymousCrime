using UnityEngine;

namespace FranklinGame.Melee
{
    /// <summary>
    /// Resolves the active melee controller and forwards one mobile sidestep input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinSidestepButton : MonoBehaviour
    {
        public enum Direction
        {
            Left,
            Right
        }

        [SerializeField] private Direction m_Direction;

        private FranklinMeleeController m_Controller;

        public void Press()
        {
            if (this.m_Controller == null)
            {
                this.m_Controller = FindFirstObjectByType<FranklinMeleeController>();
            }

            if (this.m_Controller == null) return;

            switch (this.m_Direction)
            {
                case Direction.Left:
                    this.m_Controller.SidestepLeft();
                    break;

                case Direction.Right:
                    this.m_Controller.SidestepRight();
                    break;
            }
        }
    }
}
