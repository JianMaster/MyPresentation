using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PerformanceEvaluator {
    private const string IgnoredCharacters = " \t\r\n、。，,.!?！？「」『』（）()[]{}:;：；…ー-—";

    public static LineEvaluationResult EvaluateLine(
        TextItem item,
        int targetRoleIndex,
        float referenceCpm,
        double speechStartedAt,
        double endedAt,
        bool gazeCompleted,
        IReadOnlyList<ScoringSample> samples,
        ScoringProfile profile
    ) {
        LineEvaluationResult result = CreateResult(item, targetRoleIndex, gazeCompleted);
        result.speechSeconds = Mathf.Max(0f, (float)(endedAt - speechStartedAt));

        if (result.speechSeconds < profile.MinimumSpeechSeconds) {
            result.invalidReason = "発話時間が短すぎます";
            return result;
        }

        List<ScoringSample> validSamples = samples
            .Where(sample => sample != null && sample.speechDetected && sample.baselineReady)
            .Where(sample => sample.receivedAt >= speechStartedAt)
            .Where(sample => sample.receivedAt - speechStartedAt >= Math.Max(0f, sample.featureWindowSeconds))
            .ToList();

        if (validSamples.Count == 0) {
            result.invalidReason = "この台詞に対応する有効な分析データがありません";
            return result;
        }

        ScoringSample newest = validSamples.OrderByDescending(sample => sample.receivedAt).First();
        if (endedAt - newest.receivedAt > profile.MaximumSampleAgeSeconds) {
            result.invalidReason = "分析データが古いため再試行してください";
            return result;
        }

        if (referenceCpm <= 0f) {
            result.invalidReason = "話速の基準値が設定されていません";
            return result;
        }

        result.validSampleCount = validSamples.Count;
        result.meanRawArousal = validSamples.Average(sample => sample.rawArousalScore);
        result.meanRawDominance = validSamples.Average(sample => sample.rawDominanceScore);
        result.meanRelativeVolume = validSamples.Average(sample => sample.relativeVolumeScore);
        result.actualCpm = CountCharacters(item.text) / result.speechSeconds * 60f;
        result.speedRatio = result.actualCpm / referenceCpm;
        result.deliveryScore = ScoreDelivery(
            item.deliveryStyle,
            result.meanRawArousal,
            result.meanRawDominance
        );
        result.speedScore = ScoreSpeed(item.speed, result.speedRatio, profile);
        result.volumeScore = ScoreVolume(item.volume, result.meanRelativeVolume, profile);
        result.gazeScore = gazeCompleted ? 100f : 0f;
        result.totalScore = WeightedAverage(
            result.deliveryScore,
            result.speedScore,
            result.volumeScore,
            result.gazeScore,
            profile
        );
        result.valid = true;
        result.invalidReason = string.Empty;
        return result;
    }

    public static SessionEvaluationResult EvaluateSession(
        IReadOnlyList<LineEvaluationResult> lineResults,
        ScoringProfile profile
    ) {
        List<LineEvaluationResult> valid = lineResults.Where(line => line != null && line.valid).ToList();
        SessionEvaluationResult result = new SessionEvaluationResult { lines = valid };
        if (valid.Count == 0) {
            result.advice = "有効な台詞評価がありません。マイクと分析システムを確認してください。";
            return result;
        }

        result.validLineCount = valid.Count;
        result.deliveryScore = valid.Average(line => line.deliveryScore);
        result.speedScore = valid.Average(line => line.speedScore);
        result.volumeScore = valid.Average(line => line.volumeScore);
        result.gazeScore = valid.Average(line => line.gazeScore);
        result.totalScore = WeightedAverage(
            result.deliveryScore,
            result.speedScore,
            result.volumeScore,
            result.gazeScore,
            profile
        );

        var dimensions = new[] {
            (Name: "語気", Score: result.deliveryScore),
            (Name: "話速", Score: result.speedScore),
            (Name: "音量", Score: result.volumeScore),
            (Name: "視線", Score: result.gazeScore),
        };
        result.strongestDimension = dimensions.OrderByDescending(item => item.Score).First().Name;
        result.weakestDimension = dimensions.OrderBy(item => item.Score).First().Name;
        result.advice = AdviceFor(result.weakestDimension);
        return result;
    }

    public static float ScoreDelivery(DeliveryStyle target, float arousal, float dominance) {
        float arousalSign = target == DeliveryStyle.EnergeticConfident || target == DeliveryStyle.EnergeticUnsteady ? 1f : -1f;
        float dominanceSign = target == DeliveryStyle.EnergeticConfident || target == DeliveryStyle.CalmConfident ? 1f : -1f;
        float arousalFit = Mathf.Clamp01(0.5f + 0.5f * arousalSign * Mathf.Clamp(arousal, -1f, 1f));
        float dominanceFit = Mathf.Clamp01(0.5f + 0.5f * dominanceSign * Mathf.Clamp(dominance, -1f, 1f));
        return (arousalFit + dominanceFit) * 50f;
    }

    public static float ScoreSpeed(Speed target, float ratio, ScoringProfile profile) {
        float center = target switch {
            Speed.Slow => profile.SlowSpeedRatio,
            Speed.Fast => profile.FastSpeedRatio,
            _ => profile.NormalSpeedRatio,
        };
        return ScoreAroundCenter(ratio, center, profile.SpeedFalloff);
    }

    public static float ScoreVolume(Volume target, float relativeVolume, ScoringProfile profile) {
        float center = target switch {
            Volume.Low => profile.LowVolumeCenter,
            Volume.High => profile.HighVolumeCenter,
            _ => profile.NormalVolumeCenter,
        };
        return ScoreAroundCenter(relativeVolume, center, profile.VolumeFalloff);
    }

    public static int CountCharacters(string text) {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Count(character => !IgnoredCharacters.Contains(character));
    }

    private static LineEvaluationResult CreateResult(TextItem item, int targetRoleIndex, bool gazeCompleted) {
        return new LineEvaluationResult {
            lineId = item.lineId,
            targetDeliveryStyle = EnumTool.GetDeliveryStyleWireValue(item.deliveryStyle),
            targetSpeed = item.speed.ToString().ToLowerInvariant(),
            targetVolume = item.volume.ToString().ToLowerInvariant(),
            targetRoleIndex = targetRoleIndex,
            gazeCompleted = gazeCompleted,
            invalidReason = "未評価",
        };
    }

    private static float ScoreAroundCenter(float value, float center, float falloff) {
        return 100f * Mathf.Clamp01(1f - Mathf.Abs(value - center) / Mathf.Max(0.01f, falloff));
    }

    private static float WeightedAverage(
        float delivery,
        float speed,
        float volume,
        float gaze,
        ScoringProfile profile
    ) {
        float totalWeight = profile.DeliveryWeight + profile.SpeedWeight + profile.VolumeWeight + profile.GazeWeight;
        if (totalWeight <= 0f) return 0f;
        return (
            delivery * profile.DeliveryWeight +
            speed * profile.SpeedWeight +
            volume * profile.VolumeWeight +
            gaze * profile.GazeWeight
        ) / totalWeight;
    }

    private static string AdviceFor(string dimension) {
        return dimension switch {
            "語気" => "表示された語気の方向を意識し、声の勢いと安定感をより明確に変えてみましょう。",
            "話速" => "基準話速との差を確認し、句読点で間を取りながら目標の速さを保ちましょう。",
            "音量" => "マイクとの距離を一定にし、表示された大小を意識して声量を調整しましょう。",
            _ => "視線対象を早めに確認し、台詞の途中で一度は相手の顔を見るようにしましょう。",
        };
    }
}
