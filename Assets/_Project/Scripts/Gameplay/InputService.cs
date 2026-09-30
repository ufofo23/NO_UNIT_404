using UnityEngine;
using UnityEngine.InputSystem;

namespace NO404.Gameplay
{
    /// <summary>
    /// Input actions built in code so there is exactly one source of truth and no asset
    /// wiring step. Bindings match GDD 8.1; gamepad bindings are present from the start so
    /// the P1 pad-support task is a polish pass, not a rewrite.
    /// </summary>
    public sealed class InputService
    {
        public readonly InputActionMap PlayerMap = new InputActionMap("Player");
        public readonly InputActionMap UiMap = new InputActionMap("UI");

        public InputAction Move { get; private set; }
        public InputAction Look { get; private set; }
        public InputAction Interact { get; private set; }
        public InputAction Cancel { get; private set; }
        public InputAction Sprint { get; private set; }
        public InputAction Crouch { get; private set; }
        public InputAction Flashlight { get; private set; }
        public InputAction Pause { get; private set; }
        public InputAction Tablet { get; private set; }
        public InputAction EvidenceBoard { get; private set; }
        public InputAction Snapshot { get; private set; }
        public InputAction Console { get; private set; }


        public InputService()
        {
            Move = PlayerMap.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            Look = PlayerMap.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            Look.AddBinding("<Gamepad>/rightStick");

            Interact = PlayerMap.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonSouth");

            Cancel = PlayerMap.AddAction("Cancel", InputActionType.Button, "<Mouse>/rightButton");
            Cancel.AddBinding("<Gamepad>/buttonEast");

            Sprint = PlayerMap.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Crouch = PlayerMap.AddAction("Crouch", InputActionType.Button, "<Keyboard>/c");
            Crouch.AddBinding("<Gamepad>/buttonEast");

            Flashlight = PlayerMap.AddAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
            Flashlight.AddBinding("<Gamepad>/dpad/up");

            Tablet = PlayerMap.AddAction("Tablet", InputActionType.Button, "<Keyboard>/tab");
            Tablet.AddBinding("<Gamepad>/selectButton");

            EvidenceBoard = PlayerMap.AddAction("EvidenceBoard", InputActionType.Button, "<Keyboard>/q");
            EvidenceBoard.AddBinding("<Gamepad>/dpad/left");

            Snapshot = PlayerMap.AddAction("Snapshot", InputActionType.Button, "<Keyboard>/r");
            Snapshot.AddBinding("<Gamepad>/dpad/right");

            Pause = UiMap.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");

            Console = UiMap.AddAction("Console", InputActionType.Button, "<Keyboard>/backquote");

        }

        public void Enable()
        {
            PlayerMap.Enable();
            UiMap.Enable();
        }

        public void Disable()
        {
            PlayerMap.Disable();
            UiMap.Disable();
        }

        /// <summary>Movement/look are suppressed while the PC, tablet or a menu owns input.</summary>
        public void SetGameplayEnabled(bool enabled)
        {
            if (enabled) PlayerMap.Enable(); else PlayerMap.Disable();
        }

        public Vector2 MoveValue { get { return PlayerMap.enabled ? Move.ReadValue<Vector2>() : Vector2.zero; } }
        public Vector2 LookValue { get { return PlayerMap.enabled ? Look.ReadValue<Vector2>() : Vector2.zero; } }
        public bool SprintHeld { get { return PlayerMap.enabled && Sprint.IsPressed(); } }
    }
}
