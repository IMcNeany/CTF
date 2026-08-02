using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Extended
{
    public class JetPackAddon : NetworkBehaviour, IPlayerAddon
    {
        [Header("References")]
        [Tooltip("The actual locomotion ability to toggle.")]
        [SerializeField] private JetPackLocomotionAbility jetpackAbility;
        [Tooltip("Visuals object (e.g. wings/flames) to enable while flying.")]
        [SerializeField] private GameObject jetpackVisuals;

        [Header("Input Events")]
        [Tooltip("Event raised when the jetpack button is pressed.")]
        [SerializeField] private GameEvent onJetpackPressed;
        [Tooltip("Event raised when the jetpack button is released.")]
        [SerializeField] private GameEvent onJetpackReleased;

        [Header("Effects")]
        [Tooltip("Effect spawned when jetpack starts (e.g. burst).")]
        [SerializeField] private GameObject startEffectPrefab;
        [Tooltip("Ribbon/Trail effect spawned when jetpack starts.")]
        [SerializeField] private GameObject ribbonEffectPrefab;
        [Tooltip("Effect spawned when jetpack stops.")]
        [SerializeField] private GameObject stopEffectPrefab;

        [Header("Spawn Points")]
        [SerializeField] private Transform thrusterLeft;
        [SerializeField] private Transform thrusterRight;

        [Header("Camera Shake")]
        [Tooltip("Camera shake intensity on start.")]
        [SerializeField] private float shakeIntensity = 1f;
        [Tooltip("Camera shake duration on start.")]
        [SerializeField] private float shakeDuration = 0.2f;

        private CoreStatsHandler m_StatsHandler;
        private CorePlayerManager m_PlayerManager;
        private bool m_IsJetPacking;

        public void Initialize(CorePlayerManager playerManager)
        {
            m_StatsHandler = playerManager.CoreStats;
            m_PlayerManager = playerManager;
            if (jetpackAbility == null)
            {
                jetpackAbility = GetComponent<JetPackLocomotionAbility>();
            }
        }

        public void OnPlayerSpawn()
        {
            if (m_PlayerManager != null && m_PlayerManager.IsOwner)
            {
                RegisterEventListeners();
            }
        }

        public void OnPlayerDespawn()
        {
            if (m_PlayerManager != null && m_PlayerManager.IsOwner)
            {
                UnregisterEventListeners();
            }
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            if (newState == PlayerLifeState.InitialSpawn || newState == PlayerLifeState.Respawned)
            {
                ResetJetpackState();
            }
        }

        private void RegisterEventListeners()
        {
            if (onJetpackPressed != null) onJetpackPressed.RegisterListener(HandlePressed);
            if (onJetpackReleased != null) onJetpackReleased.RegisterListener(HandleReleased);
        }

        private void UnregisterEventListeners()
        {
            if (onJetpackPressed != null) onJetpackPressed.UnregisterListener(HandlePressed);
            if (onJetpackReleased != null) onJetpackReleased.UnregisterListener(HandleReleased);
        }

        private void ResetJetpackState()
        {
            m_IsJetPacking = false;
            if (jetpackAbility != null)
            {
                jetpackAbility.IsJetpacking = false;
            }
        }

        private void HandlePressed()
        {
            if (!m_PlayerManager.IsOwner) return;
            if (jetpackAbility != null)
            {
                jetpackAbility.IsJetpacking = true;
                CoreDirector.RequestCameraShake()
                    .WithVelocity(shakeIntensity)
                    .WithDuration(shakeDuration)
                    .AtPosition(transform.position)
                    .Execute();
            }

            SetJetpackStateRpc(true);
        }

        private void HandleReleased()
        {
            if (!m_PlayerManager.IsOwner) return;
            if (jetpackAbility != null)
            {
                jetpackAbility.IsJetpacking = false;
            }

            SetJetpackStateRpc(false);
        }

        private void Update()
        {
            if (m_PlayerManager == null || !m_PlayerManager.IsOwner || !m_IsJetPacking || jetpackAbility == null || m_StatsHandler == null) return;

            float fuelNeeded = jetpackAbility.FuelCost * Time.deltaTime;

            if (!m_StatsHandler.TryConsumeStat(StatKeys.JetPackFuel, fuelNeeded, OwnerClientId))
            {
                HandleReleased();
            }
        }

        [Rpc(SendTo.Everyone)]
        private void SetJetpackStateRpc(bool active)
        {
            m_IsJetPacking = active;
            if (active)
            {
                SpawnEffectAtThrusters(startEffectPrefab);
                SpawnEffectAtThrusters(ribbonEffectPrefab, true);
                if (jetpackVisuals != null) jetpackVisuals.SetActive(true);
            }
            else
            {
                SpawnEffectAtThrusters(stopEffectPrefab);
            }
        }

        private void SpawnEffectAtThrusters(GameObject prefab, bool attachToThrusters = false)
        {
            if (prefab == null) return;

            Transform[] thrusters = { thrusterLeft, thrusterRight };

            foreach (var thruster in thrusters)
            {
                if (thruster != null)
                {
                    var builder = CoreDirector.CreatePrefabEffect(prefab)
                        .WithPosition(thruster.position)
                        .WithRotation(thruster.rotation);

                    if (attachToThrusters)
                    {
                        builder.WithParent(thruster)
                            .WithScale(thruster.localScale * 0.01f);
                    }

                    builder.Create();
                }
            }
        }
    }
}
