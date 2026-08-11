public enum DeliveryStyle {
    CalmConfident,
    EnergeticConfident,
    EnergeticUnsteady,
    SubduedHesitant
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
    public static string GetDeliveryStyleText(DeliveryStyle style) {
        return style switch {
            DeliveryStyle.EnergeticConfident => "活力・自信",
            DeliveryStyle.EnergeticUnsteady => "活力・不安定",
            DeliveryStyle.SubduedHesitant => "控えめ・ためらい",
            _ => "冷静・自信",
        };
    }

    public static string GetDeliveryStyleWireValue(DeliveryStyle style) {
        return style switch {
            DeliveryStyle.EnergeticConfident => "energetic_confident",
            DeliveryStyle.EnergeticUnsteady => "energetic_unsteady",
            DeliveryStyle.SubduedHesitant => "subdued_hesitant",
            _ => "calm_confident",
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
