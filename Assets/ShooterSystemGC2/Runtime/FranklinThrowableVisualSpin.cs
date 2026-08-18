using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>Cheap visual-only tumble; Bullet remains the sole owner of projectile physics.</summary>
    [DisallowMultipleComponent]
    public sealed class FranklinThrowableVisualSpin : MonoBehaviour
    {
        [SerializeField] private Vector3 m_DegreesPerSecond = new(410f, 255f, 175f);

        private void Update()
        {
            this.transform.Rotate(
                this.m_DegreesPerSecond * Time.deltaTime,
                Space.Self
            );
        }
    }
}
