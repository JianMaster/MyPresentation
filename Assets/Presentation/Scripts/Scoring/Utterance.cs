using System.Collections.Generic;
using UnityEngine;

namespace PresentationRewrite {
    public sealed class Utterance {
        private readonly HashSet<int> _segments = new HashSet<int>();
        private double _startedAt;
        private Vector4 _sum;
        private float _seconds;

        // Components: arousal, dominance, speech rate, loudness.
        public double StartedAt => _startedAt;
        public Vector4 Mean => _sum / _seconds;

        public bool Add(VoicePacket packet) {
            if (packet.utterance_started_at < _startedAt) return false;
            if (packet.utterance_started_at > _startedAt) {
                _startedAt = packet.utterance_started_at;
                _segments.Clear();
                _sum = Vector4.zero;
                _seconds = 0;
            }
            if (_segments.Add(packet.segment_index)) {
                _sum += new Vector4(packet.A, packet.D, packet.speech_rate, packet.loudness) * packet.segment_seconds;
                _seconds += packet.segment_seconds;
            }
            // A repeated final segment still carries the end-of-speech signal.
            return packet.speech_ended;
        }
    }
}
