using System;
using System.Collections.Generic;

public enum PresentationTaskState {
    WaitingForVoice,
    WaitingForSpeech,
    RecordingLine,
    Completed
}

[Serializable]
public sealed class ScoringSample {
    public double receivedAt;
    public bool speechDetected;
    public float featureWindowSeconds;
    public float rawArousalScore;
    public float rawDominanceScore;
    public float relativeVolumeScore;

    public static ScoringSample FromPacket(DeliveryPacket packet, double receivedAt) {
        return new ScoringSample {
            receivedAt = receivedAt,
            speechDetected = packet?.speech_detected ?? false,
            featureWindowSeconds = packet?.feature_window_seconds ?? 0f,
            rawArousalScore = packet != null ? (float)packet.raw_arousal_score : 0f,
            rawDominanceScore = packet != null ? (float)packet.raw_dominance_score : 0f,
            relativeVolumeScore = packet != null ? (float)packet.relative_volume_score : 0f,
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
