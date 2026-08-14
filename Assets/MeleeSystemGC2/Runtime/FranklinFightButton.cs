using UnityEngine;

namespace FranklinGame.Melee
{
    /// <summary>
    /// Keeps the Canvas prefab independent from the Player prefab by resolving the active
    /// Franklin melee controller when the local UI Button invokes Press.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinFightButton : MonoBehaviour
    {
        private FranklinMeleeController m_Controller;

        public void Press()
        {
            if (this.m_Controller == null)
            {
                this.m_Controller = FindFirstObjectByType<FranklinMeleeController>();
            }

            this.m_Controller?.Fight();
        }
    }
}
