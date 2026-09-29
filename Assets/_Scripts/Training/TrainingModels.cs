using System;
using System.Collections.Generic;

public enum PresentationTaskState {
    WaitingForSpeech,
    RecordingLine,
    Completed
}

[Serializable]
public sealed class ScoringSample {
    public int segmentIndex;
    public float segmentSeconds;
    public float arousal;
    public float dominance;
    public float speechRateValue;
    public float volumeValue;

    public static ScoringSample FromPacket(VoiceAnalysisPacket packet) {
        return new ScoringSample {
            segmentIndex = packet.segment_index,
            segmentSeconds = packet.segment_seconds,
            arousal = (float)packet.A,
            dominance = (float)packet.D,
            speechRateValue = (float)packet.speech_rate,
            volumeValue = (float)packet.loudness,
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
    public float meanDominance;
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
