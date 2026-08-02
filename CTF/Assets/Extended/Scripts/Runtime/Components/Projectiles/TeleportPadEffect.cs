using UnityEngine;
using Unity.Netcode;
using UnityEngine.VFX;
using Unity.Cinemachine;
using System.Collections;
using Blocks.Gameplay.Core;
using System.Collections.Generic;

namespace Blocks.Gameplay.Extended
{
    public class TeleportPadEffect : NetworkBehaviour, IInteractionEffect
    {
        [Header("Effect Settings")]
        [SerializeField] private int priority = 10;

        [Header("Teleport Start Effects")]
        [SerializeField] private GameObject teleportStartVFX;
        [SerializeField] private SoundDef teleportStartSfx;
        [SerializeField] private float teleportDelay = 0.5f;

        [Header("Teleport End Effects")]
        [SerializeField] private GameObject teleportEndVFX;
        [SerializeField] private SoundDef teleportEndSfx;

        [Header("VFX & Audio Settings")]
        [SerializeField] private float vfxDuration = 2.0f;
        [SerializeField] private float sfxVolume = 1.0f;

        [Header("Movement Control")]
        [SerializeField] private bool disableMovementDuringTeleport = true;

        [Header("Pad Pairing")]
        [SerializeField] private int maxPadsPerPlayer = 2;
        [SerializeField] private float teleportHeightOffset = 1.0f;

        [Header("Access Control")]
        [SerializeField] private bool allowAllPlayers;

        [Header("Cooldown")]
        [SerializeField] private float teleportCooldown = 1.5f;

        // Network variables for pad state
        private readonly NetworkVariable<ulong> m_PadOwnerClientId =
            new NetworkVariable<ulong>(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<NetworkObjectReference> m_PartnerPad =
            new NetworkVariable<NetworkObjectReference>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Pending owner ID for post-spawn initialization
        private ulong m_PendingOwnerClientId = ulong.MaxValue;

        // Static tracking of pads per player (authority-side)
        private static readonly Dictionary<ulong, List<TeleportPadEffect>> k_PlayerPads =
            new Dictionary<ulong, List<TeleportPadEffect>>();

        // Cooldown tracking per player
        private readonly Dictionary<ulong, float> m_LastTeleportTimes = new Dictionary<ulong, float>();

        public int Priority => priority;

        public TeleportPadEffect PartnerPad
        {
            get
            {
                if (m_PartnerPad.Value.TryGet(out NetworkObject netObj))
                {
                    return netObj.GetComponent<TeleportPadEffect>();
                }
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Network Lifecycle
        // ═══════════════════════════════════════════════════════════════

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (HasAuthority && m_PendingOwnerClientId != ulong.MaxValue)
            {
                m_PadOwnerClientId.Value = m_PendingOwnerClientId;
                RegisterPad();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (HasAuthority)
            {
                UnregisterPad();
            }

            base.OnNetworkDespawn();
        }

        public void InitializeOwner(ulong ownerClientId)
        {
            m_PendingOwnerClientId = ownerClientId;
        }

        // ═══════════════════════════════════════════════════════════════
        // IInteractionEffect Implementation
        // ═══════════════════════════════════════════════════════════════

        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (!interactor.TryGetComponent<NetworkObject>(out var interactorNetObj))
            {
                yield break;
            }

            // Check if player is allowed to use this pad
            if (!allowAllPlayers && interactorNetObj.OwnerClientId != m_PadOwnerClientId.Value)
            {
                Debug.Log($"[TeleportPadEffect] Player {interactorNetObj.OwnerClientId} " +
                          $"cannot use pad owned by {m_PadOwnerClientId.Value}");
                yield break;
            }

            // Check cooldown to prevent teleport loops
            ulong playerId = interactorNetObj.OwnerClientId;
            if (m_LastTeleportTimes.TryGetValue(playerId, out float lastTime))
            {
                if (Time.time - lastTime < teleportCooldown)
                {
                    yield break;
                }
            }

            TeleportPadEffect partner = PartnerPad;
            if (partner == null)
            {
                Debug.Log("[TeleportPadEffect] No partner pad available for teleportation.");
                yield break;
            }

            Vector3 startPosition = interactor.transform.position;
            Vector3 endPosition = partner.transform.position + Vector3.up * teleportHeightOffset;

            CorePlayerManager playerManager = null;
            if (disableMovementDuringTeleport)
            {
                playerManager = interactor.GetComponent<CorePlayerManager>();
                if (playerManager != null)
                {
                    playerManager.SetMovementInputEnabled(false);
                }
            }

            PlayTeleportStartEffectsRpc(startPosition);
            CoreDirector.RequestCameraShake()
                .WithImpulseDefinition(CinemachineImpulseDefinition.ImpulseShapes.Rumble,
                    CinemachineImpulseDefinition.ImpulseTypes.Dissipating,
                    0.15f)
                .WithVelocity(0.15f)
                .AtPosition(transform.position)
                .Execute();

            if (teleportDelay > 0f)
            {
                yield return new WaitForSeconds(teleportDelay);
            }

            PerformTeleport(interactor, endPosition);

            // Set cooldown on both pads to prevent teleport loops
            m_LastTeleportTimes[playerId] = Time.time;
            partner.m_LastTeleportTimes[playerId] = Time.time;

            PlayTeleportEndEffectsRpc(endPosition);
            CoreDirector.RequestCameraShake()
                .WithImpulseDefinition(CinemachineImpulseDefinition.ImpulseShapes.Rumble,
                    CinemachineImpulseDefinition.ImpulseTypes.Propagating,
                    0.15f)
                .WithVelocity(0.15f)
                .AtPosition(endPosition)
                .Execute();

            if (disableMovementDuringTeleport && playerManager != null)
            {
                playerManager.SetMovementInputEnabled(true);
            }

            yield return null;
        }

        public void CancelEffect(GameObject interactor)
        {
            if (disableMovementDuringTeleport)
            {
                var playerManager = interactor.GetComponent<CorePlayerManager>();
                if (playerManager != null)
                {
                    playerManager.SetMovementInputEnabled(true);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Pad Registration
        // ═══════════════════════════════════════════════════════════════

        private void RegisterPad()
        {
            ulong ownerId = m_PadOwnerClientId.Value;

            if (!k_PlayerPads.TryGetValue(ownerId, out var padList))
            {
                padList = new List<TeleportPadEffect>();
                k_PlayerPads[ownerId] = padList;
            }

            // If player already has max pads, despawn the oldest
            while (padList.Count >= maxPadsPerPlayer)
            {
                var oldestPad = padList[0];
                padList.RemoveAt(0);
                if (oldestPad != null && oldestPad.IsSpawned)
                {
                    oldestPad.NetworkObject.Despawn();
                }
            }

            padList.Add(this);
            UpdatePartnerReferences(padList);
        }

        private void UnregisterPad()
        {
            ulong ownerId = m_PadOwnerClientId.Value;

            if (k_PlayerPads.TryGetValue(ownerId, out var padList))
            {
                padList.Remove(this);

                if (padList.Count > 0)
                {
                    UpdatePartnerReferences(padList);
                }
                else
                {
                    k_PlayerPads.Remove(ownerId);
                }
            }
        }

        private void UpdatePartnerReferences(List<TeleportPadEffect> padList)
        {
            // Clear all partner references first
            foreach (var pad in padList)
            {
                if (pad != null && pad.IsSpawned)
                {
                    pad.m_PartnerPad.Value = default;
                }
            }

            // If we have exactly 2 pads, link them together
            if (padList.Count == 2)
            {
                var pad1 = padList[0];
                var pad2 = padList[1];

                if (pad1 != null && pad1.IsSpawned && pad2 != null && pad2.IsSpawned)
                {
                    pad1.m_PartnerPad.Value = new NetworkObjectReference(pad2.NetworkObject);
                    pad2.m_PartnerPad.Value = new NetworkObjectReference(pad1.NetworkObject);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Teleportation
        // ═══════════════════════════════════════════════════════════════

        private void PerformTeleport(GameObject interactor, Vector3 targetPosition)
        {
            if (interactor.TryGetComponent<CoreMovement>(out var coreMovement))
            {
                coreMovement.SetPosition(targetPosition);
            }
            else
            {
                Debug.LogWarning($"[TeleportPadEffect] Interactor '{interactor.name}' does not have " +
                                 "CoreMovement. Using direct transform.");
                interactor.transform.position = targetPosition;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Visual & Audio Effects (synced via RPC)
        // ═══════════════════════════════════════════════════════════════

        [Rpc(SendTo.Everyone)]
        private void PlayTeleportStartEffectsRpc(Vector3 position)
        {
            if (teleportStartVFX != null)
            {
                GameObject vfxInstance = Instantiate(teleportStartVFX, position, Quaternion.identity);

                if (vfxInstance.TryGetComponent<VisualEffect>(out var vfx))
                {
                    vfx.Play();
                }
                else if (vfxInstance.TryGetComponent<ParticleSystem>(out var ps))
                {
                    ps.Play();
                }

                Destroy(vfxInstance, vfxDuration);
            }

            if (teleportStartSfx != null)
            {
                CoreDirector.RequestAudio(teleportStartSfx)
                    .WithPosition(position)
                    .Play(sfxVolume);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void PlayTeleportEndEffectsRpc(Vector3 position)
        {
            if (teleportEndVFX != null)
            {
                GameObject vfxInstance = Instantiate(teleportEndVFX, position, Quaternion.identity);

                if (vfxInstance.TryGetComponent<VisualEffect>(out var vfx))
                {
                    vfx.Play();
                }
                else if (vfxInstance.TryGetComponent<ParticleSystem>(out var ps))
                {
                    ps.Play();
                }

                Destroy(vfxInstance, vfxDuration);
            }

            if (teleportEndSfx != null)
            {
                CoreDirector.RequestAudio(teleportEndSfx)
                    .WithPosition(position)
                    .Play(sfxVolume);
            }
        }
    }
}
