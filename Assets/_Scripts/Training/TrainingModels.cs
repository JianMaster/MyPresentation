using System;
using System.Collections.Generic;

public enum PresentationTaskState {
    WaitingForAnalyzer,
    Calibration,
    AwaitingSpeech,
    PresentingLine,
    Completed
}

[Serializable]
public sealed class ScoringSample {
    public long sequenceId;
    public double sourceTimestamp;
    public double receivedAt;
    public bool speechDetected;
    public bool baselineReady;
    public float featureWindowSeconds;
    public float rawArousalScore;
    public float rawDominanceScore;
    public float relativeVolumeScore;

    public static ScoringSample FromAnalysis(AnalysisData data, double receivedAt) {
        DeliveryAnalysis delivery = data?.analyzers?.delivery;
        return new ScoringSample {
            sequenceId = data?.sequence_id ?? 0,
            sourceTimestamp = data?.timestamp ?? 0d,
            receivedAt = receivedAt,
            speechDetected = data?.speech_detected ?? false,
            baselineReady = delivery?.baseline_ready ?? false,
            featureWindowSeconds = data != null ? data.feature_window_seconds : 0f,
            rawArousalScore = delivery != null ? (float)delivery.raw_arousal_score : 0f,
            rawDominanceScore = delivery != null ? (float)delivery.raw_dominance_score : 0f,
            relativeVolumeScore = delivery != null ? (float)delivery.relative_volume_score : 0f,
        };
    }
}

[Serializable]
public sealed class LineEvaluationResult {
    public string lineId;
    public bool valid;
    public string invalidReason;
    public string targetDeliveryStyle;
    public string targetSpeed;
    public string targetVolume;
    public int targetRoleIndex;
    public bool gazeCompleted;
    public int validSampleCount;
    public float speechSeconds;
    public float actualCpm;
    public float speedRatio;
    public float meanRawArousal;
    public float meanRawDominance;
    public float meanRelativeVolume;
    public float deliveryScore;
    public float speedScore;
    public float volumeScore;
    public float gazeScore;
    public float totalScore;
}

[Serializable]
public sealed class SessionEvaluationResult {
    public int validLineCount;
    public float deliveryScore;
    public float speedScore;
    public float volumeScore;
    public float gazeScore;
    public float totalScore;
    public string strongestDimension;
    public string weakestDimension;
    public string advice;
    public List<LineEvaluationResult> lines = new List<LineEvaluationResult>();
}
