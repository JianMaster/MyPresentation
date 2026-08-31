using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PresentationTaskController : MonoBehaviour {
    [Header("Scene dependencies")]
    [SerializeField] private AnalysisUdpReceiver _analysisReceiver;
    [SerializeField] private AudienceView _audienceView;
    [SerializeField] private UIManager _ui;

    [Header("Training configuration")]
    [SerializeField] private SpeechText _speechText;
    [SerializeField] private ScoringProfile _scoringProfile;
    [SerializeField] private string _participantId = "anonymous";

    private readonly List<ScoringSample> _lineSamples = new List<ScoringSample>();
    private readonly List<LineEvaluationResult> _lineResults = new List<LineEvaluationResult>();
    private PresentationTaskState _state = PresentationTaskState.WaitingForVoice;
    private AudienceFeedbackController _audience;
    private SessionLogWriter _logWriter;
    private int _lineIndex;
    private double _speechStartedAt = -1d;

    private void OnEnable() {
        if (_analysisReceiver != null) {
            _analysisReceiver.AnalysisReceived += HandleAnalysisReceived;
        }
    }

    private void Start() {
        if (!ValidateConfiguration()) {
            enabled = false;
            return;
        }

        _audience = new AudienceFeedbackController(_audienceView);
        _logWriter = new SessionLogWriter(_participantId, _scoringProfile);
        EnterWaitingForVoice();
    }

    private void Update() {
        if (_state == PresentationTaskState.RecordingLine && _audience.TryCompleteGaze()) {
            _logWriter?.LogGazeCompleted(
                CurrentItem.lineId,
                _audience.TargetRoleIndex,
                _audience.TargetRoleId
            );
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.enterKey.wasPressedThisFrame) return;

        if (_state == PresentationTaskState.WaitingForSpeech ||
            _state == PresentationTaskState.RecordingLine) {
            CompleteCurrentLine();
        }
    }

    private void OnDisable() {
        if (_analysisReceiver != null) {
            _analysisReceiver.AnalysisReceived -= HandleAnalysisReceived;
        }
    }

    private void OnDestroy() {
        _logWriter?.Dispose();
    }

    private void HandleAnalysisReceived(VoiceAnalysisPacket packet) {
        if (packet == null) return;
        _logWriter?.LogVoiceAnalysisSample(packet, _state, CurrentItem?.lineId);

        if (_state == PresentationTaskState.WaitingForVoice) {
            _lineIndex = 0;
            EnterCurrentLine();
            return;
        }

        double receivedAt = Time.realtimeSinceStartupAsDouble;
        if (_state == PresentationTaskState.WaitingForSpeech && packet.speech_detected) {
            EnterRecordingLine(receivedAt);
        }

        if (_state == PresentationTaskState.RecordingLine && packet.speech_detected) {
            RegisterScoringSample(packet, receivedAt);
        }
    }

    private void EnterWaitingForVoice() {
        _state = PresentationTaskState.WaitingForVoice;
        _ui.ShowWaiting("VoiceAnalyzer の Arousal / Valence 出力を待っています。");
    }

    private void EnterCurrentLine(string retryReason = null) {
        if (_lineIndex >= _speechText.Items.Length) {
            CompleteSession();
            return;
        }

        _lineSamples.Clear();
        _speechStartedAt = -1d;
        _state = PresentationTaskState.WaitingForSpeech;
        _audience.BeginLine(CurrentItem, _lineIndex);
        _logWriter?.LogLineStart(
            CurrentItem,
            _lineIndex,
            _audience.TargetRoleIndex,
            _audience.TargetRoleId
        );

        string status = string.IsNullOrEmpty(retryReason)
            ? "読み始めてください。完了後に Enter を押してください。"
            : $"{retryReason}\n同じ台詞をもう一度読んでください。";
        ShowCurrentLine(status);
    }

    private void EnterRecordingLine(double startedAt) {
        _speechStartedAt = startedAt;
        _state = PresentationTaskState.RecordingLine;
        ShowCurrentLine("発話を検出しました。読み終わったら Enter を押してください。");
    }

    private void RegisterScoringSample(VoiceAnalysisPacket packet, double receivedAt) {
        ScoringSample sample = ScoringSample.FromPacket(packet, receivedAt);
        _lineSamples.Add(sample);

        if (receivedAt - _speechStartedAt < packet.feature_window_seconds) return;

        float deliveryScore = PerformanceEvaluator.ScoreDelivery(
            CurrentItem.deliveryStyle,
            sample.arousal,
            sample.valence
        );
        float speedScore = PerformanceEvaluator.ScoreSpeed(
            CurrentItem.speed,
            sample.speechRateValue,
            _scoringProfile
        );
        float volumeScore = PerformanceEvaluator.ScoreVolume(
            CurrentItem.volume,
            sample.volumeValue,
            _scoringProfile
        );
        bool matchesTarget = deliveryScore >= _scoringProfile.FeedbackMinimumDimensionScore &&
                             speedScore >= _scoringProfile.FeedbackMinimumDimensionScore &&
                             volumeScore >= _scoringProfile.FeedbackMinimumDimensionScore;
        _audience.RegisterVoiceMatch(matchesTarget, _scoringProfile.FeedbackConsecutiveMatches);
    }

    private void CompleteCurrentLine() {
        if (_state != PresentationTaskState.RecordingLine || _speechStartedAt < 0d) {
            ShowCurrentLine("まだ音声を検出していません。台詞を読んでください。");
            return;
        }

        LineEvaluationResult result = PerformanceEvaluator.EvaluateLine(
            CurrentItem,
            _audience.TargetRoleIndex,
            _speechStartedAt,
            Time.realtimeSinceStartupAsDouble,
            _audience.GazeCompleted,
            _lineSamples,
            _scoringProfile
        );
        _logWriter?.LogLineResult(result);

        if (!result.valid) {
            EnterCurrentLine(result.invalidReason);
            return;
        }

        _lineResults.Add(result);
        _lineIndex++;
        EnterCurrentLine();
    }

    private void CompleteSession() {
        _state = PresentationTaskState.Completed;
        SessionEvaluationResult result = PerformanceEvaluator.EvaluateSession(_lineResults, _scoringProfile);
        _logWriter?.LogSessionResult(result);
        _audience.FinishSession(result.totalScore, _scoringProfile.ApplauseThreshold);

        string logPath = _logWriter?.FilePath ?? string.Empty;
        _logWriter?.Dispose();
        _logWriter = null;
        _ui.ShowFinalReport(result, logPath);
    }

    private void ShowCurrentLine(string status) {
        _ui.ShowLine(
            _lineIndex,
            _speechText.Items.Length,
            _speechText.GetProcessedText(_lineIndex),
            status
        );
    }

    private bool ValidateConfiguration() {
        if (_ui == null || !_ui.IsConfigured) {
            Debug.LogError("PresentationTaskController requires a configured UIManager.");
            return false;
        }
        if (_analysisReceiver == null || _audienceView == null || !_audienceView.IsConfigured ||
            _speechText == null || _scoringProfile == null) {
            _ui.ShowFatalError("必要なシーン参照または設定が不足しています。");
            return false;
        }
        if (_speechText.Items == null || _speechText.Items.Length == 0) {
            _ui.ShowFatalError("台詞データがありません。");
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
