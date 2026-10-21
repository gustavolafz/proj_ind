using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NucleoInstavel
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField]
        private InputActionAsset controls;

        private InputActionMap gameplay,
            system;
        private InputAction move,
            focus,
            pause;

        public event Action PauseRequested;

        public Vector2 MoveInput =>
            gameplay != null && gameplay.enabled ? move.ReadValue<Vector2>() : Vector2.zero;
        public bool FocusHeld => gameplay != null && gameplay.enabled && focus.IsPressed();

        public bool ValidateBindings(out string error)
        {
            error =
                controls
                && controls.FindAction("Gameplay/Move") != null
                && controls.FindAction("Gameplay/Focus") != null
                && controls.FindAction("System/Pause") != null
                    ? null
                    : "GameControls: faltam Gameplay/Move, Gameplay/Focus ou System/Pause.";

            return error == null;
        }

        private void Awake()
        {
            if (!ValidateBindings(out _))
                return;

            gameplay = controls.FindActionMap("Gameplay");
            system = controls.FindActionMap("System");
            move = gameplay.FindAction("Move");
            focus = gameplay.FindAction("Focus");
            pause = system.FindAction("Pause");
        }

        private void OnEnable()
        {
            if (pause == null)
                return;

            pause.performed += OnPausePerformed;

            system.Enable();
        }

        private void OnDisable()
        {
            if (pause != null)
                pause.performed -= OnPausePerformed;

            gameplay?.Disable();
            system?.Disable();
        }

        public void SetGameplayEnabled(bool value)
        {
            if (value)
                gameplay?.Enable();
            else
                gameplay?.Disable();
        }

        public void ClearInput()
        {
            move?.Reset();
            focus?.Reset();
        }

        private void OnPausePerformed(InputAction.CallbackContext context) =>
            PauseRequested?.Invoke();
    }
}
