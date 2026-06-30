using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Texts", menuName = "Presentation/Texts")]
public class SpeechText : ScriptableObject {
    public const string ResourcePath = "Texts/Texts";
    private const string EmotionColor = "#FFD54A";
    private const string SpeedColor = "#4DD0E1";
    private const string VolumeColor = "#FF7043";

    public static SpeechText LoadDefault() {
        return Resources.Load<SpeechText>(ResourcePath);
    }

    [SerializeField] private TextItem[] _texts = Array.Empty<TextItem>();
    public TextItem[] Items => _texts;

    public string GetProcessedText(int textIndex) {
        if (textIndex < 0 || textIndex >= _texts.Length || _texts[textIndex] == null) {
            return string.Empty;
        }

        TextItem item = _texts[textIndex];
        string body = ProcessBody(item);

        return $"[{ColorTag(EnumTool.GetEmotionText(item.emotion), EmotionColor)}] [{ColorTag(EnumTool.GetSpeedText(item.speed), SpeedColor)}] [{ColorTag(EnumTool.GetVolumeText(item.volume), VolumeColor)}]\n{body}";
    }

    private static string ColorTag(string text, string color) {
        return $"<color={color}>{text}</color>";
    }

    private static string ProcessBody(TextItem item) {
        string body = (item.text ?? string.Empty)
            .Replace(",", string.Empty)
            .Replace("、", string.Empty)
            .Replace("，", string.Empty);

        foreach (string pause in item.pause_after ?? Array.Empty<string>()) {
            if (string.IsNullOrEmpty(pause)) continue;

            body = body.Replace(pause, $"{pause}/");
        }

        foreach (string emphasis in item.emphasis ?? Array.Empty<string>()) {
            if (string.IsNullOrEmpty(emphasis)) continue;

            body = body.Replace(emphasis, $"<color=\"yellow\">{emphasis}</color>");
        }

        return body;
    }
}

[Serializable]
public class TextItem {
    [TextArea(2, 5)]
    public string text;
    public Emotion emotion;
    public Speed speed;
    public Volume volume;
    public string[] emphasis = Array.Empty<string>();
    public string[] pause_after = Array.Empty<string>();
    public string gesture;
    public string gaze;
}
