using System;
using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Shooter;
using Unity.Netcode.Components;

namespace Blocks.Gameplay.Extended
{
    public class SpawnInteractableEffect : NetworkBehaviour, IProjectileEffect
    {
        [Header("Spawn Settings")]
        [SerializeField] private NetworkObject interactablePrefab;
        [SerializeField] private bool alignToSurface = true;
        [SerializeField] private float spawnOffset = 0.1f;

        [Header("Collision Settings")]
        [SerializeField] private Collider[] colliders;

        [Header("Lifetime Settings")]
        [SerializeField] private bool deferredDespawn = true;
        [SerializeField] private int deferredDespawnTicks = 2;

        private GameObject m_Owner;
        private ModularProjectile m_Projectile;
        private ShootingContext m_ShootingContext;
        private bool m_HasSpawned;

        public bool IsDeferredDespawnEnabled => deferredDespawn;
        public int DeferredDespawnTicks => deferredDespawnTicks;

        public event Action<ModularProjectile> OnEffectComplete;

        // ═══════════════════════════════════════════════════════════════
        // IProjectileEffect Implementation
        // ═══════════════════════════════════════════════════════════════

        public void Initialize(ModularProjectile projectile)
        {
            m_Projectile = projectile;
        }

        public void Setup(GameObject owner, IWeapon sourceWeapon, ShootingContext context)
        {
            m_Owner = owner;
            m_ShootingContext = context;
            m_HasSpawned = false;
            IgnoreCollision(owner, gameObject, true);
        }

        public void OnLaunch()
        {
            m_HasSpawned = false;
            EnableColliders(true);
        }

        public void ProcessUpdate()
        {
            // No time-based logic for this effect
        }

        public void Cleanup()
        {
            if (m_Owner != null)
            {
                IgnoreCollision(m_Owner, gameObject, false);
            }
        }

        public ContactEventHandlerInfo GetContactEventHandlerInfo()
        {
            return new ContactEventHandlerInfo
            {
                ProvideNonRigidBodyContactEvents = true,
                HasContactEventPriority = HasAuthority
            };
        }

        public Rigidbody GetRigidbody()
        {
            return m_Projectile != null ? m_Projectile.GetComponent<Rigidbody>() : null;
        }

        // ═══════════════════════════════════════════════════════════════
        // Collision Handling
        // ═══════════════════════════════════════════════════════════════

        public void ContactEvent(ulong eventId, Vector3 averageNormal, Rigidbody collidingBody,
            Vector3 contactPoint, bool hasCollisionStay = false, Vector3 averagedCollisionStayNormal = default)
        {
            if (!IsSpawned || m_HasSpawned || !HasAuthority) return;
            if (collidingBody != null && collidingBody.gameObject == m_Owner) return;

            SpawnInteractable(contactPoint, averageNormal);
        }

        // ═══════════════════════════════════════════════════════════════
        // Spawning
        // ═══════════════════════════════════════════════════════════════

        private void SpawnInteractable(Vector3 contactPoint, Vector3 surfaceNormal)
        {
            if (!HasAuthority || m_HasSpawned || interactablePrefab == null) return;

            m_HasSpawned = true;

            // Calculate spawn position and rotation
            Vector3 spawnPosition = contactPoint + (surfaceNormal * spawnOffset);
            Quaternion spawnRotation = alignToSurface
                ? Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, surfaceNormal), surfaceNormal)
                : Quaternion.identity;

            // Instantiate and spawn the network object
            NetworkObject spawnedObject = Instantiate(interactablePrefab, spawnPosition, spawnRotation);

            // Initialize TeleportPadEffect if present
            if (spawnedObject.TryGetComponent<TeleportPadEffect>(out var teleportPad))
            {
                teleportPad.InitializeOwner(m_ShootingContext.ownerClientId);
            }

            spawnedObject.SpawnWithOwnership(m_ShootingContext.ownerClientId);

            // Notify all clients to disable the projectile visually
            DisableProjectileRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void DisableProjectileRpc()
        {
            EnableColliders(false);

            var rb = GetRigidbody();
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
            }

            if (HasAuthority)
            {
                OnEffectComplete?.Invoke(m_Projectile);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════

        private void EnableColliders(bool enable)
        {
            if (colliders == null) return;
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = enable;
            }
        }

        private void IgnoreCollision(GameObject objectA, GameObject objectB, bool shouldIgnore)
        {
            if (objectA == null || objectB == null) return;

            var rootA = objectA.transform.root.gameObject;
            var rootB = objectB.transform.root.gameObject;

            var collidersA = rootA.GetComponentsInChildren<Collider>();
            var collidersB = rootB.GetComponentsInChildren<Collider>();

            foreach (var colliderA in collidersA)
            {
                foreach (var colliderB in collidersB)
                {
                    Physics.IgnoreCollision(colliderA, colliderB, shouldIgnore);
                }
            }
        }
    }
}
