using UnityEngine;

namespace PresentationRewrite {
    public static class Scoring {
        // Result components: delivery, speed, volume, gaze.
        public static Vector4 Evaluate(SpeechLine line, Vector4 voice, bool looked, ScoringSettings settings) {
            float a = line.deliveryStyle == Delivery.EnergeticConfident || line.deliveryStyle == Delivery.EnergeticHesitant ? 1f : -1f;
            float d = line.deliveryStyle == Delivery.CalmConfident || line.deliveryStyle == Delivery.EnergeticConfident ? 1f : -1f;
            float delivery = 50f + 25f * (a * voice.x + d * voice.y);
            return new Vector4(delivery, Band(line.speed, voice.z, settings.speedBand.x, settings.speedBand.y, settings.speedFalloff),
                Band(line.volume, voice.w, settings.volumeBand.x, settings.volumeBand.y, settings.volumeFalloff), looked ? 100f : 0f);
        }

        public static float Total(Vector4 scores, ScoringSettings settings) {
            var w = settings.weights;
            return Vector4.Dot(scores, w) / (w.x + w.y + w.z + w.w);
        }

        public static string Advice(Vector4 scores, ScoringSettings settings) {
            int weakest = -1;
            for (int i = 0; i < 4; i++) {
                if (settings.weights[i] > 0 && (weakest < 0 || scores[i] < scores[weakest])) weakest = i;
            }
            return weakest switch {
                0 => "優先改善：音声表現。声の勢いと自信の表現を、台詞の目標に合わせましょう。",
                1 => "優先改善：話速。句読点で間を取り、目標の速さを保ちましょう。",
                2 => "優先改善：音量。マイクとの距離を一定にし、目標の声量に合わせましょう。",
                _ => "優先改善：視線。光っている相手を、台詞の途中で一度は見ましょう。",
            };
        }

        private static float Band(Level target, float value, float min, float max, float falloff) {
            float distance = target == Level.Low ? Mathf.Max(0f, value - min)
                : target == Level.High ? Mathf.Max(0f, max - value)
                : Mathf.Max(0f, min - value, value - max);
            return 100f * Mathf.Clamp01(1f - distance / falloff);
        }
    }
}
