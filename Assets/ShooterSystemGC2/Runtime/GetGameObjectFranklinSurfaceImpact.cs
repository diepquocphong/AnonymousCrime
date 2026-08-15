using System;
using FranklinGame.Vehicles;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Shooter
{
    [Title("Franklin Surface Impact")]
    [Category("Shooter/Franklin Surface Impact")]
    [Image(typeof(IconBullsEye), ColorTheme.Type.Yellow)]
    [Description("Returns ground/wall impact VFX only when target is not a Character, Car or Bike")]
    [Serializable]
    public sealed class GetGameObjectFranklinSurfaceImpact : PropertyTypeGetGameObject
    {
        [SerializeField] private GameObject m_SurfaceImpact;

        public GameObject SurfaceImpact => this.m_SurfaceImpact;

        public GetGameObjectFranklinSurfaceImpact()
        { }

        public GetGameObjectFranklinSurfaceImpact(GameObject surfaceImpact)
        {
            this.m_SurfaceImpact = surfaceImpact;
        }

        public override GameObject Get(Args args)
        {
            return IsSpecialHitTarget(args.Target) ? null : this.m_SurfaceImpact;
        }

        public override GameObject Get(GameObject gameObject)
        {
            return IsSpecialHitTarget(gameObject) ? null : this.m_SurfaceImpact;
        }

        public override string String => this.m_SurfaceImpact != null
            ? $"Surface: {this.m_SurfaceImpact.name}"
            : "Surface: (none)";

        public override GameObject EditorValue => this.m_SurfaceImpact;

        private static bool IsSpecialHitTarget(GameObject target)
        {
            return target != null &&
                   (target.GetComponentInParent<Character>() != null ||
                    target.GetComponentInParent<FranklinBikeHealth>() != null ||
                    target.GetComponentInParent<SimcadeCarHealth>() != null);
        }
    }
}
