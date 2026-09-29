public enum DeliveryStyle {
    CalmConfident,
    EnergeticConfident,
    EnergeticHesitant,
    CalmHesitant
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
            DeliveryStyle.EnergeticHesitant => "活力・ためらい",
            DeliveryStyle.CalmHesitant => "落ち着き・ためらい",
            _ => "落ち着き・自信",
        };
    }

    public static string GetDeliveryStyleWireValue(DeliveryStyle style) {
        return style switch {
            DeliveryStyle.EnergeticConfident => "energetic_confident",
            DeliveryStyle.EnergeticHesitant => "energetic_hesitant",
            DeliveryStyle.CalmHesitant => "calm_hesitant",
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
