using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public sealed class SessionLogWriter : IDisposable {
    private readonly string _participantId;
    private readonly ScoringProfile _profile;
    private StreamWriter _writer;

    public SessionLogWriter(string participantId, ScoringProfile profile) {
        _participantId = string.IsNullOrWhiteSpace(participantId) ? "anonymous" : participantId.Trim();
        _profile = profile;
        SessionId = $"{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        try {
            string directory = Path.Combine(Application.persistentDataPath, "Sessions");
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, $"{SessionId}.jsonl");
            _writer = new StreamWriter(FilePath, false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
            Append(new {
                event_type = "session_start",
                recorded_at = UtcNowSeconds(),
                session_id = SessionId,
                participant_id = _participantId,
                phase = _profile.Phase,
                condition = _profile.Condition,
                script_version = _profile.ScriptVersion,
                algorithm_version = _profile.AlgorithmVersion,
            });
        }
        catch (Exception exception) {
            Debug.LogWarning($"Unable to create session log: {exception.Message}");
        }
    }

    public string SessionId { get; }
    public string FilePath { get; private set; } = string.Empty;

    public void LogAnalysisSample(AnalysisData data, PresentationTaskState state, string lineId) {
        if (data == null) return;
        DeliveryAnalysis delivery = data.analyzers?.delivery;
        ProsodyAnalysis prosody = data.analyzers?.prosody;
        PronunciationAnalysis pronunciation = data.analyzers?.pronunciation;
        AsrAnalysis asr = data.analyzers?.asr;

        Append(new {
            event_type = "analysis_sample",
            recorded_at = UtcNowSeconds(),
            session_id = SessionId,
            task_state = state.ToString(),
            line_id = lineId ?? string.Empty,
            source_timestamp = data.timestamp,
            sequence_id = data.sequence_id,
            speech_detected = data.speech_detected,
            delivery = delivery == null ? null : new {
                delivery.baseline_ready,
                delivery.arousal_score,
                delivery.raw_arousal_score,
                delivery.dominance_score,
                delivery.raw_dominance_score,
                delivery.delivery_style,
                delivery.relative_volume_score,
            },
            prosody = prosody == null ? null : new {
                prosody.mean_pitch_st,
                prosody.pitch_range_st,
                prosody.pitch_variation,
                prosody.mean_loudness,
                prosody.loudness_range,
                prosody.loudness_variation,
                prosody.sound_level_db,
            },
            pronunciation = pronunciation == null ? null : new {
                pronunciation.hnr_db,
                pronunciation.jitter,
                pronunciation.shimmer_db,
                pronunciation.spectral_flux,
            },
            asr_metrics = asr == null ? null : new {
                asr.status,
                asr.speech_rate_cpm,
                asr.pause_count,
                asr.speaking_ratio,
            },
        });
    }

    public void LogCalibration(float baselineCpm, float durationSeconds) {
        Append(new {
            event_type = "calibration_completed",
            recorded_at = UtcNowSeconds(),
            session_id = SessionId,
            baseline_cpm = baselineCpm,
            duration_seconds = durationSeconds,
        });
    }

    public void LogLineStart(TextItem item, int lineIndex, int targetRoleIndex, string targetRoleId) {
        Append(new {
            event_type = "line_start",
            recorded_at = UtcNowSeconds(),
            session_id = SessionId,
            line_id = item?.lineId ?? string.Empty,
            line_index = lineIndex,
            target_delivery_style = item != null ? EnumTool.GetDeliveryStyleWireValue(item.deliveryStyle) : string.Empty,
            target_speed = item?.speed.ToString().ToLowerInvariant(),
            target_volume = item?.volume.ToString().ToLowerInvariant(),
            target_role_index = targetRoleIndex,
            target_role_id = targetRoleId ?? string.Empty,
        });
    }

    public void LogGazeCompleted(string lineId, int targetRoleIndex, string targetRoleId) {
        Append(new {
            event_type = "gaze_completed",
            recorded_at = UtcNowSeconds(),
            session_id = SessionId,
            line_id = lineId,
            target_role_index = targetRoleIndex,
            target_role_id = targetRoleId,
        });
    }

    public void LogLineResult(LineEvaluationResult result) {
        Append(new {
            event_type = "line_result",
            recorded_at = UtcNowSeconds(),
            session_id = SessionId,
            result,
        });
    }

    public void LogSessionResult(SessionEvaluationResult result) {
        Append(new {
            event_type = "session_result",
            recorded_at = UtcNowSeconds(),
            session_id = SessionId,
            phase = _profile.Phase,
            condition = _profile.Condition,
            result,
        });
    }

    public void Dispose() {
        _writer?.Dispose();
        _writer = null;
    }

    private void Append(object record) {
        if (_writer == null) return;
        try {
            _writer.WriteLine(JsonConvert.SerializeObject(record, Formatting.None));
        }
        catch (Exception exception) {
            Debug.LogWarning($"Unable to append session log: {exception.Message}");
        }
    }

    private static double UtcNowSeconds() {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
    }
}
