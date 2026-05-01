using TMPro;
using UnityEngine;

public class CanvasScaler : MonoBehaviour
{
    public TextMeshProUGUI widthText;
    public TextMeshProUGUI heightText;
    public TextMeshProUGUI dpiText;
    public Transform MainCam;

    void Start()
    {
        GetScreenInfo();
    }

    void GetScreenInfo()
    {
        int width = Screen.width;
        int height = Screen.height;
        float dpi = Screen.dpi;

        widthText.text = $"Width: {width}px";
        heightText.text = $"Height: {height}px";
        dpiText.text = $"DPI: {dpi}";

        MainCam.position = new Vector3(transform.position.x, transform.position.y, MainCam.position.z);
    }
}
