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
    private AnalysisData _latestData;
    private int _lineIndex;
    private double _calibrationSpeechStartedAt = -1d;
    private double _speechStartedAt = -1d;
    private float _baselineCpm;

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
            _analysisReceiver.AnalysisReceived += HandleAnalysisReceived;
        }
    }

    private void Start() {
        if (!ValidateConfiguration()) return;
        _audience = new AudienceFeedbackController(_sceneManager);
        _logWriter = new SessionLogWriter(_participantId, _scoringProfile);
        _uiManager.ShowWaiting("VoiceAnalyzer を起動してください。");
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

        if (_state == PresentationTaskState.Calibration) {
            CompleteCalibration();
        }
        else if (_state == PresentationTaskState.AwaitingSpeech || _state == PresentationTaskState.PresentingLine) {
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
        _logWriter = null;
    }

    private void HandleAnalysisReceived(AnalysisData data) {
        if (data == null) return;
        _latestData = data;
        _logWriter?.LogAnalysisSample(data, _state, CurrentItem?.lineId);

        if (_state == PresentationTaskState.WaitingForAnalyzer) {
            if (data.enabled == null || !data.enabled.delivery || data.analyzers?.delivery == null) {
                _uiManager.ShowWaiting("語気分析（delivery）を有効にしてください。");
                return;
            }
            BeginCalibration();
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (_state == PresentationTaskState.Calibration) {
            if (data.speech_detected && _calibrationSpeechStartedAt < 0d) {
                _calibrationSpeechStartedAt = now;
                _uiManager.ShowCalibration(_scoringProfile.CalibrationText, "校正中です。読み終わるまで続けてください。\n");
            }
            return;
        }

        if (_state == PresentationTaskState.AwaitingSpeech && data.speech_detected) {
            _speechStartedAt = now;
            _state = PresentationTaskState.PresentingLine;
            _uiManager.ShowLine(_lineIndex, _speechText.Items.Length, "発話を検出しました。読み終わったら Enter を押してください。");
        }

        if (_state != PresentationTaskState.PresentingLine || !data.speech_detected) return;

        ScoringSample sample = ScoringSample.FromAnalysis(data, now);
        _lineSamples.Add(sample);

        if (now - _speechStartedAt < data.feature_window_seconds) return;
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

    private void BeginCalibration() {
        _state = PresentationTaskState.Calibration;
        _calibrationSpeechStartedAt = -1d;
        _uiManager.ShowCalibration(_scoringProfile.CalibrationText, "普段の声で読み始めてください。");
    }

    private void CompleteCalibration() {
        double now = Time.realtimeSinceStartupAsDouble;
        if (_calibrationSpeechStartedAt < 0d) {
            _uiManager.ShowCalibration(_scoringProfile.CalibrationText, "音声を検出できません。読み始めてください。");
            return;
        }
        if (_latestData?.analyzers?.delivery?.baseline_ready != true) {
            _uiManager.ShowCalibration(_scoringProfile.CalibrationText, "個人基準の作成中です。もう少し読み続けてください。");
            return;
        }

        float duration = (float)(now - _calibrationSpeechStartedAt);
        if (duration < _scoringProfile.MinimumSpeechSeconds) {
            _uiManager.ShowCalibration(_scoringProfile.CalibrationText, "校正時間が短すぎます。最初から読み直してください。");
            _calibrationSpeechStartedAt = -1d;
            return;
        }

        _baselineCpm = PerformanceEvaluator.CountCharacters(_scoringProfile.CalibrationText) / duration * 60f;
        _logWriter?.LogCalibration(_baselineCpm, duration);
        _lineIndex = 0;
        BeginCurrentLine(false);
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
            : "読み始めてください。完了後に Enter を押します。";
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
            _baselineCpm,
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
