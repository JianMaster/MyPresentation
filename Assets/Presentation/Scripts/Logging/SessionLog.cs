using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace PresentationRewrite {
    public sealed class SessionLog {
        public string FilePath { get; }

        public SessionLog(string directory, ScoringSettings settings) {
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMddTHHmmss}-{Guid.NewGuid():N}.jsonl");
            Write(new {
                type = "start", utc = DateTime.UtcNow,
                weights = Components(settings.weights),
                speed_band = new[] { settings.speedBand.x, settings.speedBand.y },
                volume_band = new[] { settings.volumeBand.x, settings.volumeBand.y },
                settings.speedFalloff, settings.volumeFalloff, settings.applauseThreshold
            });
        }

        public void Line(int index, SpeechLine line, Vector4 voice, Vector4 scores, float total) {
            Write(new {
                type = "line", utc = DateTime.UtcNow, index, line.lineId,
                delivery = line.deliveryStyle.ToString(), speed = line.speed.ToString(), volume = line.volume.ToString(),
                mean = new { arousal = voice.x, dominance = voice.y, speech_rate = voice.z, loudness = voice.w },
                scores = new[] { scores.x, scores.y, scores.z }, total
            });
        }

        public void Finish(bool completed, int lineCount, Vector4 scores, float total) {
            Write(new { type = "finish", utc = DateTime.UtcNow, completed, lineCount, scores = Components(scores), total });
        }

        private void Write(object record) {
            File.AppendAllText(FilePath, JsonConvert.SerializeObject(record) + Environment.NewLine);
        }

        private static float[] Components(Vector4 value) => new[] { value.x, value.y, value.z, value.w };
    }
}
