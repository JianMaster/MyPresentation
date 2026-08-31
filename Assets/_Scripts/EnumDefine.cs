public enum DeliveryStyle {
    CalmPositive,
    EnergeticPositive,
    EnergeticNegative,
    CalmNegative
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
            DeliveryStyle.EnergeticPositive => "活力・前向き",
            DeliveryStyle.EnergeticNegative => "活力・ネガティブ",
            DeliveryStyle.CalmNegative => "落ち着き・ネガティブ",
            _ => "落ち着き・前向き",
        };
    }

    public static string GetDeliveryStyleWireValue(DeliveryStyle style) {
        return style switch {
            DeliveryStyle.EnergeticPositive => "energetic_positive",
            DeliveryStyle.EnergeticNegative => "energetic_negative",
            DeliveryStyle.CalmNegative => "calm_negative",
            _ => "calm_positive",
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
