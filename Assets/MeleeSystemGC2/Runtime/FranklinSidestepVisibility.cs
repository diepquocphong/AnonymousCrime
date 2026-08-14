using System;
using System.Collections;
using GameCreator.Runtime.Characters;
using UnityEngine;

namespace FranklinGame.Melee
{
    /// <summary>
    /// Shows the sidestep controls only while GC2 is using Object Direction facing.
    /// Visibility updates from CharacterKernel.EventChangeFacing, with no frame polling
    /// after the Player has been resolved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinSidestepVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject[] m_SidestepButtons =
            Array.Empty<GameObject>();

        private Character m_Character;
        private Coroutine m_BindRoutine;
        private bool m_IsVisible;

        private void OnEnable()
        {
            this.SetVisibility(false, true);
            if (this.TryBindCharacter()) return;

            this.m_BindRoutine = this.StartCoroutine(this.BindWhenAvailable());
        }

        private IEnumerator BindWhenAvailable()
        {
            while (this.isActiveAndEnabled && !this.TryBindCharacter())
            {
                yield return null;
            }

            this.m_BindRoutine = null;
        }

        private bool TryBindCharacter()
        {
            FranklinMeleeController controller =
                FindFirstObjectByType<FranklinMeleeController>();
            Character character = controller != null
                ? controller.GetComponent<Character>()
                : null;
            if (character?.Kernel == null) return false;

            if (this.m_Character != null)
            {
                this.m_Character.Kernel.EventChangeFacing -= this.RefreshVisibility;
            }

            this.m_Character = character;
            this.m_Character.Kernel.EventChangeFacing += this.RefreshVisibility;
            this.RefreshVisibility();
            return true;
        }

        private void RefreshVisibility()
        {
            this.SetVisibility(
                this.m_Character?.Facing is UnitFacingObjectDirection,
                false
            );
        }

        private void SetVisibility(bool visible, bool force)
        {
            if (!force && this.m_IsVisible == visible) return;

            this.m_IsVisible = visible;
            foreach (GameObject button in this.m_SidestepButtons)
            {
                if (button != null && button.activeSelf != visible)
                {
                    button.SetActive(visible);
                }
            }
        }

        private void OnDisable()
        {
            if (this.m_BindRoutine != null)
            {
                this.StopCoroutine(this.m_BindRoutine);
                this.m_BindRoutine = null;
            }

            if (this.m_Character != null && this.m_Character.Kernel != null)
            {
                this.m_Character.Kernel.EventChangeFacing -= this.RefreshVisibility;
            }

            this.m_Character = null;
            this.SetVisibility(false, true);
        }
    }
}
