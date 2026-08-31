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
    public float arousal;
    public float valence;
    public float speechRateValue;
    public float volumeValue;

    public static ScoringSample FromPacket(VoiceAnalysisPacket packet, double receivedAt) {
        return new ScoringSample {
            receivedAt = receivedAt,
            speechDetected = packet?.speech_detected ?? false,
            featureWindowSeconds = packet?.feature_window_seconds ?? 0f,
            arousal = packet != null ? (float)packet.arousal : 0f,
            valence = packet != null ? (float)packet.valence : 0f,
            speechRateValue = packet != null ? (float)packet.speech_rate_value : 0f,
            volumeValue = packet != null ? (float)packet.volume_value : 0f,
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
    public float meanArousal;
    public float meanValence;
    public float meanSpeechRateValue;
    public float meanVolumeValue;
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
