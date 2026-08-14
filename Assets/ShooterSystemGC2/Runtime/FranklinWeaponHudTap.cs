using UnityEngine;
using UnityEngine.EventSystems;

namespace FranklinGame.Shooter
{
    /// <summary>Turns the existing Franklin weapon HUD card into the weapon-menu opener.</summary>
    [DisallowMultipleComponent]
    public sealed class FranklinWeaponHudTap : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.dragging) return;
            FranklinShooterSystem.Instance?.OpenWeaponMenu();
        }
    }
}
