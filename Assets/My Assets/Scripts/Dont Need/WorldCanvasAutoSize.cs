using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class WorldCanvasAutoSize : MonoBehaviour
{
    // 1080 / 9 = 120 — change this if you use a different reference resolution
    [SerializeField] private float pixelsPerUnit = 120f;

    private RectTransform _rectTransform;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        AdjustCanvasSize();
    }

    void AdjustCanvasSize()
    {
        float canvasWidth = Screen.width / pixelsPerUnit;  // 1080 / 120 = 9
        float canvasHeight = Screen.height / pixelsPerUnit;  // 1920 / 120 = 16

        _rectTransform.sizeDelta = new Vector2(canvasWidth, canvasHeight);

        Debug.Log($"Canvas Size Set To: {canvasWidth} x {canvasHeight}");
    }
}