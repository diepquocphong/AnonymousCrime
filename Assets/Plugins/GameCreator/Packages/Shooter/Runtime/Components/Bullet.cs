using System;
using System.Collections.Generic;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.Shooter
{
    [AddComponentMenu("")]
    [Icon(EditorPaths.PACKAGES + "Shooter/Editor/Gizmos/GizmoBullet.png")]
    
    [DefaultExecutionOrder(ApplicationManager.EXECUTION_ORDER_LAST_LATER)]
    
    public class Bullet : MonoBehaviour
    {
        private enum Mode
        {
            None,
            UseTracer,
            UseRigidbody,
            UseKinematic
        }
        
        public const float KINEMATIC_BULLET_MASS = 1f;
        
        private const float TRACE_TIME_STEP = 0.01f;
        
        private static readonly RaycastHit[] RAYCAST_HITS = new RaycastHit[32];
        
        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private Args m_ArgsWeapon;
        [NonSerialized] private Args m_ArgsTarget;
        
        [NonSerialized] private Mode m_Mode;
        [NonSerialized] private ShotData m_ShotData;
        [NonSerialized] private TimeMode m_TimeMode;
        [NonSerialized] private int m_MaxPierces;
        [NonSerialized] private float m_MaxDistance;
        [NonSerialized] private bool m_IsTimeout;
        [NonSerialized] private float m_Timeout;
        
        [NonSerialized] private ForceMode m_RigidbodyImpulse;
        [NonSerialized] private float m_RigidbodyImpulseForce;
        [NonSerialized] private float m_RigidbodyWindInfluence;
        [NonSerialized] private ForceMode m_RigidbodyAttraction;
        [NonSerialized] private float m_RigidbodyAttractionForce;
        [NonSerialized] private Transform m_RigidbodyAttractionTarget;

        [NonSerialized] private float m_KinematicForce;
        [NonSerialized] private Vector3 m_KinematicGravity;
        [NonSerialized] private float m_KinematicAirResistance;
        [NonSerialized] private float m_KinematicWindInfluence;
        [NonSerialized] private float m_KinematicAttractionForce;
        [NonSerialized] private Transform m_KinematicAttractionTarget;
        [NonSerialized] private LayerMask m_KinematicLayerMask;
        
        [NonSerialized] private float m_TracerSpeed;
        [NonSerialized] private GameObject m_TracerTarget;
        [NonSerialized] private Vector3 m_TracerDeviation;
        [NonSerialized] private LayerMask m_TracerLayerMask;

        // Rigidbody projectiles are pooled. Restore owner collision pairs before an
        // instance is reused by a different shooter.
        [NonSerialized] private readonly List<Collider> m_ProjectileColliders = new(4);
        [NonSerialized] private readonly List<Collider> m_OwnerColliders = new(16);
        [NonSerialized] private readonly List<Collider> m_ColliderScratch = new(16);
        
        // PROPERTIES: ----------------------------------------------------------------------------
        
        [field: NonSerialized] private Vector3 KinematicVelocity { get; set; }
        [field: NonSerialized] private Transform m_KinematicStick { get; set; }
        [field: NonSerialized] private Vector3 m_KinematicStickOffset { get; set; }
        
        [field: NonSerialized] private List<GameObject> Hits { get; } = new List<GameObject>();
        
        [field: NonSerialized] private float StartTime { get; set; }
        
        [field: NonSerialized] private Vector3 LastMuzzlePosition { get; set; }
        [field: NonSerialized] private Vector3 LastPosition { get; set; }
        [field: NonSerialized] private float Distance { get; set; }
        [field: NonSerialized] private int Pierces { get; set; }
        
        // PUBLIC METHODS: ------------------------------------------------------------------------
        
        public void SetFromTracer(
            ShotData shotData,
            float speed,
            GameObject target,
            Vector3 deviation,
            LayerMask layerMask)
        {
            this.RestoreOwnerCollisionPairs();
            this.m_ArgsWeapon.ChangeSelf(shotData.Source);
            this.m_ArgsWeapon.ChangeTarget(shotData.Prop);
            
            this.m_ArgsTarget.ChangeSelf(shotData.Source);
            
            this.m_Mode = Mode.UseTracer;
            this.m_ShotData = shotData;
            this.m_TimeMode = shotData.Source.Time;
            this.m_MaxPierces = 0;
            this.m_MaxDistance = Mathf.Infinity;

            this.m_TracerSpeed = speed;
            this.m_TracerTarget = target;
            this.m_TracerDeviation = deviation;
            this.m_TracerLayerMask = layerMask;

            this.StartTime = Time.time;
            this.LastMuzzlePosition = shotData.ShootPosition;
            this.LastPosition = shotData.ShootPosition;
            this.Distance = 0f;
            this.Pierces = 0;
            this.Hits.Clear();

            this.m_IsTimeout = false;
            this.m_Timeout = Mathf.Infinity;
        }
        
        public void SetFromRigidbody(
            ShotData shotData,
            ForceMode impulse,
            float impulseForce,
            float mass,
            float airResistance,
            float windInfluence,
            ForceMode attraction,
            float attractionForce,
            GameObject attractionTarget,
            float maxDistance,
            bool isTimeout,
            float timeout)
        {
            this.RestoreOwnerCollisionPairs();
            this.m_ArgsWeapon.ChangeSelf(shotData.Source);
            this.m_ArgsWeapon.ChangeTarget(shotData.Prop);
            
            this.m_ArgsTarget.ChangeSelf(shotData.Source);
            
            this.m_Mode = Mode.UseRigidbody;
            this.m_ShotData = shotData;
            this.m_TimeMode = shotData.Source.Time;
            this.m_MaxPierces = 0;
            this.m_MaxDistance = maxDistance;
            
            this.m_RigidbodyImpulse = impulse;
            this.m_RigidbodyImpulseForce = impulseForce;
            this.m_RigidbodyWindInfluence = windInfluence;

            this.m_RigidbodyAttraction = attraction;
            this.m_RigidbodyAttractionForce = attractionForce;
            this.m_RigidbodyAttractionTarget = attractionTarget != null
                ? attractionTarget.transform
                : null;
            
            if (this.m_RigidbodyImpulseForce <= 0f) return;
            
            Rigidbody rigidBody = this.Get<Rigidbody>();
            if (rigidBody == null) return;

            rigidBody.mass = mass;
            rigidBody.linearDamping = airResistance;
            rigidBody.linearVelocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
            rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            
            Vector3 direction = this.transform.TransformDirection(Vector3.forward);
            Vector3 force = direction.normalized * this.m_RigidbodyImpulseForce;

            this.StartTime = Time.time;
            this.LastMuzzlePosition = shotData.ShootPosition;
            this.LastPosition = shotData.ShootPosition;
            this.Distance = 0f;
            this.Pierces = 0;
            this.Hits.Clear();

            this.IgnoreOwnerCollisionPairs(shotData.Source.gameObject, shotData.Prop);
            
            rigidBody.AddForce(force, this.m_RigidbodyImpulse);

            this.m_IsTimeout = isTimeout;
            this.m_Timeout = timeout;
        }
        
        public void SetFromKinematic(
            ShotData shotData,
            float force,
            float gravity,
            float airResistance,
            float windInfluence,
            float attractionForce,
            GameObject attractionTarget,
            LayerMask layerMask,
            float maxDistance,
            bool isTimeout,
            int pierces,
            float timeout)
        {
            this.RestoreOwnerCollisionPairs();
            this.m_ArgsWeapon.ChangeSelf(shotData.Source);
            this.m_ArgsWeapon.ChangeTarget(shotData.Prop);
            
            this.m_ArgsTarget.ChangeSelf(shotData.Source);
            
            this.m_Mode = Mode.UseKinematic;
            this.m_ShotData = shotData;
            this.m_TimeMode = shotData.Source.Time;
            this.m_MaxPierces = pierces;
            this.m_MaxDistance = maxDistance;

            this.m_KinematicForce = force;
            this.m_KinematicGravity = Vector3.down * gravity;
            this.m_KinematicAirResistance = airResistance;
            this.m_KinematicWindInfluence = Mathf.Clamp01(windInfluence);
            this.m_KinematicAttractionForce = attractionForce;
            this.m_KinematicAttractionTarget = attractionTarget != null
                ? attractionTarget.transform
                : null;
            
            this.m_KinematicLayerMask = layerMask;
            
            this.KinematicVelocity = this.m_ShotData.ShootDirection.normalized * this.m_KinematicForce;
            this.m_KinematicStick = null;
            this.m_KinematicStickOffset = Vector3.zero;

            this.StartTime = Time.time;
            this.LastMuzzlePosition = shotData.ShootPosition;
            this.LastPosition = shotData.ShootPosition;
            this.Distance = 0f;
            this.Pierces = 0;
            this.Hits.Clear();
            
            this.m_IsTimeout = isTimeout;
            this.m_Timeout = timeout;
        }
        
        // INITIALIZE: ----------------------------------------------------------------------------

        private void Awake()
        {
            this.m_ArgsWeapon = new Args(this.gameObject);
            this.m_ArgsTarget = new Args(this.gameObject);
        }

        private void OnEnable()
        {
            TrailRenderer[] trails = this.GetComponentsInChildren<TrailRenderer>();
            foreach (TrailRenderer trail in trails)
            {
                trail.Clear();
            }
        }

        private void OnDisable()
        {
            this.RestoreOwnerCollisionPairs();
            this.m_Mode = Mode.None;
        }

        private void OnDestroy()
        {
            this.RestoreOwnerCollisionPairs();
        }

        // CYCLE METHODS: -------------------------------------------------------------------------

        private void Update()
        {
            if (this.m_IsTimeout && this.StartTime + this.m_Timeout <= Time.time)
            {
                float distance = Vector3.Distance(this.transform.position, this.m_ShotData.ShootPosition);
                this.ReportHit(this.gameObject, this.transform.position, Vector3.up, distance);
                
                this.gameObject.SetActive(false);
                return;
            }
            
            if (Time.time < this.StartTime + this.m_ShotData.Delay)
            {
                this.transform.position = this.m_ShotData.Weapon.Muzzle.GetPosition(this.m_ArgsWeapon);
                this.transform.rotation = this.m_ShotData.Weapon.Muzzle.GetRotation(this.m_ArgsWeapon);
                this.LastPosition = this.transform.position;
                this.LastMuzzlePosition = this.transform.position;
                
                return;
            }
            
            Vector3 direction;

            switch (this.m_Mode)
            {
                case Mode.UseTracer:
                    direction = this.UpdateTracer();
                    break;
                case Mode.UseKinematic:
                    direction = this.UpdateKinematic();
                    break;
                
                case Mode.None:
                case Mode.UseRigidbody:
                default: return;
            }
            
            if (direction != Vector3.zero)
            {
                this.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            this.Distance += Vector3.Distance(this.transform.position, this.LastPosition);
            this.LastPosition = this.transform.position;
            
            if (this.Distance >= this.m_MaxDistance)
            {
                this.gameObject.SetActive(false);
            }
        }

        private void FixedUpdate()
        {
            if (this.m_Mode != Mode.UseRigidbody) return;
            
            if (Time.time < this.StartTime + this.m_ShotData.Delay)
            {
                this.transform.position = this.m_ShotData.Weapon.Muzzle.GetPosition(this.m_ArgsWeapon);
                this.transform.rotation = this.m_ShotData.Weapon.Muzzle.GetRotation(this.m_ArgsWeapon);
                this.LastPosition = this.transform.position;
                this.LastMuzzlePosition = this.transform.position;
                
                return;
            }
            
            Vector3 direction = this.UpdateRigidbody();
            if (direction != Vector3.zero)
            {
                this.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            this.Distance += Vector3.Distance(this.transform.position, this.LastPosition);
            this.LastPosition = this.transform.position;
            
            if (this.Distance >= this.m_MaxDistance)
            {
                this.gameObject.SetActive(false);
            }
        }
        
        // PHYSICS: -------------------------------------------------------------------------------

        private void OnCollisionEnter(Collision hit)
        {
            if (this.m_Mode == Mode.None) return;

            if (Time.time < this.StartTime + this.m_ShotData.Delay) return;

            bool hasContact = hit.contactCount > 0;
            ContactPoint contact = hasContact ? hit.GetContact(0) : default;
            Vector3 point = hasContact
                ? contact.point
                : hit.collider.ClosestPoint(this.transform.position);
            Vector3 normal = hasContact
                ? contact.normal
                : this.ResolveFallbackHitNormal(point);
            float distance = Vector3.Distance(this.m_ShotData.ShootPosition, point);
            
            if (this.m_IsTimeout)
            {
                this.m_KinematicStick = hit.transform;
                this.m_KinematicStickOffset = hit.transform.InverseTransformDirection(
                    this.transform.position
                );
            }
            else
            {
                this.ReportHit(hit.gameObject, point, normal, distance);
            }
        }

        private void OnTriggerEnter(Collider hit)
        {
            if (this.m_Mode == Mode.None) return;
            // Physical projectiles follow the same solid-surface policy as Shooter raycasts.
            // Invisible interaction/damage volumes must not detonate an RPG in mid-air.
            if (hit == null || hit.isTrigger) return;
            
            if (Time.time < this.StartTime + this.m_ShotData.Delay) return;
            
            Vector3 point = hit.ClosestPoint(this.transform.position);
            Vector3 normal = this.ResolveFallbackHitNormal(point);
            float distance = Vector3.Distance(this.m_ShotData.ShootPosition, point);

            if (!this.m_IsTimeout)
            {
                this.ReportHit(hit.gameObject, point, normal, distance);
            }
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private Vector3 UpdateRigidbody()
        {
            Rigidbody rigidBody = this.Get<Rigidbody>();
            if (rigidBody == null) return this.transform.forward;
            
            if (this.m_RigidbodyWindInfluence > 0f)
            {
                Vector3 windForce = WindManager.Instance.Wind * this.m_RigidbodyWindInfluence;
                rigidBody.AddForce(windForce, ForceMode.Force);
            }
            
            if (this.m_RigidbodyAttractionForce > 0f && this.m_RigidbodyAttractionTarget != null)
            {
                Vector3 direction = this.m_RigidbodyAttractionTarget.position - this.transform.position;
                if (direction != Vector3.zero)
                {
                    Vector3 force = direction.normalized * this.m_RigidbodyAttractionForce;
                    rigidBody.AddForce(force, this.m_RigidbodyAttraction);
                }
            }
            
            return rigidBody.linearVelocity != Vector3.zero
                ? rigidBody.linearVelocity.normalized
                : this.transform.forward;
        }
        
        private Vector3 UpdateKinematic()
        {
            if (this.m_KinematicStick != null)
            {
                this.transform.position = this.m_KinematicStick.TransformPoint(
                    this.m_KinematicStickOffset
                );
                
                return Vector3.zero;
            }
            
            Vector3 drag = -1f * this.m_KinematicAirResistance * this.KinematicVelocity;
            Vector3 gravity = this.m_KinematicGravity * KINEMATIC_BULLET_MASS; 
            Vector3 wind = WindManager.Instance.Wind * this.m_KinematicWindInfluence;
            Vector3 attraction = Vector3.zero;

            if (this.m_KinematicAttractionTarget != null)
            {
                Vector3 direction = this.m_KinematicAttractionTarget.position - this.transform.position;
                if (direction != Vector3.zero)
                {
                    attraction = direction.normalized * this.m_KinematicAttractionForce;
                }
            }

            Vector3 totalForce = drag + gravity + wind + attraction;
            Vector3 acceleration = totalForce / KINEMATIC_BULLET_MASS;
            
            this.KinematicVelocity += acceleration * this.m_TimeMode.DeltaTime;
            
            Vector3 deltaTranslation = this.KinematicVelocity * this.m_TimeMode.DeltaTime;
            Vector3 nextPosition = this.transform.position + deltaTranslation;
            Vector3 nextDirection = nextPosition - transform.position;

            if (!this.m_IsTimeout)
            {
                int numHits = Physics.RaycastNonAlloc(
                    this.transform.position,
                    nextDirection.normalized,
                    RAYCAST_HITS,
                    nextDirection.magnitude,
                    this.m_KinematicLayerMask,
                    QueryTriggerInteraction.Ignore
                );

                for (int i = 0; i < numHits; ++i)
                {
                    RaycastHit hit = RAYCAST_HITS[i];
                    float distance = Vector3.Distance(
                        this.m_ShotData.ShootPosition,
                        hit.point
                    );
                    this.ReportHit(
                        hit.collider.gameObject,
                        hit.point,
                        hit.normal,
                        distance
                    );
                }   
            }
            
            this.transform.position = nextPosition;
            return this.KinematicVelocity.normalized;
        }
        
        private Vector3 UpdateTracer()
        {
            if (this.m_TracerTarget == null) return Vector3.zero;

            float totalDistance = Vector3.Distance(
                this.m_TracerTarget.transform.position,
                this.LastMuzzlePosition
            );
            
            float elapsedTime = this.m_TimeMode.Time - (this.StartTime + this.m_ShotData.Delay);
            float t = this.m_TracerSpeed * elapsedTime / totalDistance;
            
            Vector3 nextPosition = Bezier.Get(
                this.LastMuzzlePosition,
                this.m_TracerTarget.transform.position,
                this.m_TracerDeviation,
                Vector3.zero, 
                t
            );
            
            Vector3 nextDirection = nextPosition - transform.position;

            if (!this.m_IsTimeout)
            {
                int numHits = Physics.RaycastNonAlloc(
                    this.transform.position,
                    nextDirection.normalized,
                    RAYCAST_HITS,
                    nextDirection.magnitude,
                    this.m_TracerLayerMask,
                    QueryTriggerInteraction.Ignore
                );

                for (int i = 0; i < numHits; ++i)
                {
                    RaycastHit hit = RAYCAST_HITS[i];
                    float distance = Vector3.Distance(
                        this.m_ShotData.ShootPosition,
                        hit.point
                    );
                    this.ReportHit(
                        hit.collider.gameObject,
                        hit.point,
                        hit.normal,
                        distance
                    );
                }   
            }
            
            this.transform.position = nextPosition;
            
            if (t > 1f - float.Epsilon)
            {
                this.gameObject.SetActive(false);
            }
            
            return nextDirection.normalized;
        }

        private void ReportHit(GameObject hit, Vector3 point, Vector3 normal, float distance)
        {
            if (this.IsOwnerHit(hit)) return;
            if (this.Hits.Contains(hit)) return;
            this.Hits.Add(hit);

            Vector3 hitNormal = normal.sqrMagnitude > 0.000001f
                ? normal.normalized
                : this.ResolveFallbackHitNormal(point);
            this.m_ShotData.UpdateHit(
                hit.gameObject,
                point,
                hitNormal,
                distance,
                this.Pierces
            );
            
            this.Pierces += 1;
            
            this.m_ArgsTarget.ChangeTarget(hit);
            ShooterWeapon weapon = this.m_ShotData.Weapon;
            
            if (weapon.CanHit(this.m_ShotData, this.m_ArgsTarget))
            {
                weapon.OnHit(this.m_ShotData, this.m_ArgsTarget);
                
                MaterialSounds.Play(
                    this.m_ArgsTarget,
                    point,
                    hitNormal,
                    hit,
                    this.m_ShotData.ImpactSound,
                    UnityEngine.Random.Range(-180f, 180f)
                );
                
                this.m_ShotData.ImpactEffect?.Get(
                    this.m_ArgsTarget,
                    point,
                    Quaternion.LookRotation(hitNormal)
                );
            }

            if (this.Pierces > this.m_MaxPierces)
            {
                this.gameObject.SetActive(false);
            }
        }

        private Vector3 ResolveFallbackHitNormal(Vector3 point)
        {
            Rigidbody body = this.Get<Rigidbody>();
            Vector3 travel = body != null && body.linearVelocity.sqrMagnitude > 0.000001f
                ? body.linearVelocity
                : point - this.m_ShotData.ShootPosition;
            if (travel.sqrMagnitude <= 0.000001f) travel = this.transform.forward;
            return -travel.normalized;
        }

        private bool IsOwnerHit(GameObject hit)
        {
            if (hit == null) return false;

            Transform hitTransform = hit.transform;
            GameObject source = this.m_ShotData.Source != null
                ? this.m_ShotData.Source.gameObject
                : null;
            if (source != null &&
                (hit == source || hitTransform.IsChildOf(source.transform)))
            {
                return true;
            }

            GameObject prop = this.m_ShotData.Prop;
            return prop != null &&
                   (hit == prop || hitTransform.IsChildOf(prop.transform));
        }

        private void IgnoreOwnerCollisionPairs(GameObject source, GameObject prop)
        {
            this.m_ProjectileColliders.Clear();
            this.GetComponentsInChildren(true, this.m_ProjectileColliders);
            this.m_OwnerColliders.Clear();
            this.AppendOwnerColliders(source);
            this.AppendOwnerColliders(prop);

            for (int i = 0; i < this.m_ProjectileColliders.Count; ++i)
            {
                Collider projectileCollider = this.m_ProjectileColliders[i];
                if (projectileCollider == null) continue;

                for (int j = 0; j < this.m_OwnerColliders.Count; ++j)
                {
                    Collider ownerCollider = this.m_OwnerColliders[j];
                    if (ownerCollider == null || ownerCollider == projectileCollider) continue;
                    Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
                }
            }
        }

        private void AppendOwnerColliders(GameObject root)
        {
            if (root == null) return;

            this.m_ColliderScratch.Clear();
            root.GetComponentsInChildren(true, this.m_ColliderScratch);
            for (int i = 0; i < this.m_ColliderScratch.Count; ++i)
            {
                Collider candidate = this.m_ColliderScratch[i];
                if (candidate == null || this.m_OwnerColliders.Contains(candidate)) continue;
                this.m_OwnerColliders.Add(candidate);
            }
        }

        private void RestoreOwnerCollisionPairs()
        {
            for (int i = 0; i < this.m_ProjectileColliders.Count; ++i)
            {
                Collider projectileCollider = this.m_ProjectileColliders[i];
                if (projectileCollider == null) continue;

                for (int j = 0; j < this.m_OwnerColliders.Count; ++j)
                {
                    Collider ownerCollider = this.m_OwnerColliders[j];
                    if (ownerCollider == null || ownerCollider == projectileCollider) continue;
                    Physics.IgnoreCollision(projectileCollider, ownerCollider, false);
                }
            }

            this.m_ProjectileColliders.Clear();
            this.m_OwnerColliders.Clear();
            this.m_ColliderScratch.Clear();
        }
        
        // PUBLIC STATIC METHODS: -----------------------------------------------------------------
        
        public static void TracePositions(
            List<Vector3> points,
            Vector3 position,
            Vector3 force,
            float maxDistance,
            float minDeltaDistance,
            float airResistance,
            float windInfluence,
            float gravity,
            float mass)
        {
            Vector3 nextPoint = position;
            Vector3 prevPoint = position;
            
            points.Clear();
            points.Add(nextPoint);
            
            float distance = 0f;
            float currentDeltaDistance = 0f;

            while (distance < maxDistance)
            {
                Vector3 dragForce = -1f * airResistance * force;
                Vector3 gravityForce = Vector3.down * (gravity * mass);
                Vector3 windForce = WindManager.Instance.Wind * windInfluence;
                
                Vector3 acceleration = (dragForce + gravityForce + windForce) / mass;
                
                force += acceleration * TRACE_TIME_STEP;
                nextPoint += force * TRACE_TIME_STEP;

                float deltaStep = (nextPoint - prevPoint).magnitude;
                
                currentDeltaDistance += deltaStep;
                distance += deltaStep;
                prevPoint = nextPoint;

                if (currentDeltaDistance >= minDeltaDistance)
                {
                    points.Add(nextPoint);
                    currentDeltaDistance = 0f;
                }
            }
        }
    }
}
