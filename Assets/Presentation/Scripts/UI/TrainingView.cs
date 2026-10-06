using TMPro;
using UnityEngine;

namespace PresentationRewrite {
    public sealed class TrainingView : MonoBehaviour {
        [SerializeField] private TMP_Text _text;
        private string _body;
        private string _status;
        private string _logError;

        private static readonly string[] DeliveryNames = { "落ち着き・自信", "活力・自信", "活力・ためらい", "落ち着き・ためらい" };
        private static readonly string[] SpeedNames = { "遅め", "普通", "速め" };
        private static readonly string[] VolumeNames = { "小さめ", "普通", "大きめ" };

        public bool IsConfigured => _text != null;
        public void ShowMessage(string message) => _text.text = message;

        // VoiceAD sends analysis results, not heartbeats or an immediate speech-start event.
        public static string StatusFor(bool received, float secondsWithoutResult, float timeout) {
            if (secondsWithoutResult >= timeout) {
                return received
                    ? "しばらく結果が届いていません。終了通知を待っています。VoiceADを確認し、必要なら同じ台詞を読み直してください。"
                    : "まだ分析結果が届いていません。未発話なら読み始めてください。発話済みならVoiceADとマイクを確認してください。";
            }
            return received ? "音声を分析中。読み終えて少し黙ると次へ進みます。"
                : "読み始めてください。音声分析の結果を待っています。";
        }

        public void ShowStatus(string status, string logError) {
            if (_status == status && _logError == logError) return;
            _status = status;
            _logError = logError;
            _text.text = _body + "\n" + status;
                        //+ LogWarning(logError);
        }

        public void ShowLine(SpeechLine line, int index, int count) {
            _body = $"台詞 {index + 1} / {count}\n" +
                $"[{DeliveryNames[(int)line.deliveryStyle]}] [{SpeedNames[(int)line.speed]}] [{VolumeNames[(int)line.volume]}]\n" +
                $"{line.FormattedText()}";
            _status = null;
        }

        public void ShowResult(Vector4 scores, ScoringSettings settings, string logError, bool includeGaze = true) {
            _text.text = $"トレーニング完了\n\n総合　{Scoring.Total(scores, settings, includeGaze):F1}\n" +
                $"音声表現　{scores.x:F1}\n話速　{scores.y:F1}\n音量　{scores.z:F1}\n" +
                (includeGaze ? $"視線　{scores.w:F1}\n\n" : "視線　対象なし\n\n") +
                Scoring.Advice(scores, settings, includeGaze) + LogWarning(logError);
        }

        private static string LogWarning(string error) => error == null ? string.Empty : "\n<color=#FF7043>記録を保存できませんでした。</color>";
    }
}
