using System;
using System.Collections.Generic;
using UnityEngine;

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
    private PresentationTaskState _state = PresentationTaskState.WaitingForSpeech;
    private AudienceFeedbackController _audience;
    private SessionLogWriter _logWriter;
    private int _lineIndex;
    private double _lineShownAt;
    private double _utteranceStartedAt;
    private bool _initialized;

    private void OnEnable() {
        if (_analysisReceiver != null) {
            _analysisReceiver.AnalysisReceived += HandleAnalysisReceived;
        }
        if (_initialized && _state != PresentationTaskState.Completed) EnterCurrentLine();
    }

    private void Start() {
        if (!ValidateConfiguration()) {
            enabled = false;
            return;
        }
        _audience = new AudienceFeedbackController(_audienceView);
        _logWriter = new SessionLogWriter(_participantId, _scoringProfile);
        _initialized = true;
        EnterCurrentLine();
    }

    private void Update() {
        UpdateGaze();
    }

    private void UpdateGaze() {
        // VoiceAD reports after inference; allow gaze throughout the displayed line.
        if (_initialized && _state != PresentationTaskState.Completed && _audience.TryCompleteGaze()) {
            _logWriter?.LogGazeCompleted(
                CurrentItem.lineId,
                _audience.TargetRoleIndex,
                _audience.TargetRoleId
            );
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
        if (!_initialized || packet == null || _state == PresentationTaskState.Completed) return;
        // Ignore results from before this line, including delayed or duplicated end packets.
        if (packet.utterance_started_at < _lineShownAt || packet.utterance_started_at < _utteranceStartedAt) return;

        if (packet.utterance_started_at > _utteranceStartedAt) {
            // A dropped end packet must not mix two utterances into one evaluation.
            _lineSamples.Clear();
            _utteranceStartedAt = packet.utterance_started_at;
            _state = PresentationTaskState.RecordingLine;
            ShowCurrentLine("音声を分析しています。発話終了を検出すると自動で次へ進みます。");
        }
        _logWriter?.LogVoiceAnalysisSample(packet, _state, CurrentItem?.lineId);
        UpdateGaze();
        if (!_lineSamples.Exists(sample => sample.segmentIndex == packet.segment_index)) {
            RegisterScoringSample(packet);
        }
        if (packet.speech_ended) CompleteCurrentLine();
    }

    private void EnterCurrentLine(string retryReason = null) {
        if (_lineIndex >= _speechText.Items.Length) {
            CompleteSession();
            return;
        }

        _lineSamples.Clear();
        _lineShownAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
        _utteranceStartedAt = 0d;
        _state = PresentationTaskState.WaitingForSpeech;
        _audience.BeginLine(CurrentItem, _lineIndex);
        _logWriter?.LogLineStart(
            CurrentItem,
            _lineIndex,
            _audience.TargetRoleIndex,
            _audience.TargetRoleId
        );

        string status = string.IsNullOrEmpty(retryReason)
            ? "読み始めてください。発話後に少し黙ると自動で次へ進みます。"
            : $"{retryReason}\n同じ台詞をもう一度読んでください。";
        ShowCurrentLine(status);
    }

    private void RegisterScoringSample(VoiceAnalysisPacket packet) {
        ScoringSample sample = ScoringSample.FromPacket(packet);
        _lineSamples.Add(sample);

        float deliveryScore = PerformanceEvaluator.ScoreDelivery(
            CurrentItem.deliveryStyle,
            sample.arousal,
            sample.dominance
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
        if (_state != PresentationTaskState.RecordingLine) return;

        LineEvaluationResult result = PerformanceEvaluator.EvaluateLine(
            CurrentItem,
            _audience.TargetRoleIndex,
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
