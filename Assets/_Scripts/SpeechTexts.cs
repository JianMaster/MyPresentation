using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Texts", menuName = "Presentation/Texts")]
public sealed class SpeechText : ScriptableObject {
    private const string EmotionColor = "#FFD54A";
    private const string SpeedColor = "#4DD0E1";
    private const string VolumeColor = "#FF7043";

    [SerializeField] private TextItem[] _texts = Array.Empty<TextItem>();
    public TextItem[] Items => _texts;

    public string GetProcessedText(int textIndex) {
        if (textIndex < 0 || textIndex >= _texts.Length || _texts[textIndex] == null) {
            return string.Empty;
        }

        TextItem item = _texts[textIndex];
        string body = ProcessBody(item);

        string guidance = string.Empty;
        if (!string.IsNullOrWhiteSpace(item.gesture) || !string.IsNullOrWhiteSpace(item.gaze)) {
            guidance = $"\n<size=75%><color=#B0BEC5>動作：{item.gesture}　視線：{item.gaze}</color></size>";
        }

        return $"[{ColorTag(EnumTool.GetDeliveryStyleText(item.deliveryStyle), EmotionColor)}] [{ColorTag(EnumTool.GetSpeedText(item.speed), SpeedColor)}] [{ColorTag(EnumTool.GetVolumeText(item.volume), VolumeColor)}]\n{body}{guidance}";
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
public sealed class TextItem {
    public string lineId;
    [TextArea(2, 5)]
    public string text;
    public DeliveryStyle deliveryStyle;
    public Speed speed;
    public Volume volume;
    [Tooltip("Negative values rotate through the configured audience roles.")]
    public int targetRoleIndex = -1;
    public string[] emphasis = Array.Empty<string>();
    public string[] pause_after = Array.Empty<string>();
    public string gesture;
    public string gaze;
}
