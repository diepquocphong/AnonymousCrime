using GameCreator.Runtime.Characters;
using UnityEngine;

namespace FranklinGame.Animations
{
    /// <summary>
    /// Temporarily swaps GC2's facing unit to Object Direction and restores the exact
    /// previous unit afterwards. There is no per-frame work in this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FranklinAnimationBridge))]
    public sealed class FranklinObjectDirectionToggle : MonoBehaviour
    {
        private Character m_Character;
        private TUnitFacing m_PreviousFacing;
        private UnitFacingObjectDirection m_ObjectDirectionFacing;
        private bool m_IsObjectDirectionEnabled;

        public bool IsObjectDirectionEnabled => this.m_IsObjectDirectionEnabled;

        private void Awake()
        {
            this.ResolveCharacter();
        }

        private void OnDisable()
        {
            this.SetObjectDirectionEnabled(false);
        }

        public bool SetObjectDirectionEnabled(bool active)
        {
            if (this.m_IsObjectDirectionEnabled == active) return true;
            if (!this.ResolveCharacter()) return false;

            if (active)
            {
                this.m_PreviousFacing = this.m_Character.Facing as TUnitFacing;
                this.m_ObjectDirectionFacing ??= new UnitFacingObjectDirection();
                this.m_Character.Kernel.ChangeFacing(
                    this.m_Character,
                    this.m_ObjectDirectionFacing
                );
                this.m_IsObjectDirectionEnabled = true;
                return true;
            }

            // Do not overwrite a facing mode installed by another system while the
            // toggle was active (vehicle, cutscene, combat, etc.).
            if (ReferenceEquals(
                    this.m_Character.Facing,
                    this.m_ObjectDirectionFacing
                ))
            {
                this.m_Character.Kernel.ChangeFacing(
                    this.m_Character,
                    this.m_PreviousFacing ?? new UnitFacingPivot()
                );
            }

            this.m_PreviousFacing = null;
            this.m_IsObjectDirectionEnabled = false;
            return true;
        }

        private bool ResolveCharacter()
        {
            if (this.m_Character != null && this.m_Character.Kernel != null)
                return true;

            this.m_Character = this.GetComponentInParent<Character>();
            return this.m_Character != null && this.m_Character.Kernel != null;
        }
    }
}
