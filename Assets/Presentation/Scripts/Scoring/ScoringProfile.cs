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


    }

    [CreateAssetMenu(menuName = "Presentation Rewrite/Scoring Profile")]
    public sealed class ScoringProfile : ScriptableObject {
        public ScoringSettings settings = new ScoringSettings();
    }
}
