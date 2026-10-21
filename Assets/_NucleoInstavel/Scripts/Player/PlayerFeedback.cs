using UnityEngine;

namespace NucleoInstavel
{
    public sealed class PlayerFeedback : MonoBehaviour
    {
        [SerializeField]
        private PlayerHealth health;

        [SerializeField]
        private PlayerInputReader input;

        [SerializeField]
        private GameSession session;

        [SerializeField]
        private SpriteRenderer bodyVisual,
            hitboxIndicator;

        private float flashUntil;

        public bool ValidateSetup(out string error)
        {
            error =
                health
                && input
                && session
                && bodyVisual
                && hitboxIndicator
                && bodyVisual.sprite
                && hitboxIndicator.sprite
                    ? null
                    : "PlayerFeedback: conecte referências e sprites do corpo/hitbox.";

            return error == null;
        }

        private void OnEnable()
        {
            if (health)
                health.Damaged += OnDamaged;

            if (session)
                session.RunStarted += ResetVisuals;
        }

        private void OnDisable()
        {
            if (health)
                health.Damaged -= OnDamaged;

            if (session)
                session.RunStarted -= ResetVisuals;
        }

        private void OnDamaged() => flashUntil = Time.time + .12f;

        private void Update()
        {
            if (!bodyVisual || !health || !input)
                return;

            Color color = Time.time < flashUntil ? Color.white : new Color(.35f, 1f, 1f);

            if (health.IsInvulnerable)
                color.a = Mathf.Repeat(Time.time * 9, 1) < .5f ? .25f : 1;

            bodyVisual.color = color;

            SetFocusIndicator(input.FocusHeld);
        }

        public void SetFocusIndicator(bool value)
        {
            if (hitboxIndicator)
                hitboxIndicator.color = value ? Color.white : new Color(.45f, 1, 1);
        }

        public void ResetVisuals()
        {
            flashUntil = 0;

            if (bodyVisual)
                bodyVisual.color = new Color(.35f, 1, 1);

            SetFocusIndicator(false);
        }
    }
}
