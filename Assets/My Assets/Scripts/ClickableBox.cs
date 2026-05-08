using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public class ClickableBox : MonoBehaviour
{
    [Header("Click Settings")]
    [SerializeField] private Camera _camera;
    [SerializeField] private ClickButton _mouseButton = ClickButton.Left;
    [SerializeField] private LayerMask _layerMask = ~0;

    [Header("Event")]
    public UnityEvent OnClicked;

    private enum ClickButton { Left, Right, Middle }

    private void Start()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current != null)
            HandleMouse();

        if (Touchscreen.current != null)
            HandleTouch();
    }

    private void HandleMouse()
    {
        bool buttonPressed = _mouseButton switch
        {
            ClickButton.Left => Mouse.current.leftButton.wasPressedThisFrame,
            ClickButton.Right => Mouse.current.rightButton.wasPressedThisFrame,
            ClickButton.Middle => Mouse.current.middleButton.wasPressedThisFrame,
            _ => false
        };

        if (!buttonPressed) return;

        CastAndCheck(Mouse.current.position.ReadValue());
    }

    private void HandleTouch()
    {
        foreach (var touch in Touchscreen.current.touches)
        {
            // Only fire on the frame the finger first touches the screen
            if (!touch.press.wasPressedThisFrame) continue;

            CastAndCheck(touch.position.ReadValue());
        }
    }

    private void CastAndCheck(Vector2 screenPos)
    {
        Ray ray = _camera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _layerMask))
        {
            if (hit.collider.gameObject == gameObject)
                OnClicked.Invoke();
        }
    }
}