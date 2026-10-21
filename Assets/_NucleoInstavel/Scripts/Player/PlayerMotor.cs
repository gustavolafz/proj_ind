using UnityEngine;

namespace NucleoInstavel
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField]
        private PlayerInputReader input;

        [SerializeField]
        private GameSettings settings;

        [SerializeField]
        private ArenaBounds arena;

        private Rigidbody2D body;
        private CircleCollider2D hitbox;
        private bool movementEnabled;

        public Vector2 Position => body ? body.position : (Vector2)transform.position;

        public bool ValidateSetup(out string error)
        {
            var rb = GetComponent<Rigidbody2D>();
            var collider = GetComponent<CircleCollider2D>();

            error =
                input
                && settings
                && arena
                && rb
                && collider
                && !collider.isTrigger
                && rb.bodyType == RigidbodyType2D.Kinematic
                && rb.gravityScale == 0
                && collider.offset == Vector2.zero
                && transform.lossyScale == Vector3.one
                    ? null
                    : "PlayerMotor: confira referências, corpo Kinematic, hitbox e escala.";

            return error == null;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CircleCollider2D>();
        }

        private void FixedUpdate()
        {
            if (movementEnabled)
                body.MovePosition(CalculateNextPosition(Time.fixedDeltaTime));
        }

        public void SetMovementEnabled(bool value)
        {
            movementEnabled = value;

            if (!value && body)
                body.linearVelocity = Vector2.zero;
        }

        public void ResetPosition(Vector2 position)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0;
        }

        public float GetCurrentSpeed() =>
            input.FocusHeld ? settings.FocusMoveSpeed : settings.MoveSpeed;

        public Vector2 CalculateNextPosition(float deltaTime) =>
            arena.ClampPlayerPosition(
                Position
                    + Vector2.ClampMagnitude(input.MoveInput, 1f) * GetCurrentSpeed() * deltaTime,
                hitbox.radius
            );
    }
}
