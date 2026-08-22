using TMPro;
using UnityEngine;

public sealed class UIManager : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI _textDisplay;

    public bool IsConfigured => _textDisplay != null;

    public void ShowWaiting(string message) {
        SetText($"<size=120%>音声分析システムを待っています</size>\n{message}");
    }

    public void ShowLine(int textIndex, int total, string body, string status) {
        SetText(
            $"<size=85%>台詞 {textIndex + 1} / {total}</size>\n" +
            $"{body}\n\n<color=#B2FF59>{status}</color>"
        );
    }

    public void ShowFinalReport(SessionEvaluationResult result, string logPath) {
        SetText(
            $"<size=130%><b>トレーニング完了</b></size>\n\n" +
            $"総合　{result.totalScore:F1}\n" +
            $"語気　{result.deliveryScore:F1}　　話速　{result.speedScore:F1}\n" +
            $"音量　{result.volumeScore:F1}　　視線　{result.gazeScore:F1}\n\n" +
            $"得意：{result.strongestDimension}\n" +
            $"優先改善：{result.weakestDimension}\n" +
            $"{result.advice}\n\n" +
            $"<size=65%><color=#90A4AE>記録: {logPath}</color></size>"
        );
    }

    public void ShowFatalError(string message) {
        SetText($"<color=#FF5252><b>開始できません</b>\n{message}</color>");
    }

    private void SetText(string value) {
        if (_textDisplay != null) {
            _textDisplay.text = value;
        }
    }
}
