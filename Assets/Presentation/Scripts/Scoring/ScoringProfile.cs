using System;
using UnityEngine;

namespace PresentationRewrite {
    [Serializable]
    public sealed class ScoringSettings {
        [Tooltip("X: 表达, Y: 语速, Z: 音量, W: 视线")]
        public Vector4 weights = new Vector4(0.25f, 0.25f, 0.25f, 0.25f);
        public Vector2 speedBand = new Vector2(2f, 5f);
        public Vector2 volumeBand = new Vector2(0.3f, 0.6f);
        [Min(0.001f)] public float speedFalloff = 3f;
        [Min(0.001f)] public float volumeFalloff = 0.3f;
        [Range(0f, 100f)] public float applauseThreshold = 80f;

        public bool IsValid => Finite(weights.x) && Finite(weights.y) && Finite(weights.z) && Finite(weights.w) &&
            weights.x >= 0 && weights.y >= 0 && weights.z >= 0 && weights.w >= 0 &&
            Finite(weights.x + weights.y + weights.z + weights.w) && weights.x + weights.y + weights.z + weights.w > 0 &&
            ValidBand(speedBand) && ValidBand(volumeBand) &&
            Finite(speedFalloff) && speedFalloff > 0 && Finite(volumeFalloff) && volumeFalloff > 0 &&
            applauseThreshold >= 0 && applauseThreshold <= 100;

        private static bool ValidBand(Vector2 band) => Finite(band.x) && Finite(band.y) && band.x >= 0 && band.y >= band.x;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [CreateAssetMenu(menuName = "Presentation Rewrite/Scoring Profile")]
    public sealed class ScoringProfile : ScriptableObject {
        public ScoringSettings settings = new ScoringSettings();
    }
}
