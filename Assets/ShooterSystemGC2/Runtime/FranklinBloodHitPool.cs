using PampelGames.BloodFactory;
using PGBloodFactory = PampelGames.BloodFactory.BloodFactory;
using UnityEngine;

namespace FranklinGame.Shooter
{
    /// <summary>
    /// Fixed-size mobile pool for BloodFactory hit splashes. BloodFactory's default Execute
    /// path instantiates an extra particle object and collision decals for every hit. Shooter
    /// only needs the authored embedded spray, so this pool replays that spray and recycles
    /// the oldest slot when its hard budget is full.
    /// </summary>
    [DefaultExecutionOrder(101)]
    public sealed class FranklinBloodHitPool : MonoBehaviour
    {
        private const int MAX_ACTIVE_HITS = 6;
        private const int MAX_SPAWNS_PER_FRAME = 2;
        private const int MAX_PARTICLES_PER_SYSTEM = 96;
        private const float MIN_LIFETIME = 0.35f;
        private const float MAX_LIFETIME = 2f;

        private struct Slot
        {
            public GameObject Root;
            public PGBloodFactory Factory;
            public float ExpireAt;
        }

        private static FranklinBloodHitPool s_Instance;

        private readonly Slot[] m_Slots = new Slot[MAX_ACTIVE_HITS];
        private GameObject m_SourcePrefab;
        private int m_NextSlot;
        private int m_BudgetFrame = -1;
        private int m_SpawnsThisFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
        }

        public static void Spawn(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            LayerMask collisionMask,
            float requestedLifetime)
        {
            if (prefab == null) return;

            FranklinBloodHitPool pool = GetOrCreate();
            if (pool == null) return;
            pool.SpawnInternal(
                prefab,
                position,
                rotation,
                collisionMask,
                requestedLifetime
            );
        }

        private static FranklinBloodHitPool GetOrCreate()
        {
            if (s_Instance != null) return s_Instance;

            s_Instance = FindAnyObjectByType<FranklinBloodHitPool>();
            if (s_Instance != null) return s_Instance;

            GameObject owner = new("[Franklin Blood Hit Pool]");
            s_Instance = owner.AddComponent<FranklinBloodHitPool>();
            return s_Instance;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            this.enabled = false;
        }

        private void Update()
        {
            float now = Time.time;
            bool hasActiveHit = false;
            for (int i = 0; i < this.m_Slots.Length; ++i)
            {
                Slot slot = this.m_Slots[i];
                if (slot.Root == null || !slot.Root.activeSelf) continue;
                if (now < slot.ExpireAt)
                {
                    hasActiveHit = true;
                    continue;
                }

                Deactivate(ref slot);
                this.m_Slots[i] = slot;
            }

            if (!hasActiveHit) this.enabled = false;
        }

        private void SpawnInternal(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            LayerMask collisionMask,
            float requestedLifetime)
        {
            int frame = Time.frameCount;
            if (this.m_BudgetFrame != frame)
            {
                this.m_BudgetFrame = frame;
                this.m_SpawnsThisFrame = 0;
            }
            if (this.m_SpawnsThisFrame >= MAX_SPAWNS_PER_FRAME) return;

            if (this.m_SourcePrefab != prefab)
            {
                this.ReleaseAllSlots();
                this.m_SourcePrefab = prefab;
            }

            int slotIndex = this.FindAvailableSlot();
            Slot slot = this.m_Slots[slotIndex];
            if (slot.Root == null && !this.CreateSlot(prefab, ref slot)) return;

            Deactivate(ref slot);
            slot.Root.transform.SetPositionAndRotation(position, rotation);
            slot.Root.SetActive(true);

            PGBloodFactory factory = slot.Factory;
            factory.executeOnAwake = false;
            factory.collisionLayer = collisionMask;

            // Collision-spawned BloodFactory decals live for ten seconds and allocate per
            // droplet. The embedded hit spray remains visible without that unbounded path.
            for (int i = 0; i < factory.bloodParticles.Count; ++i)
            {
                BloodParticle particle = factory.bloodParticles[i];
                if (particle == null) continue;
                particle.spawnActive = false;
                particle.Execute(collisionMask);
            }

            slot.ExpireAt = Time.time + Mathf.Clamp(
                requestedLifetime,
                MIN_LIFETIME,
                MAX_LIFETIME
            );
            this.m_Slots[slotIndex] = slot;
            this.enabled = true;
            this.m_NextSlot = (slotIndex + 1) % this.m_Slots.Length;
            this.m_SpawnsThisFrame += 1;
        }

        private int FindAvailableSlot()
        {
            for (int offset = 0; offset < this.m_Slots.Length; ++offset)
            {
                int index = (this.m_NextSlot + offset) % this.m_Slots.Length;
                GameObject root = this.m_Slots[index].Root;
                if (root == null || !root.activeSelf) return index;
            }

            // Hard cap: recycle the oldest round-robin slot instead of growing the pool.
            return this.m_NextSlot;
        }

        private bool CreateSlot(GameObject prefab, ref Slot slot)
        {
            GameObject root = Instantiate(prefab, this.transform);
            root.name = prefab.name + " (Franklin pooled)";

            PGBloodFactory factory = root.GetComponent<PGBloodFactory>() ??
                                     root.GetComponentInChildren<PGBloodFactory>(true);
            if (factory == null)
            {
                Destroy(root);
                return false;
            }

            factory.executeOnAwake = false;
            foreach (ParticleSystem particle in
                     root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                main.maxParticles = Mathf.Min(
                    main.maxParticles,
                    MAX_PARTICLES_PER_SYSTEM
                );
            }
            root.SetActive(false);
            slot.Root = root;
            slot.Factory = factory;
            slot.ExpireAt = 0f;
            return true;
        }

        private static void Deactivate(ref Slot slot)
        {
            if (slot.Root == null || slot.Factory == null) return;

            for (int i = 0; i < slot.Factory.bloodParticles.Count; ++i)
            {
                BloodParticle bloodParticle = slot.Factory.bloodParticles[i];
                ParticleSystem particle = bloodParticle != null
                    ? bloodParticle.particle
                    : null;
                if (particle != null)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            slot.Root.SetActive(false);
            slot.ExpireAt = 0f;
        }

        private void ReleaseAllSlots()
        {
            for (int i = 0; i < this.m_Slots.Length; ++i)
            {
                Slot slot = this.m_Slots[i];
                if (slot.Root != null) Destroy(slot.Root);
                this.m_Slots[i] = default;
            }
            this.m_NextSlot = 0;
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }
    }
}
