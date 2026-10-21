using System.Collections;
using UnityEngine;

namespace NucleoInstavel
{
    public sealed class AudioController : MonoBehaviour
    {
        [SerializeField]
        private GameSession session;

        [SerializeField]
        private PlayerHealth health;

        [SerializeField]
        private BulletEmitter primaryEmitter,
            secondaryEmitter;

        [SerializeField]
        private AudioSource musicSource,
            sfxSource;

        [SerializeField]
        private AudioClip music,
            uiClick,
            volley,
            hit,
            phaseChange,
            victory,
            defeat;

        [SerializeField]
        private AudioClip menuMusic,
            instabilityMusic,
            overloadMusic,
            resultMusic;

        private float nextVolleySound;
        private float musicVolume;
        private Coroutine musicTransition;

        private void Awake()
        {
            musicVolume = musicSource ? musicSource.volume : .15f;
        }

        public bool ValidateSetup(out string error)
        {
            error =
                session
                && health
                && primaryEmitter
                && secondaryEmitter
                && musicSource
                && sfxSource
                && musicSource != sfxSource
                && !musicSource.playOnAwake
                && !sfxSource.playOnAwake
                && musicSource.spatialBlend == 0
                && sfxSource.spatialBlend == 0
                && music
                && uiClick
                && volley
                && hit
                && phaseChange
                && victory
                && defeat
                    ? null
                    : "AudioController: conecte duas fontes 2D, eventos, música e os seis efeitos.";

            return error == null;
        }

        private void OnEnable()
        {
            if (session)
            {
                session.StateChanged += OnStateChanged;
                session.RunStarted += OnRunStarted;
                session.RunEnded += OnRunEnded;
                session.PhaseChanged += OnPhaseChanged;
            }

            if (health)
                health.Damaged += OnPlayerDamaged;

            if (primaryEmitter)
                primaryEmitter.VolleyEmitted += OnVolleyEmitted;

            if (secondaryEmitter)
                secondaryEmitter.VolleyEmitted += OnVolleyEmitted;
        }

        private void OnDisable()
        {
            if (session)
            {
                session.StateChanged -= OnStateChanged;
                session.RunStarted -= OnRunStarted;
                session.RunEnded -= OnRunEnded;
                session.PhaseChanged -= OnPhaseChanged;
            }

            if (health)
                health.Damaged -= OnPlayerDamaged;

            if (primaryEmitter)
                primaryEmitter.VolleyEmitted -= OnVolleyEmitted;

            if (secondaryEmitter)
                secondaryEmitter.VolleyEmitted -= OnVolleyEmitted;

            StopTransition();

            if (musicSource)
            {
                musicSource.Stop();

                musicSource.volume = musicVolume;
            }
        }

        public AudioClip GetMusicForPhase(int index) =>
            index >= 2 && overloadMusic ? overloadMusic
            : index == 1 && instabilityMusic ? instabilityMusic
            : music;

        public void PlayMusic() =>
            SwitchMusic(GetMusicForPhase(session ? session.CurrentPhaseIndex : 0), true);

        private void StopTransition()
        {
            if (musicTransition != null)
            {
                StopCoroutine(musicTransition);

                musicTransition = null;
            }
        }

        private void SwitchMusic(AudioClip clip, bool restart = false)
        {
            if (!musicSource || !clip)
                return;

            if (!restart && musicSource.clip == clip && musicSource.isPlaying)
                return;

            StopTransition();

            musicTransition = StartCoroutine(TransitionMusic(clip, restart));
        }

        private IEnumerator TransitionMusic(AudioClip clip, bool restart)
        {
            if (!restart && musicSource.isPlaying)
            {
                float from = musicSource.volume;

                for (float t = 0; t < .3f; )
                {
                    if (!session || session.State != GameState.Paused)
                    {
                        t += Time.unscaledDeltaTime;
                        musicSource.volume = Mathf.Lerp(from, 0, t / .3f);
                    }

                    yield return null;
                }
            }

            musicSource.Stop();

            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.volume = 0;

            musicSource.Play();

            for (float t = 0; t < .45f; )
            {
                if (!session || session.State != GameState.Paused)
                {
                    t += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(0, musicVolume, t / .45f);
                }

                yield return null;
            }

            musicSource.volume = musicVolume;
            musicTransition = null;
        }

        public void SetMusicPaused(bool value)
        {
            if (!musicSource)
                return;

            if (value)
                musicSource.Pause();
            else
                musicSource.UnPause();
        }

        public void PlaySfx(SfxId id)
        {
            var clip = GetClip(id);

            if (sfxSource && clip)
                sfxSource.PlayOneShot(clip);
        }

        public AudioClip GetClip(SfxId id)
        {
            switch (id)
            {
                case SfxId.UiClick:
                    return uiClick;
                case SfxId.Volley:
                    return volley;
                case SfxId.Hit:
                    return hit;
                case SfxId.PhaseChange:
                    return phaseChange;
                case SfxId.Victory:
                    return victory;
                default:
                    return defeat;
            }
        }

        private bool CanPlayVolleySound() => Time.unscaledTime >= nextVolleySound;

        private void OnVolleyEmitted()
        {
            if (!CanPlayVolleySound())
                return;

            nextVolleySound = Time.unscaledTime + .08f;

            PlaySfx(SfxId.Volley);
        }

        private void OnRunStarted()
        {
            sfxSource.Stop();

            nextVolleySound = 0;

            PlayMusic();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Paused)
                SetMusicPaused(true);
            else if (state == GameState.Playing)
                SetMusicPaused(false);
            else if (state == GameState.Menu)
                SwitchMusic(menuMusic ? menuMusic : music);
            else if (state == GameState.Won || state == GameState.Lost)
                SwitchMusic(resultMusic ? resultMusic : music);
        }

        private void OnRunEnded(RunResult result) =>
            PlaySfx(result.Won ? SfxId.Victory : SfxId.Defeat);

        private void OnPlayerDamaged()
        {
            if (health.CurrentHealth > 0)
                PlaySfx(SfxId.Hit);
        }

        private void OnPhaseChanged(int index)
        {
            if (index > 0)
                PlaySfx(SfxId.PhaseChange);

            if (session && session.State == GameState.Playing)
                SwitchMusic(GetMusicForPhase(index));
        }
    }
}
