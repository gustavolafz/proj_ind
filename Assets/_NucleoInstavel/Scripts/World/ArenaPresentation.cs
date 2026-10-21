using UnityEngine;
using UnityEngine.UI;

namespace NucleoInstavel
{
    // Decorative layers only. Gameplay continues to own movement, collisions and time.
    public sealed class ArenaPresentation : MonoBehaviour
    {
        [SerializeField]
        private GameSession session;

        [SerializeField]
        private SpriteRenderer outerHalo,
            innerHalo,
            energyGlow;

        [SerializeField]
        private SpriteRenderer playerBody,
            playerHitbox,
            reactorBody;

        [SerializeField]
        private RectTransform menuHalo;

        [SerializeField]
        private Image timeProgress;

        private float animationTime;
        private Vector3 glowScale;

        private void Awake()
        {
            if (energyGlow)
                glowScale = energyGlow.transform.localScale;
        }

        public bool ValidateSetup(out string error)
        {
            error =
                session
                && outerHalo
                && innerHalo
                && energyGlow
                && playerBody
                && playerHitbox
                && reactorBody
                && menuHalo
                && timeProgress
                && !timeProgress.raycastTarget
                    ? null
                    : "Apresentação: conecte sessão, aros, brilho e barra de tempo sem raycast.";

            return error == null;
        }

        private void LateUpdate()
        {
            if (!session || !outerHalo || !innerHalo || !energyGlow || !timeProgress)
                return;

            bool showArena =
                session.State == GameState.Playing || session.State == GameState.Paused;

            if (playerBody)
                playerBody.enabled = showArena;

            if (playerHitbox)
                playerHitbox.enabled = showArena;

            if (reactorBody)
                reactorBody.enabled = showArena;

            outerHalo.enabled = innerHalo.enabled = energyGlow.enabled = showArena;

            float dt = session.State == GameState.Menu ? Time.unscaledDeltaTime : Time.deltaTime;

            animationTime += dt;

            int phase = session.State == GameState.Menu ? 0 : session.CurrentPhaseIndex;
            var accent =
                phase >= 2 ? new Color(1, .36f, .24f)
                : phase == 1 ? new Color(.85f, .32f, 1)
                : new Color(.2f, .85f, 1);

            outerHalo.transform.Rotate(0, 0, dt * (8 + phase * 4));
            innerHalo.transform.Rotate(0, 0, -dt * (13 + phase * 5));

            if (menuHalo)
                menuHalo.localRotation = Quaternion.Euler(0, 0, animationTime * 8);

            outerHalo.color = new Color(accent.r, accent.g, accent.b, .32f);
            innerHalo.color = new Color(accent.r, accent.g, accent.b, .22f);
            energyGlow.color = accent;
            energyGlow.transform.localScale = glowScale * (1 + Mathf.Sin(animationTime * 2) * .07f);
            timeProgress.color = accent;
            timeProgress.fillAmount =
                session.DurationSeconds > 0
                    ? Mathf.Clamp01(session.ElapsedSeconds / session.DurationSeconds)
                    : 0;
        }

        private void OnDisable()
        {
            if (playerBody)
                playerBody.enabled = true;

            if (playerHitbox)
                playerHitbox.enabled = true;

            if (reactorBody)
                reactorBody.enabled = true;
        }
    }
}
