public enum Emotion {
    Neutral,
    Warm,
    Serious,
    Confident,
    Encouraging,
    Grateful
}

public enum Speed {
    Slow,
    Normal,
    Fast
}

public enum Volume {
    Low,
    Normal,
    High
}

public static class EnumTool {
    public static string GetEmotionText(Emotion emotion) {
        return emotion switch {
            Emotion.Warm => "丁寧",
            Emotion.Serious => "厳粛",
            Emotion.Confident => "確信",
            Emotion.Encouraging => "前向き",
            Emotion.Grateful => "感謝",
            _ => "平静",
        };
    }

    public static string GetSpeedText(Speed speed) {
        return speed switch {
            Speed.Slow => "遅め",
            Speed.Fast => "速め",
            _ => "普通",
        };
    }

    public static string GetVolumeText(Volume volume) {
        return volume switch {
            Volume.Low => "小さめ",
            Volume.High => "大きめ",
            _ => "普通",
        };
    }
}
