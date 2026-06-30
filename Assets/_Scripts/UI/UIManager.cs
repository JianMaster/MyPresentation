using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour {
    public SpeechText _speechText;
    public TextMeshProUGUI _textDisplay;

    private int _txtIndex = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        if (_speechText == null) {
            Debug.LogError("SpeechText is not assigned in the UIManager.");
            return;
        }
        _textDisplay.text = _speechText.GetProcessedText(_txtIndex);
    }

    // Update is called once per frame
    void Update() {
        if (Keyboard.current.enterKey.wasPressedThisFrame) {
            _txtIndex++;
            _textDisplay.text = _speechText.GetProcessedText(_txtIndex);
        }
    }
}
