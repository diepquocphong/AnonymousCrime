using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.Shooter
{
    /// <summary>Opaque annular sector used to highlight one weapon-wheel slice.</summary>
    [DisallowMultipleComponent]
    public sealed class FranklinWeaponSectorHighlight : MaskableGraphic
    {
        private const int ARC_SEGMENTS = 18;
        private const float INNER_RADIUS_RATIO = 0.39f;
        private const float OUTER_RADIUS_RATIO = 0.965f;
        private const float HALF_ANGLE = 20.5f;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = this.rectTransform.rect;
            float halfSize = Mathf.Min(rect.width, rect.height) * 0.5f;
            float innerRadius = halfSize * INNER_RADIUS_RATIO;
            float outerRadius = halfSize * OUTER_RADIUS_RATIO;
            Color32 vertexColor = this.color;

            for (int i = 0; i <= ARC_SEGMENTS; ++i)
            {
                float t = i / (float) ARC_SEGMENTS;
                float angle = Mathf.Lerp(90f - HALF_ANGLE, 90f + HALF_ANGLE, t) *
                              Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));

                vertexHelper.AddVert(direction * innerRadius, vertexColor, Vector2.zero);
                vertexHelper.AddVert(direction * outerRadius, vertexColor, Vector2.zero);
            }

            for (int i = 0; i < ARC_SEGMENTS; ++i)
            {
                int innerStart = i * 2;
                int outerStart = innerStart + 1;
                int innerEnd = innerStart + 2;
                int outerEnd = innerStart + 3;

                vertexHelper.AddTriangle(innerStart, outerStart, outerEnd);
                vertexHelper.AddTriangle(innerStart, outerEnd, innerEnd);
            }
        }
    }
}
