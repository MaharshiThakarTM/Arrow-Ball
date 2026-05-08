using UnityEngine;
using UnityEngine.UI;

public class LevelController : MonoBehaviour
{
    public bool DoReset;
    [SerializeField] private GameObject _playArea;
    [SerializeField] private GameObject _gameOverPanel;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _quitButton;

    private Transform _playAreaHolder;
    private GameObject _playAreaDuplicate;

    private void Awake()
    {
        _playAreaHolder = _playArea.transform.parent;
        if (DoReset)
        {
            _playAreaDuplicate = Instantiate(_playArea, _playAreaHolder);
            _playArea.SetActive(false);
        }
    }

    private void Start()
    {
        _resetButton.onClick.AddListener(OnResetButtonClicked);
        _quitButton.onClick.AddListener(OnQuitButtonClicked);
    }

    public void OnLevelCompleted()
    {
        _gameOverPanel.SetActive(true);
    }

    private void OnResetButtonClicked()
    {
        _gameOverPanel.SetActive(false);
        if (DoReset)
        {
            Destroy(_playAreaDuplicate);
            _playAreaDuplicate = Instantiate(_playArea, _playAreaHolder);
            _playAreaDuplicate.SetActive(true);
        }
    }

    private void OnQuitButtonClicked()
    {
        Application.Quit();
    }
}
