using System;
using UnityEngine;

namespace FranklinGame.Vehicles
{
    /// <summary>
    /// Sim-Cade impact adapter for the render-mesh deformation algorithm ported
    /// from Edy's Vehicle Physics 5.5.3 VehicleDamage. Only visual mesh damage is
    /// retained: no EVP controller, input, wheels, nodes or deformable collider.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    [RequireComponent(typeof(SimcadeCarImpactAudio), typeof(SimcadeCarDriver))]
    public sealed class SimcadeCarDeformation : MonoBehaviour
    {
        private const int MAXIMUM_PANELS_PER_IMPACT = 2;

        [Header("References")]
        [SerializeField] private SimcadeCarImpactAudio m_ImpactAudio;
        [SerializeField] private SimcadeCarDriver m_Driver;
        [SerializeField] private BoxCollider m_BodyCollider;
        [SerializeField] private MeshFilter[] m_DeformablePanels = Array.Empty<MeshFilter>();

        [Header("Edy Mesh Deformation Profile")]
        [Tooltip("Edy Sport Coupe profile: minimum relative impact velocity in m/s.")]
        [SerializeField, Min(0.1f)] private float m_MinimumImpactVelocity = 2.5f;
        [SerializeField, Min(0.01f)] private float m_ImpactMultiplier = 1f;
        [SerializeField, Min(0.05f)] private float m_DamageRadius = 0.5f;
        [SerializeField, Min(0.01f)] private float m_MaximumVertexDisplacement = 0.2f;
        [SerializeField, Min(0f)] private float m_MaximumVertexFracture;

        [Header("Mobile Event Budget")]
        [SerializeField, Range(1, 20)] private int m_MaximumDentCount = 8;
        [SerializeField, Range(1000, 40000)] private int m_MaximumVerticesPerPanel = 12000;

        [Header("Steering Misalignment")]
        [SerializeField, Min(0f)] private float m_SteeringDamageStartSeverity = 4.5f;
        [SerializeField, Min(0.1f)] private float m_SteeringDamageFullSeverity = 18f;
        [SerializeField, Range(0f, 0.1f)] private float m_MinSteeringBiasPerImpact = 0.005f;
        [SerializeField, Range(0f, 0.1f)] private float m_MaxSteeringBiasPerImpact = 0.055f;

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
        private int m_DentCount;
        private int m_LastDeformedVertexCount;

        public bool IsConfigured => m_ImpactAudio != null && m_Driver != null &&
            m_BodyCollider != null && m_DeformablePanels != null &&
            m_DeformablePanels.Length >= 5;
        public bool UsesEdysMeshDeformation => true;
        public int DentCount => m_DentCount;
        public int LastDeformedVertexCount => m_LastDeformedVertexCount;
        public int DeformablePanelCount => m_DeformablePanels?.Length ?? 0;
        public int MaximumDentCount => m_MaximumDentCount;
        public int MaximumVerticesPerPanel => m_MaximumVerticesPerPanel;
        public float MinimumImpactVelocity => m_MinimumImpactVelocity;
        public float DamageRadius => m_DamageRadius;
        public float MaximumVertexDisplacement => m_MaximumVertexDisplacement;
        public float MaximumVertexFracture => m_MaximumVertexFracture;
        public float MaximumSteeringBiasPerImpact => m_MaxSteeringBiasPerImpact;

        private void Awake()
        {
            if (m_ImpactAudio == null) m_ImpactAudio = GetComponent<SimcadeCarImpactAudio>();
            if (m_Driver == null) m_Driver = GetComponent<SimcadeCarDriver>();
            if (m_BodyCollider == null) m_BodyCollider = GetComponent<BoxCollider>();
            BuildPanelStates();
        }

        private void OnEnable()
        {
            if (m_ImpactAudio == null) m_ImpactAudio = GetComponent<SimcadeCarImpactAudio>();
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

        public void Configure(
            SimcadeCarImpactAudio impactAudio,
            SimcadeCarDriver driver,
            BoxCollider bodyCollider,
            MeshFilter[] deformablePanels)
        {
            m_ImpactAudio = impactAudio;
            m_Driver = driver;
            m_BodyCollider = bodyCollider;
            m_DeformablePanels = deformablePanels ?? Array.Empty<MeshFilter>();

            // Tuned values used by EVP's Sport Coupe example.
            m_MinimumImpactVelocity = 2.5f;
            m_ImpactMultiplier = 1f;
            m_DamageRadius = 0.5f;
            m_MaximumVertexDisplacement = 0.2f;
            m_MaximumVertexFracture = 0f;
            m_MaximumDentCount = 8;
            m_MaximumVerticesPerPanel = 12000;
            m_SteeringDamageStartSeverity = 4.5f;
            m_SteeringDamageFullSeverity = 18f;
            m_MinSteeringBiasPerImpact = 0.005f;
            m_MaxSteeringBiasPerImpact = 0.055f;
        }

        public void ResetDeformation(bool resetSteering = true)
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
                else
                {
                    panel.RuntimeMesh.RecalculateNormals();
                }
                panel.RuntimeMesh.RecalculateBounds();
                panel.RuntimeMesh.UploadMeshData(false);
            }

            m_DentCount = 0;
            m_LastDeformedVertexCount = 0;
            if (resetSteering) m_Driver?.ResetDamageSteering();
        }

        private void BuildPanelStates()
        {
            int count = m_DeformablePanels?.Length ?? 0;
            m_Panels = new PanelState[count];
            for (int i = 0; i < count; ++i)
            {
                MeshFilter filter = m_DeformablePanels[i];
                m_Panels[i] = new PanelState
                {
                    Filter = filter,
                    Renderer = filter != null ? filter.GetComponent<Renderer>() : null,
                    SourceMesh = filter != null ? filter.sharedMesh : null
                };
            }
        }

        private void OnImpactAccepted(Collision collision, bool _, float severity)
        {
            if (collision == null || collision.contactCount <= 0 ||
                collision.relativeVelocity.sqrMagnitude <
                m_MinimumImpactVelocity * m_MinimumImpactVelocity)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            if (IsUndersideGroundContact(contact)) return;

            ApplySteeringDamage(contact.point, severity);
            if (m_DentCount >= m_MaximumDentCount) return;

            // EVP multiplies its world impact velocity by multiplier * 0.02.
            // Ensure the vector points into the Car because Collision's relative
            // velocity direction depends on which body receives the callback.
            Vector3 worldImpactVelocity = collision.relativeVelocity *
                (m_ImpactMultiplier * EdysVehicleMeshDeformation.ImpactScale);
            if (m_BodyCollider != null)
            {
                Vector3 bodyCenter = transform.TransformPoint(m_BodyCollider.center);
                Vector3 towardBody = bodyCenter - contact.point;
                if (Vector3.Dot(worldImpactVelocity, towardBody) < 0f)
                    worldImpactVelocity = -worldImpactVelocity;
            }

            int changedVertices = 0;
            float broadPhaseRadiusSq = m_DamageRadius * m_DamageRadius;
            PanelState nearestPanel = null;
            PanelState secondPanel = null;
            float nearestDistance = float.PositiveInfinity;
            float secondDistance = float.PositiveInfinity;
            for (int i = 0; i < m_Panels.Length; ++i)
            {
                PanelState panel = m_Panels[i];
                if (panel?.Filter == null || panel.SourceMesh == null ||
                    !panel.SourceMesh.isReadable ||
                    panel.SourceMesh.vertexCount <= 0 ||
                    panel.SourceMesh.vertexCount > m_MaximumVerticesPerPanel ||
                    !panel.Filter.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float panelDistance = panel.Renderer != null
                    ? panel.Renderer.bounds.SqrDistance(contact.point)
                    : 0f;
                if (panelDistance > broadPhaseRadiusSq)
                {
                    continue;
                }

                if (panelDistance < nearestDistance)
                {
                    secondPanel = nearestPanel;
                    secondDistance = nearestDistance;
                    nearestPanel = panel;
                    nearestDistance = panelDistance;
                }
                else if (panelDistance < secondDistance)
                {
                    secondPanel = panel;
                    secondDistance = panelDistance;
                }
            }

            // A collision can overlap many split render pieces. Limiting Edy's
            // O(vertex-count) pass to the two closest panels prevents one impact
            // from scanning every body mesh while preserving the visible dent.
            if (nearestPanel != null && EnsureRuntimeMesh(nearestPanel))
                changedVertices += DeformPanel(
                    nearestPanel,
                    contact.point,
                    worldImpactVelocity
                );
            if (MAXIMUM_PANELS_PER_IMPACT > 1 && secondPanel != null &&
                EnsureRuntimeMesh(secondPanel))
            {
                changedVertices += DeformPanel(
                    secondPanel,
                    contact.point,
                    worldImpactVelocity
                );
            }

            m_LastDeformedVertexCount = changedVertices;
            if (changedVertices > 0) ++m_DentCount;
        }

        private bool IsUndersideGroundContact(ContactPoint contact)
        {
            if (m_BodyCollider == null) return false;
            Vector3 localPoint = transform.InverseTransformPoint(contact.point);
            float undersideLimit = m_BodyCollider.center.y -
                m_BodyCollider.size.y * 0.28f;
            return localPoint.y < undersideLimit;
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
            panel.RuntimeMesh.name = source.name + " (Edy Runtime Dented)";
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

            Vector3 localContactPoint = panelTransform.InverseTransformPoint(
                worldContactPoint
            );
            // InverseTransformVector preserves world-space displacement on the
            // Car.FBX hierarchy, whose MainBody scale is not 1.
            Vector3 localImpactVelocity = panelTransform.InverseTransformVector(
                worldImpactVelocity
            );
            float localDamageRadius = m_DamageRadius / largestScale;
            float localMaxDisplacement = m_MaximumVertexDisplacement / largestScale;
            float localMaxFracture = m_MaximumVertexFracture / largestScale;

            int changed = EdysVehicleMeshDeformation.DeformVertices(
                panel.CurrentVertices,
                panel.OriginalVertices,
                localContactPoint,
                localImpactVelocity,
                localDamageRadius,
                localMaxDisplacement,
                localMaxFracture
            );
            if (changed <= 0) return 0;

            panel.RuntimeMesh.vertices = panel.CurrentVertices;
            // EVP recalculates both after every accepted deformation. This is
            // intentionally retained because it makes dents visible in lighting
            // and prevents the changed mesh from using stale culling bounds.
            panel.RuntimeMesh.RecalculateNormals();
            panel.RuntimeMesh.RecalculateBounds();
            panel.RuntimeMesh.UploadMeshData(false);
            return changed;
        }

        private void ApplySteeringDamage(Vector3 worldPoint, float severity)
        {
            if (m_Driver == null || severity < m_SteeringDamageStartSeverity ||
                m_BodyCollider == null)
            {
                return;
            }

            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            float halfWidth = Mathf.Max(0.1f, m_BodyCollider.size.x * 0.5f);
            float lateral = Mathf.Clamp(
                (localPoint.x - m_BodyCollider.center.x) / halfWidth,
                -1f,
                1f
            );
            if (Mathf.Abs(lateral) < 0.12f) return;

            float amount = Mathf.InverseLerp(
                m_SteeringDamageStartSeverity,
                Mathf.Max(m_SteeringDamageStartSeverity + 0.1f, m_SteeringDamageFullSeverity),
                severity
            );
            float bias = Mathf.Lerp(
                m_MinSteeringBiasPerImpact,
                m_MaxSteeringBiasPerImpact,
                amount
            );
            m_Driver.AddDamageSteeringBias(
                Mathf.Sign(lateral) * bias * Mathf.Max(0.25f, Mathf.Abs(lateral))
            );
        }

        private void OnValidate()
        {
            m_MinimumImpactVelocity = Mathf.Max(0.1f, m_MinimumImpactVelocity);
            m_ImpactMultiplier = Mathf.Max(0.01f, m_ImpactMultiplier);
            m_DamageRadius = Mathf.Max(0.05f, m_DamageRadius);
            m_MaximumVertexDisplacement = Mathf.Max(
                0.01f,
                m_MaximumVertexDisplacement
            );
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
            m_SteeringDamageStartSeverity = Mathf.Max(0f, m_SteeringDamageStartSeverity);
            m_SteeringDamageFullSeverity = Mathf.Max(
                m_SteeringDamageStartSeverity + 0.1f,
                m_SteeringDamageFullSeverity
            );
            m_MinSteeringBiasPerImpact = Mathf.Clamp(m_MinSteeringBiasPerImpact, 0f, 0.1f);
            m_MaxSteeringBiasPerImpact = Mathf.Clamp(
                m_MaxSteeringBiasPerImpact,
                m_MinSteeringBiasPerImpact,
                0.1f
            );
        }
    }
}
