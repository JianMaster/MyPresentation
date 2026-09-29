using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public sealed class SessionLogWriter : IDisposable {
    private readonly ScoringProfile _profile;
    private StreamWriter _writer;

    public SessionLogWriter(string participantId, ScoringProfile profile) {
        string normalizedParticipantId = string.IsNullOrWhiteSpace(participantId) ? "anonymous" : participantId.Trim();
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
                participant_id = normalizedParticipantId,
                phase = _profile.Phase,
                condition = _profile.Condition,
                script_version = _profile.ScriptVersion,
                algorithm_version = _profile.AlgorithmVersion,
                udp_contract = "voicead-segments-v1",
                speech_rate_medium_band = new[] { _profile.SpeechRateMediumMin, _profile.SpeechRateMediumMax },
                volume_medium_band = new[] { _profile.VolumeMediumMin, _profile.VolumeMediumMax },
            });
        }
        catch (Exception exception) {
            Debug.LogWarning($"Unable to create session log: {exception.Message}");
        }
    }

    public string SessionId { get; }
    public string FilePath { get; private set; } = string.Empty;

    public void LogVoiceAnalysisSample(VoiceAnalysisPacket packet, PresentationTaskState state, string lineId) {
        if (packet == null) return;

        Append(new {
            event_type = "voice_analysis_sample",
            recorded_at = UtcNowSeconds(),
            session_id = SessionId,
            task_state = state.ToString(),
            line_id = lineId ?? string.Empty,
            utterance_started_at = packet.utterance_started_at,
            segment_index = packet.segment_index,
            segment_seconds = packet.segment_seconds,
            speech_ended = packet.speech_ended,
            voice_analysis = new {
                arousal = packet.A,
                dominance = packet.D,
                packet.speech_rate,
                packet.speech_rate_level,
                packet.loudness,
                packet.loudness_level,
            },
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
