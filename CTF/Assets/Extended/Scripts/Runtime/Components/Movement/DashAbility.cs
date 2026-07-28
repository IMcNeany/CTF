using UnityEngine;
using Blocks.Gameplay.Core;
using System;

namespace Blocks.Gameplay.Extended
{ 
    public class DashAbility : MonoBehaviour, IMovementAbility
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashForce = 50f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float dashCooldown = 1.0f;
        [SerializeField] private float staminaCost = 15f;
        [SerializeField] private bool requireGrounded = false;
        [SerializeField] private bool allowAirDash = true;
        [SerializeField] private int maxAirDashes = 1;

        [Header("Effects")]
        [SerializeField] private GameObject dashStartEffect;
        [SerializeField] private SoundDef dashStartSound;


        //should have a higher priority than walking/running
        public int Priority => 20;
        public float StaminaCost => staminaCost;

        private bool m_IsDashing;
        private float m_DashTimer;
        private CoreMovement m_Motor;
        private float m_CooldownTimer;
        private Vector3 m_DashDirection;
        private int m_RemainingAirDashes;

        //called when the ability is added to the character
        public void Initialize(CoreMovement movementController)
        {
            m_Motor = movementController;
            m_Motor.OnGroundedStateChanged += OnGroundedStateChanged;
            m_RemainingAirDashes = maxAirDashes;
        }

        //called every frame to apply movement forces
        public MovementModifier Process()
        {
            var modifier = new MovementModifier();

            // Handle Cooldown
            if (m_CooldownTimer > 0)
            {
                m_CooldownTimer -= Time.deltaTime;
            }

            // Handle Active Dash
            if (m_IsDashing)
            {
                //just set a timer instead?
                m_DashTimer -= Time.deltaTime;
                if(m_DashTimer < 0)
                {
                    EndDash();
                }
                else
                {
                    // Apply velocity in the dash direction
                    modifier.ArealVelocity = m_DashDirection * dashForce;

                    // Disable gravity during dash for consistent distance
                    modifier.OverrideGravity = true;
                }
            }
            return modifier;
        }

        //custom method to trigger the ability
        public bool TryActivate()
        {
            // Validation Checks
            if (m_CooldownTimer > 0 || m_IsDashing) return false;
            if (requireGrounded && !m_Motor.IsGrounded) return false;
            if (!m_Motor.IsGrounded && !allowAirDash) return false;
            if (!m_Motor.IsGrounded && m_RemainingAirDashes <= 0) return false;

            Vector3 dashDir = CalculateDashDirection();

            // Fallback if no input: dash forward
            if (dashDir.magnitude < 0.1f)
            {
                dashDir = m_Motor.RotationTransform != null
                    ? m_Motor.RotationTransform.forward
                    : m_Motor.transform.forward;
            }

            // Start Dash
            StartDash(dashDir.normalized);
            return true;
        }

        private void StartDash(Vector3 direction)
        {
            m_IsDashing = true;
            m_DashTimer = dashDuration;
            m_CooldownTimer = dashCooldown;
            m_DashDirection = new Vector3(direction.x, 0f, direction.z).normalized;

            if (!m_Motor.IsGrounded)
            {
                m_RemainingAirDashes--;
            }

            // Reset vertical velocity for a snappy dash feel
            m_Motor.SetVerticalVelocity(0f);

            // Play Effects
            if (dashStartEffect != null)
            {
                CoreDirector.CreatePrefabEffect(dashStartEffect)
                    .WithPosition(m_Motor.transform.position)
                    .WithRotation(Quaternion.LookRotation(m_DashDirection))
                    .WithName("DashStart")
                    .WithDuration(dashDuration + 0.5f)
                    .Create();
            }

            if (dashStartSound != null)
            {
                CoreDirector.RequestAudio(dashStartSound)
                    .AttachedTo(m_Motor.transform)
                    .Play();
            }
        }

        private void OnGroundedStateChanged(bool isGrounded)
        {
            if (isGrounded)
            {
                // Reset air dashes
                m_RemainingAirDashes = maxAirDashes;
            }
        }

        private Vector3 CalculateDashDirection()
        {
            // If there is movement input, dash in that direction relative to camera/character
            if (m_Motor.MoveInput.magnitude > 0.1f)
            {
                Vector3 inputDirection = new Vector3(m_Motor.MoveInput.x, 0.0f, m_Motor.MoveInput.y);
                switch (m_Motor.directionMode)
                {
                    case CoreMovement.MovementDirectionMode.CharacterRelative:
                        return m_Motor.transform.rotation * inputDirection;
                    case CoreMovement.MovementDirectionMode.CameraRelative:
                        return Quaternion.Euler(0.0f, m_Motor.TargetRotationY, 0.0f) * inputDirection;
                    default:
                        return inputDirection;
                }
            }

            // Otherwise default to forward
            Transform rotationTransform = m_Motor.RotationTransform != null
                ? m_Motor.RotationTransform
                : m_Motor.transform;

            return rotationTransform.forward;
        }

        private void EndDash()
        {
            m_IsDashing = false;
            m_DashTimer = 0f;
        }

        private void OnDestroy()
        {
            if (m_Motor != null)
            {
                m_Motor.OnGroundedStateChanged -= OnGroundedStateChanged;
            }
        }
    }
}
