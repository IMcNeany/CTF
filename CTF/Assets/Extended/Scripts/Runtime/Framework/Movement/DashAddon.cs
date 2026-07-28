using Blocks.Gameplay.Core;
using System;
using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Extended
{
    public class DashAddon : NetworkBehaviour, IPlayerAddon
    {
        [Header("References")]
        [SerializeField] private DashAbility dashAbility;
        [SerializeField] private GameEvent OnDashActionPressed;

        [Header("Feedback")]
        [SerializeField] private SoundDef insufficientStaminaSound;
        [SerializeField] private float warningCooldown = 2.0f;

        private CorePlayerManager m_PlayerManager;
        private CoreStatsHandler m_StatsHandler;
        private float m_LastWarningTime;
        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;
            m_StatsHandler = playerManager.CoreStats;
            if(dashAbility == null)
            {
                dashAbility = GetComponent<DashAbility>();
            }
        }
        public void OnPlayerSpawn()
        {
            //return if we are not the owner player
            if (!m_PlayerManager.IsOwner)
            {
                return;
            }

            if (OnDashActionPressed != null)
            {
                OnDashActionPressed.RegisterListener(HandleDashInput);
            }
        }

        private void HandleDashInput()
        {
            if(dashAbility == null)
            {
                Debug.LogWarning("[DashAddon] DashAbility component not found.", this);
                return;
            }

            float staminaCost = dashAbility.StaminaCost;

            float currentStamina = m_StatsHandler.GetCurrentValue(StatKeys.Stamina);

            if(currentStamina < staminaCost)
            {
                HandleInsufficientStamina();
                return;
            }

            //attempt dash
            if(dashAbility.TryActivate())
            {
                //consume stamina
                m_StatsHandler.ModifyStat(StatKeys.Stamina,-staminaCost,m_PlayerManager.OwnerClientId, ModificationSource.Consumption);
            }

        }

        private void HandleInsufficientStamina()
        {
            // Prevent spamming the warning sound
            if (Time.time - m_LastWarningTime < warningCooldown)
            {
                return;
            }

            m_LastWarningTime = Time.time;
            if (insufficientStaminaSound != null)
            {
                CoreDirector.RequestAudio(insufficientStaminaSound).AttachedTo(transform).Play(0.5f);
            }
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
        }

        public void OnPlayerDespawn()
        {
            if (!m_PlayerManager.IsOwner) return;
            if (OnDashActionPressed != null)
            {
                OnDashActionPressed.UnregisterListener(HandleDashInput);
            }
        }


    }
}
