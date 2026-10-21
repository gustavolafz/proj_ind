using System;
using UnityEngine;

namespace NucleoInstavel
{
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField]
        private GameSettings settings;

        [SerializeField]
        private PlayerInputReader input;

        [SerializeField]
        private PlayerMotor player;

        [SerializeField]
        private PlayerHealth health;

        [SerializeField]
        private PlayerFeedback feedback;

        [SerializeField]
        private EnemyController enemy;

        [SerializeField]
        private BulletPool pool;

        [SerializeField]
        private ArenaBounds arena;

        [SerializeField]
        private GameUI ui;

        private bool transitioning;

        public GameState State { get; private set; } = GameState.Menu;
        public bool IsInitialized { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int CurrentScore { get; private set; }
        public int CurrentPhaseIndex { get; private set; }
        public bool HasResult { get; private set; }
        public RunResult LastResult { get; private set; }
        public float DurationSeconds => settings ? settings.DurationSeconds : 60;

        public event Action<GameState> StateChanged;
        public event Action RunStarted;
        public event Action<int> PhaseChanged;
        public event Action<RunResult> RunEnded;

        private void OnEnable()
        {
            if (input)
                input.PauseRequested += TogglePause;

            if (health)
                health.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            if (input)
            {
                input.PauseRequested -= TogglePause;

                input.SetGameplayEnabled(false);
            }

            if (health)
            {
                health.Died -= OnPlayerDied;

                health.SetDamageEnabled(false);
            }

            if (player)
                player.SetMovementEnabled(false);

            Time.timeScale = 1;
        }

        private void Start()
        {
            Time.timeScale = 1;

            if (!ValidateSetup(out var error))
            {
                Debug.LogError($"Núcleo Instável: {error}", this);

                if (ui)
                    ui.ShowConfigurationError(error);

                return;
            }

            pool.Initialize();
            arena.FitCameraToViewport();

            IsInitialized = true;

            SetGameplayEnabled(false);
            enemy.StopAttacks();
            health.ResetHealth();
            ApplyState(GameState.Menu);
        }

        public bool ValidateSetup(out string error)
        {
            error = null;

            if (
                !settings
                || !input
                || !player
                || !health
                || !feedback
                || !enemy
                || !pool
                || !arena
                || !ui
            )
                error =
                    "GameSession: conecte todas as referências no Inspector ou execute Create or Repair Project.";
            else if (!isActiveAndEnabled || !input.isActiveAndEnabled)
                error =
                    "GameSession e PlayerInputReader precisam estar ativos e habilitados. Execute Create or Repair Project fora do Play.";
            else if (
                player.transform.lossyScale != Vector3.one
                || !player.GetComponent<CircleCollider2D>()
                || !Mathf.Approximately(player.GetComponent<CircleCollider2D>().radius, .14f)
            )
                error = "Player: use escala (1,1,1) e hitbox de raio 0.14.";
            else if (
                !settings.TryValidate(out error)
                || !input.ValidateBindings(out error)
                || !arena.TryValidate(out error)
                || !player.ValidateSetup(out error)
                || !health.ValidateSetup(out error)
                || !feedback.ValidateSetup(out error)
                || !enemy.ValidateSetup(out error)
                || !pool.ValidateSetup(out error)
                || !ui.ValidateBindings(out error)
            )
                return false;

            return error == null;
        }

        private void Update()
        {
            if (IsInitialized && State == GameState.Playing)
                TickRun(Time.deltaTime);
        }

        public void TickRun(float deltaTime)
        {
            if (
                !IsInitialized
                || State != GameState.Playing
                || transitioning
                || deltaTime < 0
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime)
            )
                return;

            ElapsedSeconds = Mathf.Min(DurationSeconds, ElapsedSeconds + deltaTime);
            CurrentScore = Mathf.FloorToInt(ElapsedSeconds * settings.PointsPerSecond);

            if (ElapsedSeconds >= DurationSeconds)
            {
                EndRun(health.CurrentHealth > 0);

                return;
            }

            int index = settings.GetPhaseIndex(ElapsedSeconds);

            if (index != CurrentPhaseIndex)
                ApplyPhase(index);

            enemy.Tick(deltaTime);
        }

        public void StartRun()
        {
            if (!IsInitialized || transitioning)
                return;

            transitioning = true;

            SetGameplayEnabled(false);
            enemy.StopAttacks();
            pool.ReleaseAll();

            ElapsedSeconds = 0;
            CurrentScore = 0;
            HasResult = false;
            LastResult = default;

            player.ResetPosition(arena.PlayerSpawnPosition);
            enemy.ResetForRun();
            health.ResetHealth();
            feedback.ResetVisuals();
            ApplyPhase(0);
            ApplyState(GameState.Playing);
            RunStarted?.Invoke();

            transitioning = false;
        }

        private void ApplyPhase(int index)
        {
            CurrentPhaseIndex = index;

            enemy.ConfigurePhase(settings.GetPhase(index));
            PhaseChanged?.Invoke(index);
        }

        private void SetGameplayEnabled(bool value)
        {
            input.SetGameplayEnabled(value);

            if (!value)
                input.ClearInput();

            player.SetMovementEnabled(value);
            health.SetDamageEnabled(value);
        }

        private void ApplyState(GameState state)
        {
            State = state;
            Time.timeScale = state == GameState.Paused ? 0 : 1;

            SetGameplayEnabled(state == GameState.Playing);
            StateChanged?.Invoke(state);
        }

        public void PauseRun()
        {
            if (!transitioning && IsInitialized && State == GameState.Playing)
                ApplyState(GameState.Paused);
        }

        public void ResumeRun()
        {
            if (!transitioning && IsInitialized && State == GameState.Paused)
                ApplyState(GameState.Playing);
        }

        private void TogglePause()
        {
            if (State == GameState.Playing)
                PauseRun();
            else if (State == GameState.Paused)
                ResumeRun();
        }

        public void EndRun(bool won)
        {
            if (!IsInitialized || State != GameState.Playing || transitioning)
                return;

            transitioning = true;
            CurrentScore =
                Mathf.FloorToInt(ElapsedSeconds * settings.PointsPerSecond)
                + (won ? health.CurrentHealth * settings.BonusPerRemainingHealth : 0);
            LastResult = new RunResult(won, ElapsedSeconds, CurrentScore, health.CurrentHealth);
            HasResult = true;

            enemy.StopAttacks();
            SetGameplayEnabled(false);
            pool.ReleaseAll();
            ApplyState(won ? GameState.Won : GameState.Lost);
            RunEnded?.Invoke(LastResult);

            transitioning = false;
        }

        public void ReturnToMenu()
        {
            if (!IsInitialized || transitioning || State == GameState.Menu)
                return;

            transitioning = true;

            enemy.StopAttacks();
            SetGameplayEnabled(false);
            pool.ReleaseAll();
            feedback.ResetVisuals();
            ApplyState(GameState.Menu);

            transitioning = false;
        }

        private void OnPlayerDied() => EndRun(false);

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                PauseRun();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                PauseRun();
        }
    }
}
