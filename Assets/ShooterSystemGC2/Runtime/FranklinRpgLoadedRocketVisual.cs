using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Shooter;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Mirrors the finite RPG reserve onto the separate loaded-rocket mesh. The component
    /// caches the munition and renderers once; LateUpdate is allocation-free and guarantees
    /// the loaded rocket disappears in the same rendered frame that GC2 consumes the shot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinRpgLoadedRocketVisual : MonoBehaviour
    {
        private ShooterMunition m_Munition;
        private Renderer[] m_Renderers = System.Array.Empty<Renderer>();
        private int m_LastTotal = int.MinValue;

        public void Initialize(
            Character character,
            ShooterWeapon weapon,
            FranklinWeaponModelPose modelPose)
        {
            this.m_Munition = character != null && weapon != null
                ? character.Combat.RequestMunition(weapon) as ShooterMunition
                : null;

            Transform model = modelPose != null ? modelPose.ModelTransform : null;
            Transform loadedRocket = model != null ? model.Find("Rocket") : null;
            this.m_Renderers = loadedRocket != null
                ? loadedRocket.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            this.m_LastTotal = int.MinValue;
            this.RefreshVisual();
        }

        private void LateUpdate()
        {
            this.RefreshVisual();
        }

        private void RefreshVisual()
        {
            int total = this.m_Munition?.Total ?? 0;
            if (total == this.m_LastTotal) return;

            this.m_LastTotal = total;
            bool visible = total > 0;
            for (int i = 0; i < this.m_Renderers.Length; ++i)
            {
                Renderer item = this.m_Renderers[i];
                if (item != null && item.enabled != visible) item.enabled = visible;
            }
        }
    }
}
