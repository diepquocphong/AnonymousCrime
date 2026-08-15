using System;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Render-mesh-only Bike deformation using the same Edy 5.5.3 algorithm as
    /// the Car. Wheel, tire, glass and physical MeshCollider meshes are untouched.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FranklinBikeImpactAudio))]
    public sealed class FranklinBikeDeformation : MonoBehaviour
    {
        [SerializeField] private FranklinBikeImpactAudio m_ImpactAudio;
        [SerializeField] private Transform m_RenderedBody;
        [SerializeField] private MeshFilter[] m_DeformablePanels =
            Array.Empty<MeshFilter>();

        [Header("Bike Mesh Deformation")]
        [SerializeField, Min(0.1f)] private float m_MinimumImpactVelocity = 2.5f;
        [SerializeField, Min(0.01f)] private float m_ImpactMultiplier = 0.8f;
        [SerializeField, Min(0.05f)] private float m_DamageRadius = 0.38f;
        [SerializeField, Min(0.01f)] private float m_MaximumVertexDisplacement = 0.12f;
        [SerializeField, Min(0f)] private float m_MaximumVertexFracture = 0.018f;

        [Header("Mobile Event Budget")]
        [SerializeField, Range(1, 20)] private int m_MaximumDentCount = 10;
        [SerializeField, Range(1000, 40000)] private int m_MaximumVerticesPerPanel = 32000;

        private sealed class PanelState
        {
            public MeshFilter Filter;
            public Renderer Renderer;
            public Mesh SourceMesh;
            public Mesh RuntimeMesh;
            public Vector3[] OriginalVertices;
            public Vector3[] CurrentVertices;
            public Vector3[] OriginalNormals;
        }

        private PanelState[] m_Panels = Array.Empty<PanelState>();
        private Bounds m_LocalBodyBounds;
        private int m_DentCount;

        public bool IsConfigured => m_ImpactAudio != null && m_RenderedBody != null &&
            m_DeformablePanels != null && m_DeformablePanels.Length > 0;
        public int DeformablePanelCount => m_DeformablePanels?.Length ?? 0;
        public int DentCount => m_DentCount;
        public int MaximumVerticesPerPanel => m_MaximumVerticesPerPanel;
        public bool HasCurrentConfiguration => m_MaximumVerticesPerPanel >= 32000;

        public void Configure(
            FranklinBikeImpactAudio impactAudio,
            Transform renderedBody,
            MeshFilter[] deformablePanels)
        {
            m_ImpactAudio = impactAudio;
            m_RenderedBody = renderedBody;
            m_DeformablePanels = deformablePanels ?? Array.Empty<MeshFilter>();
            m_MinimumImpactVelocity = 2.5f;
            m_ImpactMultiplier = 0.8f;
            m_DamageRadius = 0.38f;
            m_MaximumVertexDisplacement = 0.12f;
            m_MaximumVertexFracture = 0.018f;
            m_MaximumDentCount = 10;
            // Generated DQP body panels peak at 30,320 vertices. Keep a small
            // headroom so the visible main fairing never gets silently skipped.
            m_MaximumVerticesPerPanel = 32000;
        }

        private void Awake()
        {
            if (m_ImpactAudio == null)
                m_ImpactAudio = GetComponent<FranklinBikeImpactAudio>();
            BuildPanelStates();
        }

        private void OnEnable()
        {
            if (m_ImpactAudio == null)
                m_ImpactAudio = GetComponent<FranklinBikeImpactAudio>();
            if (m_ImpactAudio != null)
                m_ImpactAudio.EventImpactContactAccepted += OnImpactAccepted;
        }

        private void OnDisable()
        {
            if (m_ImpactAudio != null)
                m_ImpactAudio.EventImpactContactAccepted -= OnImpactAccepted;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < m_Panels.Length; ++i)
            {
                PanelState panel = m_Panels[i];
                if (panel == null || panel.RuntimeMesh == null) continue;
                if (panel.Filter != null && panel.Filter.sharedMesh == panel.RuntimeMesh)
                    panel.Filter.sharedMesh = panel.SourceMesh;
                Destroy(panel.RuntimeMesh);
            }
        }

        public void ResetDeformation()
        {
            for (int i = 0; i < m_Panels.Length; ++i)
            {
                PanelState panel = m_Panels[i];
                if (panel?.RuntimeMesh == null || panel.OriginalVertices == null) continue;
                Array.Copy(
                    panel.OriginalVertices,
                    panel.CurrentVertices,
                    panel.OriginalVertices.Length
                );
                panel.RuntimeMesh.vertices = panel.CurrentVertices;
                if (panel.OriginalNormals != null &&
                    panel.OriginalNormals.Length == panel.OriginalVertices.Length)
                {
                    panel.RuntimeMesh.normals = panel.OriginalNormals;
                }
                else panel.RuntimeMesh.RecalculateNormals();
                panel.RuntimeMesh.RecalculateBounds();
                panel.RuntimeMesh.UploadMeshData(false);
            }
            m_DentCount = 0;
        }

        private void BuildPanelStates()
        {
            int count = m_DeformablePanels?.Length ?? 0;
            m_Panels = new PanelState[count];
            bool hasBounds = false;
            for (int i = 0; i < count; ++i)
            {
                MeshFilter filter = m_DeformablePanels[i];
                Renderer renderer = filter != null ? filter.GetComponent<Renderer>() : null;
                m_Panels[i] = new PanelState
                {
                    Filter = filter,
                    Renderer = renderer,
                    SourceMesh = filter != null ? filter.sharedMesh : null
                };
                if (renderer == null || m_RenderedBody == null) continue;
                Bounds worldBounds = renderer.bounds;
                Vector3 localCenter = m_RenderedBody.InverseTransformPoint(worldBounds.center);
                Bounds local = new Bounds(localCenter, worldBounds.size);
                if (!hasBounds)
                {
                    m_LocalBodyBounds = local;
                    hasBounds = true;
                }
                else m_LocalBodyBounds.Encapsulate(local);
            }
        }

        private void OnImpactAccepted(Collision collision, bool _, float __)
        {
            int dentBudget = Application.isMobilePlatform
                ? Mathf.Min(5, m_MaximumDentCount)
                : m_MaximumDentCount;
            if (m_DentCount >= dentBudget || collision == null ||
                collision.contactCount <= 0 ||
                collision.relativeVelocity.sqrMagnitude <
                    m_MinimumImpactVelocity * m_MinimumImpactVelocity)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            if (IsUndersideGroundContact(contact)) return;

            Vector3 worldImpactVelocity = collision.relativeVelocity *
                (m_ImpactMultiplier * EdysVehicleMeshDeformation.ImpactScale);
            Vector3 bodyCenter = m_RenderedBody != null
                ? m_RenderedBody.TransformPoint(m_LocalBodyBounds.center)
                : transform.position;
            if (Vector3.Dot(worldImpactVelocity, bodyCenter - contact.point) < 0f)
                worldImpactVelocity = -worldImpactVelocity;

            int changed = 0;
            float broadPhaseRadiusSq = m_DamageRadius * m_DamageRadius;
            for (int i = 0; i < m_Panels.Length; ++i)
            {
                PanelState panel = m_Panels[i];
                if (panel?.Filter == null || panel.SourceMesh == null ||
                    !panel.Filter.gameObject.activeInHierarchy)
                {
                    continue;
                }
                // Off-screen deformation still updates health/destruction, but
                // skips a 10k-30k vertex CPU rebuild that the mobile Player cannot
                // see. Visible panels retain the authored deformation quality.
                if (Application.isMobilePlatform && panel.Renderer != null &&
                    !panel.Renderer.isVisible)
                {
                    continue;
                }
                if (panel.Renderer != null &&
                    panel.Renderer.bounds.SqrDistance(contact.point) > broadPhaseRadiusSq)
                {
                    continue;
                }
                if (!EnsureRuntimeMesh(panel)) continue;
                changed += DeformPanel(panel, contact.point, worldImpactVelocity);
            }
            if (changed > 0) ++m_DentCount;
        }

        private bool IsUndersideGroundContact(ContactPoint contact)
        {
            if (m_RenderedBody == null) return false;
            Vector3 localPoint = m_RenderedBody.InverseTransformPoint(contact.point);
            float underside = m_LocalBodyBounds.center.y -
                m_LocalBodyBounds.extents.y * 0.35f;
            return localPoint.y < underside &&
                Vector3.Dot(contact.normal, Vector3.up) > 0.55f;
        }

        private bool EnsureRuntimeMesh(PanelState panel)
        {
            if (panel.RuntimeMesh != null) return true;
            Mesh source = panel.SourceMesh;
            if (source == null || !source.isReadable || source.vertexCount <= 0 ||
                source.vertexCount > m_MaximumVerticesPerPanel)
            {
                return false;
            }
            panel.OriginalVertices = source.vertices;
            panel.CurrentVertices = (Vector3[])panel.OriginalVertices.Clone();
            panel.OriginalNormals = source.normals;
            panel.RuntimeMesh = Instantiate(source);
            panel.RuntimeMesh.name = source.name + " (Bike Runtime Dented)";
            panel.RuntimeMesh.MarkDynamic();
            panel.Filter.sharedMesh = panel.RuntimeMesh;
            return true;
        }

        private int DeformPanel(
            PanelState panel,
            Vector3 worldContactPoint,
            Vector3 worldImpactVelocity)
        {
            Transform panelTransform = panel.Filter.transform;
            Vector3 lossyScale = panelTransform.lossyScale;
            float largestScale = Mathf.Max(
                0.001f,
                Mathf.Max(
                    Mathf.Abs(lossyScale.x),
                    Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z))
                )
            );
            int changed = EdysVehicleMeshDeformation.DeformVertices(
                panel.CurrentVertices,
                panel.OriginalVertices,
                panelTransform.InverseTransformPoint(worldContactPoint),
                panelTransform.InverseTransformVector(worldImpactVelocity),
                m_DamageRadius / largestScale,
                m_MaximumVertexDisplacement / largestScale,
                m_MaximumVertexFracture / largestScale
            );
            if (changed <= 0) return 0;
            panel.RuntimeMesh.vertices = panel.CurrentVertices;
            panel.RuntimeMesh.RecalculateNormals();
            panel.RuntimeMesh.RecalculateBounds();
            panel.RuntimeMesh.UploadMeshData(false);
            return changed;
        }

        private void OnValidate()
        {
            m_MinimumImpactVelocity = Mathf.Max(0.1f, m_MinimumImpactVelocity);
            m_ImpactMultiplier = Mathf.Max(0.01f, m_ImpactMultiplier);
            m_DamageRadius = Mathf.Max(0.05f, m_DamageRadius);
            m_MaximumVertexDisplacement = Mathf.Max(0.01f, m_MaximumVertexDisplacement);
            m_MaximumVertexFracture = Mathf.Clamp(
                m_MaximumVertexFracture,
                0f,
                m_MaximumVertexDisplacement
            );
            m_MaximumDentCount = Mathf.Clamp(m_MaximumDentCount, 1, 20);
            m_MaximumVerticesPerPanel = Mathf.Clamp(
                m_MaximumVerticesPerPanel,
                1000,
                40000
            );
        }
    }
}
