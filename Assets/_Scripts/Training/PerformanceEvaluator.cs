using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PerformanceEvaluator {
    public static LineEvaluationResult EvaluateLine(
        TextItem item,
        int targetRoleIndex,
        bool gazeCompleted,
        IReadOnlyList<ScoringSample> samples,
        ScoringProfile profile
    ) {
        LineEvaluationResult result = CreateResult(item, targetRoleIndex, gazeCompleted);
        List<ScoringSample> validSamples = samples
            .Where(sample => sample != null && sample.segmentSeconds > 0f)
            .GroupBy(sample => sample.segmentIndex)
            .Select(group => group.First())
            .ToList();

        if (validSamples.Count == 0) {
            result.invalidReason = "この台詞に対応する有効な分析データがありません";
            return result;
        }

        result.speechSeconds = validSamples.Sum(sample => sample.segmentSeconds);
        result.validSampleCount = validSamples.Count;
        // Weight by analyzed duration so a short tail does not outweigh a full segment.
        result.meanArousal = validSamples.Sum(sample => sample.arousal * sample.segmentSeconds) / result.speechSeconds;
        result.meanDominance = validSamples.Sum(sample => sample.dominance * sample.segmentSeconds) / result.speechSeconds;
        result.meanSpeechRateValue = validSamples.Sum(sample => sample.speechRateValue * sample.segmentSeconds) / result.speechSeconds;
        result.meanVolumeValue = validSamples.Sum(sample => sample.volumeValue * sample.segmentSeconds) / result.speechSeconds;
        result.deliveryScore = ScoreDelivery(
            item.deliveryStyle,
            result.meanArousal,
            result.meanDominance
        );
        result.speedScore = ScoreSpeed(item.speed, result.meanSpeechRateValue, profile);
        result.volumeScore = ScoreVolume(item.volume, result.meanVolumeValue, profile);
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
            (Name: "音声表現", Score: result.deliveryScore),
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
        float arousalSign = target == DeliveryStyle.EnergeticConfident || target == DeliveryStyle.EnergeticHesitant ? 1f : -1f;
        float dominanceSign = target == DeliveryStyle.EnergeticConfident || target == DeliveryStyle.CalmConfident ? 1f : -1f;
        float arousalFit = Mathf.Clamp01(0.5f + 0.5f * arousalSign * Mathf.Clamp(arousal, -1f, 1f));
        float dominanceFit = Mathf.Clamp01(0.5f + 0.5f * dominanceSign * Mathf.Clamp(dominance, -1f, 1f));
        return (arousalFit + dominanceFit) * 50f;
    }

    public static float ScoreSpeed(Speed target, float speechRateValue, ScoringProfile profile) {
        return ScoreTargetBand(
            target == Speed.Slow,
            target == Speed.Fast,
            speechRateValue,
            profile.SpeechRateMediumMin,
            profile.SpeechRateMediumMax,
            profile.SpeechRateFalloff
        );
    }

    public static float ScoreVolume(Volume target, float volumeValue, ScoringProfile profile) {
        return ScoreTargetBand(
            target == Volume.Low,
            target == Volume.High,
            volumeValue,
            profile.VolumeMediumMin,
            profile.VolumeMediumMax,
            profile.VolumeFalloff
        );
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

    private static float ScoreTargetBand(
        bool targetLow,
        bool targetHigh,
        float value,
        float mediumMin,
        float mediumMax,
        float falloff
    ) {
        float distance = targetLow
            ? Mathf.Max(0f, value - mediumMin)
            : targetHigh
                ? Mathf.Max(0f, mediumMax - value)
                : value < mediumMin
                    ? mediumMin - value
                    : Mathf.Max(0f, value - mediumMax);
        return 100f * Mathf.Clamp01(1f - distance / Mathf.Max(0.001f, falloff));
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
            "音声表現" => "表示された方向を意識し、声の勢いと自信の表現をより明確に変えてみましょう。",
            "話速" => "基準話速との差を確認し、句読点で間を取りながら目標の速さを保ちましょう。",
            "音量" => "マイクとの距離を一定にし、表示された大小を意識して声量を調整しましょう。",
            _ => "視線対象を早めに確認し、台詞の途中で一度は相手の顔を見るようにしましょう。",
        };
    }
}
