using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NucleoInstavel
{
    public sealed class GameUI : MonoBehaviour
    {
        [SerializeField]
        private GameSession session;

        [SerializeField]
        private PlayerHealth health;

        [SerializeField]
        private AudioController audioController;

        [SerializeField]
        private EventSystem eventSystem;

        [SerializeField]
        private InputActionAsset controls;

        [SerializeField]
        private GameObject menuPanel,
            helpPanel,
            creditsPanel,
            hudPanel,
            pausePanel,
            resultPanel,
            errorPanel;

        [SerializeField]
        private Button playButton,
            helpButton,
            creditsButton,
            helpBackButton,
            creditsBackButton,
            resumeButton,
            retryButton,
            errorBackButton;

        [SerializeField]
        private TMP_Text timeText,
            scoreText,
            healthText,
            phaseText,
            phaseNotice,
            resultTitle,
            resultText,
            errorText;

        private InputAction cancel;
        private int displayedSeconds = -1,
            displayedScore = -1;
        private float phaseNoticeUntil;

        private Button MenuDefault =>
            playButton && playButton.IsInteractable() ? playButton : helpButton;

        private static readonly string[] PhaseNames =
        {
            "Contenção",
            "Instabilidade",
            "Sobrecarga",
        };

        private void Start()
        {
            // A disabled session never receives Start and cannot report its own setup error.
            if (!session || !session.isActiveAndEnabled)
                ReportStartError(
                    "GameSession ausente ou desativada. Saia do Play e execute Create or Repair Project."
                );
        }

        private void OnEnable()
        {
            if (session)
            {
                session.StateChanged += ShowState;
                session.RunStarted += OnRunStarted;
                session.PhaseChanged += ShowPhase;
                session.RunEnded += ShowResult;
            }

            if (health)
                health.HealthChanged += UpdateHealth;

            cancel = controls ? controls.FindAction("UI/Cancel") : null;

            if (cancel != null)
            {
                cancel.performed += OnCancelPerformed;

                cancel.Enable();
            }
        }

        private void OnDisable()
        {
            if (session)
            {
                session.StateChanged -= ShowState;
                session.RunStarted -= OnRunStarted;
                session.PhaseChanged -= ShowPhase;
                session.RunEnded -= ShowResult;
            }

            if (health)
                health.HealthChanged -= UpdateHealth;

            if (cancel != null)
            {
                cancel.performed -= OnCancelPerformed;

                cancel.Disable();
            }
        }

        public bool ValidateBindings(out string error)
        {
            error =
                session
                && health
                && audioController
                && eventSystem
                && controls
                && controls.FindAction("UI/Cancel") != null
                && menuPanel
                && helpPanel
                && creditsPanel
                && hudPanel
                && pausePanel
                && resultPanel
                && errorPanel
                && playButton
                && helpButton
                && creditsButton
                && helpBackButton
                && creditsBackButton
                && resumeButton
                && retryButton
                && errorBackButton
                && timeText
                && scoreText
                && healthText
                && phaseText
                && phaseNotice
                && resultTitle
                && resultText
                && errorText
                    ? null
                    : "UI: há referências ausentes. Execute Create or Repair Project e Validate Project.";

            if (error != null)
                return false;

            var canvas = GetComponent<Canvas>();
            var raycaster = GetComponent<GraphicRaycaster>();
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();

            if (
                !isActiveAndEnabled
                || !canvas
                || !canvas.isActiveAndEnabled
                || !raycaster
                || !raycaster.isActiveAndEnabled
                || !eventSystem.isActiveAndEnabled
                || !module
                || !module.isActiveAndEnabled
            )
            {
                error =
                    "UI: Canvas, GameUI, GraphicRaycaster, EventSystem e InputSystemUIInputModule precisam estar ativos e habilitados.";

                return false;
            }

            if (
                module.actionsAsset != controls
                || !Matches(module.point, "Point")
                || !Matches(module.leftClick, "Click")
                || !Matches(module.move, "Navigate")
                || !Matches(module.submit, "Submit")
                || module.cancel
            )
            {
                error =
                    "UI: o módulo precisa usar Point, Click, Navigate e Submit de GameControls; Cancel pertence somente a GameUI.";

                return false;
            }

            if (
                !ValidateButton(playButton, nameof(OnPlayClicked), out error)
                || !ValidateButton(helpButton, nameof(OpenHelp), out error)
                || !ValidateButton(creditsButton, nameof(OpenCredits), out error)
                || !ValidateButton(helpBackButton, nameof(CloseHelp), out error)
                || !ValidateButton(creditsBackButton, nameof(CloseCredits), out error)
                || !ValidateButton(resumeButton, nameof(OnResumeClicked), out error)
                || !ValidateButton(retryButton, nameof(OnRetryClicked), out error)
                || !ValidateButton(errorBackButton, nameof(CloseError), out error)
            )
                return false;

            return audioController.ValidateSetup(out error);
        }

        private bool Matches(InputActionReference reference, string action) =>
            reference
            && reference.asset == controls
            && reference.action == controls.FindAction("UI/" + action);

        private bool ValidateButton(Button button, string method, out string error)
        {
            // Hidden panels are expected; their Button components must still be enabled.
            bool valid =
                button.enabled
                && button.interactable
                && button.gameObject.activeSelf
                && button.targetGraphic
                && button.targetGraphic.enabled
                && button.targetGraphic.raycastTarget
                && button.onClick.GetPersistentEventCount() == 1
                && button.onClick.GetPersistentTarget(0) == this
                && button.onClick.GetPersistentMethodName(0) == method
                && button.onClick.GetPersistentListenerState(0) != UnityEventCallState.Off;

            error = valid
                ? null
                : $"UI: botão {button.name} desabilitado ou callback {method} inválido/desligado. Saia do Play e execute Create or Repair Project.";

            return valid;
        }

        public void ShowState(GameState state)
        {
            eventSystem.SetSelectedGameObject(null);
            menuPanel.SetActive(state == GameState.Menu);
            helpPanel.SetActive(false);
            creditsPanel.SetActive(false);
            hudPanel.SetActive(state == GameState.Playing || state == GameState.Paused);
            pausePanel.SetActive(state == GameState.Paused);
            resultPanel.SetActive(state == GameState.Won || state == GameState.Lost);
            errorPanel.SetActive(false);

            eventSystem.sendNavigationEvents = state != GameState.Playing;

            if (state == GameState.Menu)
                SelectDefaultButton(MenuDefault.gameObject);
            else if (state == GameState.Paused)
                SelectDefaultButton(resumeButton.gameObject);
            else if (state == GameState.Won || state == GameState.Lost)
                SelectDefaultButton(retryButton.gameObject);
        }

        private void OnRunStarted()
        {
            ShowState(session.State);

            displayedSeconds = -1;
            displayedScore = -1;

            UpdateHealth(health.CurrentHealth, health.MaxHealth);
            ShowPhase(session.CurrentPhaseIndex);
            RefreshHud();
        }

        private void LateUpdate()
        {
            if (!session || !session.IsInitialized || !hudPanel.activeSelf)
                return;

            RefreshHud();
            phaseNotice.gameObject.SetActive(Time.time < phaseNoticeUntil);
        }

        private void RefreshHud()
        {
            int seconds = Mathf.FloorToInt(session.ElapsedSeconds);

            if (seconds != displayedSeconds)
            {
                displayedSeconds = seconds;
                timeText.text =
                    $"TEMPO  {FormatTime(session.ElapsedSeconds)} / {FormatTime(session.DurationSeconds)}";
            }

            if (session.CurrentScore != displayedScore)
            {
                displayedScore = session.CurrentScore;
                scoreText.text = $"SCORE  {displayedScore:0000}";
            }
        }

        public void UpdateHealth(int current, int max)
        {
            if (!healthText)
                return;

            string indicators = "";

            for (int i = 0; i < max; i++)
                indicators += i < current ? "[+] " : "[ ] ";

            healthText.text = $"VIDA  {current}/{max}  " + indicators;
        }

        public void ShowPhase(int index)
        {
            string name = index >= 0 && index < PhaseNames.Length ? PhaseNames[index] : "Contenção";

            phaseText.text = $"FASE {index + 1}  ·  {name.ToUpperInvariant()}";
            phaseNotice.text = $"Fase {index + 1} - {name}";
            phaseNoticeUntil = Time.time + 1.2f;

            phaseNotice.gameObject.SetActive(true);
        }

        public void ShowResult(RunResult result)
        {
            resultTitle.text = result.Won ? "CONTENÇÃO CONCLUÍDA" : "DRONE DESTRUÍDO";
            resultText.text =
                $"TEMPO SOBREVIVIDO\n{result.ElapsedSeconds.ToString("F2", CultureInfo.GetCultureInfo("pt-BR"))} s\n\nSCORE FINAL  {result.FinalScore:0000}\nVIDA RESTANTE  {result.RemainingHealth}";
        }

        public void ShowHelp(bool visible)
        {
            Click();
            eventSystem.SetSelectedGameObject(null);
            creditsPanel.SetActive(false);
            menuPanel.SetActive(!visible);
            helpPanel.SetActive(visible);
            SelectDefaultButton((visible ? helpBackButton : MenuDefault).gameObject);
        }

        public void ShowCredits(bool visible)
        {
            Click();
            eventSystem.SetSelectedGameObject(null);
            helpPanel.SetActive(false);
            menuPanel.SetActive(!visible);
            creditsPanel.SetActive(visible);
            SelectDefaultButton((visible ? creditsBackButton : MenuDefault).gameObject);
        }

        public void ShowConfigurationError(string message)
        {
            if (playButton)
                playButton.interactable = false;

            if (helpButton && creditsButton)
            {
                var navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = creditsButton,
                    selectOnDown = creditsButton,
                };

                helpButton.navigation = navigation;
                navigation.selectOnUp = helpButton;
                navigation.selectOnDown = helpButton;
                creditsButton.navigation = navigation;
            }

            if (eventSystem)
            {
                eventSystem.sendNavigationEvents = true;

                eventSystem.SetSelectedGameObject(null);
            }

            if (menuPanel)
                menuPanel.SetActive(false);

            if (errorText)
                errorText.text = message + "\n\nConsulte o Console e o README.md.";

            if (errorPanel)
                errorPanel.SetActive(true);

            if (errorBackButton)
                SelectDefaultButton(errorBackButton.gameObject);
        }

        public void CloseError()
        {
            Click();

            if (errorPanel)
                errorPanel.SetActive(false);

            if (menuPanel)
                menuPanel.SetActive(true);

            if (helpButton)
                SelectDefaultButton(helpButton.gameObject);
        }

        private void Click()
        {
            if (audioController)
                audioController.PlaySfx(SfxId.UiClick);
        }

        public void OnPlayClicked() => StartRunFromUI();

        public void OnPauseClicked()
        {
            Click();
            session.PauseRun();
        }

        public void OnResumeClicked()
        {
            Click();
            session.ResumeRun();
        }

        public void OnRetryClicked() => StartRunFromUI();

        public void OnMenuClicked()
        {
            Click();
            session.ReturnToMenu();
        }

        public void OpenHelp() => ShowHelp(true);

        public void CloseHelp() => ShowHelp(false);

        public void OpenCredits() => ShowCredits(true);

        public void CloseCredits() => ShowCredits(false);

        private void StartRunFromUI()
        {
            if (!session || !session.isActiveAndEnabled || !session.IsInitialized)
            {
                ReportStartError(
                    "A partida não iniciou: GameSession ausente, desativada ou não inicializada. Saia do Play e execute Create or Repair Project e Validate Project."
                );

                return;
            }

            Click();
            session.StartRun();
        }

        private void ReportStartError(string message)
        {
            Debug.LogError("Núcleo Instável: " + message, this);
            ShowConfigurationError(message);
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (!session)
                return;

            if (session.State == GameState.Menu)
            {
                if (helpPanel.activeSelf)
                    ShowHelp(false);
                else if (creditsPanel.activeSelf)
                    ShowCredits(false);
            }
            else if (session.State == GameState.Paused)
                OnResumeClicked();
            else if (session.State == GameState.Won || session.State == GameState.Lost)
                OnMenuClicked();
        }

        private void SelectDefaultButton(GameObject button)
        {
            if (
                eventSystem
                && button
                && button.activeInHierarchy
                && button.TryGetComponent<Selectable>(out var s)
                && s.IsInteractable()
            )
                eventSystem.SetSelectedGameObject(button);
        }

        public static string FormatTime(float seconds)
        {
            int value = Mathf.FloorToInt(Mathf.Max(0, seconds));

            return $"{value / 60:00}:{value % 60:00}";
        }
    }
}
