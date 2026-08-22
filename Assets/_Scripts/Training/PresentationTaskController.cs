using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PresentationTaskController : MonoBehaviour {
    [SerializeField] private AnalysisUdpReceiver _analysisReceiver;
    [SerializeField] private SceneManager _sceneManager;
    [SerializeField] private UIManager _uiManager;
    [SerializeField] private SpeechText _speechText;
    [SerializeField] private ScoringProfile _scoringProfile;
    [SerializeField] private string _participantId = "anonymous";

    private readonly List<ScoringSample> _lineSamples = new List<ScoringSample>();
    private readonly List<LineEvaluationResult> _lineResults = new List<LineEvaluationResult>();
    private PresentationTaskState _state = PresentationTaskState.WaitingForAnalyzer;
    private AudienceFeedbackController _audience;
    private SessionLogWriter _logWriter;
    private int _lineIndex;
    private double _speechStartedAt = -1d;

    public PresentationTaskState State => _state;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() {
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null && uiManager.GetComponent<PresentationTaskController>() == null) {
            uiManager.gameObject.AddComponent<PresentationTaskController>();
        }
    }

    private void Awake() {
        _analysisReceiver ??= FindFirstObjectByType<AnalysisUdpReceiver>();
        _sceneManager ??= FindFirstObjectByType<SceneManager>();
        _uiManager ??= GetComponent<UIManager>() ?? FindFirstObjectByType<UIManager>();
        _speechText ??= _uiManager != null ? _uiManager.SpeechText : SpeechText.LoadDefault();
        _scoringProfile ??= ScoringProfile.LoadDefault();
    }

    private void OnEnable() {
        if (_analysisReceiver != null) {
            _analysisReceiver.DeliveryReceived += HandleDeliveryReceived;
        }
    }

    private void Start() {
        if (!ValidateConfiguration()) return;
        _audience = new AudienceFeedbackController(_sceneManager);
        _logWriter = new SessionLogWriter(_participantId, _scoringProfile);
        _uiManager.ShowWaiting("VoiceAnalyzer の delivery 出力を待っています。");
    }

    private void Update() {
        if (_state == PresentationTaskState.PresentingLine && _audience.TryCompleteGaze()) {
            TextItem item = CurrentItem;
            _logWriter?.LogGazeCompleted(
                item?.lineId,
                _audience.TargetRoleIndex,
                _audience.TargetRoleId
            );
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.enterKey.wasPressedThisFrame) return;

        if (_state == PresentationTaskState.AwaitingSpeech || _state == PresentationTaskState.PresentingLine) {
            CompleteCurrentLine();
        }
    }

    private void OnDisable() {
        if (_analysisReceiver != null) {
            _analysisReceiver.DeliveryReceived -= HandleDeliveryReceived;
        }
    }

    private void OnDestroy() {
        _logWriter?.Dispose();
        _logWriter = null;
    }

    private void HandleDeliveryReceived(DeliveryPacket packet) {
        if (packet == null) return;
        _logWriter?.LogDeliverySample(packet, _state, CurrentItem?.lineId);

        if (_state == PresentationTaskState.WaitingForAnalyzer) {
            if (!packet.baseline_ready) {
                _uiManager.ShowWaiting("VoiceAnalyzer の delivery 出力を待っています。");
                return;
            }

            _lineIndex = 0;
            BeginCurrentLine(false);
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (_state == PresentationTaskState.AwaitingSpeech && packet.speech_detected) {
            _speechStartedAt = now;
            _state = PresentationTaskState.PresentingLine;
            _uiManager.ShowLine(_lineIndex, _speechText.Items.Length, "発話を検出しました。読み終わったら Enter を押してください。");
        }

        if (_state != PresentationTaskState.PresentingLine || !packet.speech_detected) return;

        ScoringSample sample = ScoringSample.FromPacket(packet, now);
        _lineSamples.Add(sample);

        if (now - _speechStartedAt < packet.feature_window_seconds) return;
        float deliveryScore = PerformanceEvaluator.ScoreDelivery(
            CurrentItem.deliveryStyle,
            sample.rawArousalScore,
            sample.rawDominanceScore
        );
        float volumeScore = PerformanceEvaluator.ScoreVolume(
            CurrentItem.volume,
            sample.relativeVolumeScore,
            _scoringProfile
        );
        bool voiceMatches = deliveryScore >= _scoringProfile.FeedbackMinimumDimensionScore &&
                            volumeScore >= _scoringProfile.FeedbackMinimumDimensionScore;
        _audience.RegisterVoiceMatch(voiceMatches, _scoringProfile.FeedbackConsecutiveMatches);
    }

    private void BeginCurrentLine(bool retry, string retryReason = null) {
        if (_lineIndex >= _speechText.Items.Length) {
            CompleteSession();
            return;
        }

        _lineSamples.Clear();
        _speechStartedAt = -1d;
        _state = PresentationTaskState.AwaitingSpeech;
        _audience.BeginLine(CurrentItem, _lineIndex);
        _logWriter?.LogLineStart(
            CurrentItem,
            _lineIndex,
            _audience.TargetRoleIndex,
            _audience.TargetRoleId
        );
        string status = retry
            ? $"{retryReason}\n同じ台詞をもう一度読んでください。"
            : "読み始めてください。完了後に Enter を押してください。";
        _uiManager.ShowLine(_lineIndex, _speechText.Items.Length, status);
    }

    private void CompleteCurrentLine() {
        if (_state == PresentationTaskState.AwaitingSpeech || _speechStartedAt < 0d) {
            _uiManager.ShowLine(_lineIndex, _speechText.Items.Length, "まだ音声を検出していません。台詞を読んでください。");
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        LineEvaluationResult result = PerformanceEvaluator.EvaluateLine(
            CurrentItem,
            _audience.TargetRoleIndex,
            _scoringProfile.ReferenceSpeedCpm,
            _speechStartedAt,
            now,
            _audience.GazeCompleted,
            _lineSamples,
            _scoringProfile
        );
        _logWriter?.LogLineResult(result);

        if (!result.valid) {
            BeginCurrentLine(true, result.invalidReason);
            return;
        }

        _lineResults.Add(result);
        _lineIndex++;
        BeginCurrentLine(false);
    }

    private void CompleteSession() {
        _state = PresentationTaskState.Completed;
        SessionEvaluationResult result = PerformanceEvaluator.EvaluateSession(_lineResults, _scoringProfile);
        _logWriter?.LogSessionResult(result);
        _audience.FinishSession(result.totalScore, _scoringProfile.ApplauseThreshold);
        _uiManager.ShowFinalReport(result, _logWriter?.FilePath ?? string.Empty);
    }

    private bool ValidateConfiguration() {
        if (_analysisReceiver == null || _sceneManager == null || _uiManager == null || _speechText == null || _scoringProfile == null) {
            _uiManager?.ShowFatalError("必要なコンポーネントまたは設定が見つかりません。");
            enabled = false;
            return false;
        }
        if (_speechText.Items == null || _speechText.Items.Length == 0) {
            _uiManager.ShowFatalError("台詞データがありません。");
            enabled = false;
            return false;
        }
        return true;
    }

    private TextItem CurrentItem {
        get {
            if (_speechText?.Items == null || _lineIndex < 0 || _lineIndex >= _speechText.Items.Length) return null;
            return _speechText.Items[_lineIndex];
        }
    }
}
