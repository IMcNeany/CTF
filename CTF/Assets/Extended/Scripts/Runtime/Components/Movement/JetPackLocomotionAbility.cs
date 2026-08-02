using UnityEngine;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Extended
{
    public class JetPackLocomotionAbility : MonoBehaviour, IMovementAbility
    {
        [Header("Fuel")]
        [SerializeField] private float fuelCost = 10f;

        [Header("Movement Settings")]
        [SerializeField] private float acceleration = 15f;
        [SerializeField] private float deceleration = 25f;
        [SerializeField] private float moveSpeed = 6f;

        [Header("Jetpack Settings")]
        [SerializeField] private float flyAcceleration = 20f;
        [SerializeField] private float maxFlySpeed = 10f;

        private CoreMovement m_Motor;
        private float m_CurrentSpeed;
        private Vector3 m_LastMoveDirection;

        public int Priority => 10;
        public float StaminaCost => 0f;
        public float FuelCost => fuelCost;
        public bool IsJetpacking { get; set; }

        public void Initialize(CoreMovement movementController)
        {
            m_Motor = movementController;
        }

        public MovementModifier Process()
        {
            var modifier = new MovementModifier();
            HandleHorizontalMovement(ref modifier);
            HandleVerticalMovement(ref modifier);
            return modifier;
        }

        public bool TryActivate() => false;

        private void HandleHorizontalMovement(ref MovementModifier modifier)
        {
            float targetSpeed = m_Motor.InputMagnitude * moveSpeed;
            bool hasInput = m_Motor.InputMagnitude > 0.01f;
            float rate = hasInput ? acceleration : deceleration;

            m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, targetSpeed, rate * Time.deltaTime);

            if (hasInput)
            {
                Vector3 inputDirection = new Vector3(m_Motor.MoveInput.x, 0.0f, m_Motor.MoveInput.y).normalized;
                m_LastMoveDirection = TransformInputDirection(inputDirection);
            }

            modifier.ArealVelocity = m_LastMoveDirection * m_CurrentSpeed;
        }

        private void HandleVerticalMovement(ref MovementModifier modifier)
        {
            if (IsJetpacking)
            {
                modifier.OverrideGravity = true;
                float currentVertical = m_Motor.VerticalVelocity;
                float newVertical = Mathf.MoveTowards(currentVertical, maxFlySpeed, flyAcceleration * Time.deltaTime);
                m_Motor.SetVerticalVelocity(newVertical);
            }
        }

        private Vector3 TransformInputDirection(Vector3 inputDirection)
        {
            if (m_Motor.directionMode == CoreMovement.MovementDirectionMode.CameraRelative)
            {
                return Quaternion.Euler(0.0f, m_Motor.TargetRotationY, 0.0f) * inputDirection;
            }

            return m_Motor.transform.rotation * inputDirection;
        }
    }
}
