using System;
using UnityEngine;

namespace PresentationRewrite {
    public enum Delivery { CalmConfident, EnergeticConfident, EnergeticHesitant, CalmHesitant }
    public enum Level { Low, Normal, High }

    [Serializable]
    public sealed class SpeechLine {
        public string lineId;
        [TextArea(2, 5)] public string text;
        public Delivery deliveryStyle;
        public Level speed = Level.Normal;
        public Level volume = Level.Normal;
        public int targetRoleIndex = -1;
        public string[] emphasis = Array.Empty<string>();
        public string[] pause_after = Array.Empty<string>();

        public string FormattedText() {
            // ponytail: literal annotations match the authored script; use text spans if overlapping marks are needed.
            string body = text.Replace(",", "").Replace("、", "").Replace("，", "");
            foreach (string pause in pause_after) {
                body = body.Replace(pause, pause + "/");
            }
            foreach (string word in emphasis) {
                body = body.Replace(word, $"<color=#FFD54A>{word}</color>");
            }
            return body;
        }
    }

    [CreateAssetMenu(menuName = "Presentation Rewrite/Speech")]
    public sealed class SpeechText : ScriptableObject {
        public SpeechLine[] lines = Array.Empty<SpeechLine>();
    }
}
