using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PerformanceEvaluatorTests {
    private ScoringProfile _profile;

    [SetUp]
    public void SetUp() {
        _profile = ScriptableObject.CreateInstance<ScoringProfile>();
    }

    [TearDown]
    public void TearDown() {
        Object.DestroyImmediate(_profile);
    }

    [Test]
    public void PerfectLineScoresOneHundred() {
        TextItem item = CreateItem();
        float duration = 5f;
        float actualCpm = PerformanceEvaluator.CountCharacters(item.text) / duration * 60f;

        LineEvaluationResult result = PerformanceEvaluator.EvaluateLine(
            item,
            0,
            actualCpm,
            0d,
            duration,
            true,
            CreateSamples(4d, 1f, 1f, 0f),
            _profile
        );

        Assert.That(result.valid, Is.True);
        Assert.That(result.deliveryScore, Is.EqualTo(100f).Within(0.001f));
        Assert.That(result.speedScore, Is.EqualTo(100f).Within(0.001f));
        Assert.That(result.volumeScore, Is.EqualTo(100f).Within(0.001f));
        Assert.That(result.gazeScore, Is.EqualTo(100f));
        Assert.That(result.totalScore, Is.EqualTo(100f).Within(0.001f));
    }

    [Test]
    public void MissingGazeReducesEqualWeightedTotalByTwentyFive() {
        TextItem item = CreateItem();
        float duration = 5f;
        float actualCpm = PerformanceEvaluator.CountCharacters(item.text) / duration * 60f;

        LineEvaluationResult result = PerformanceEvaluator.EvaluateLine(
            item,
            0,
            actualCpm,
            0d,
            duration,
            false,
            CreateSamples(4d, 1f, 1f, 0f),
            _profile
        );

        Assert.That(result.totalScore, Is.EqualTo(75f).Within(0.001f));
    }

    [Test]
    public void OppositeDeliveryDirectionScoresZero() {
        Assert.That(
            PerformanceEvaluator.ScoreDelivery(DeliveryStyle.EnergeticConfident, -1f, -1f),
            Is.EqualTo(0f)
        );
    }

    [Test]
    public void StaleOrShortAttemptsAreInvalidInsteadOfZeroScored() {
        TextItem item = CreateItem();
        List<ScoringSample> staleSamples = CreateSamples(4d, 1f, 1f, 0f);

        LineEvaluationResult stale = PerformanceEvaluator.EvaluateLine(
            item, 0, 200f, 0d, 7d, true, staleSamples, _profile
        );
        LineEvaluationResult shortAttempt = PerformanceEvaluator.EvaluateLine(
            item, 0, 200f, 0d, 3d, true, staleSamples, _profile
        );

        Assert.That(stale.valid, Is.False);
        Assert.That(shortAttempt.valid, Is.False);
    }

    [Test]
    public void DeliveryRootUdpContractDeserializesTrainingFields() {
        const string json = "{\"timestamp\":123.5,\"sequence_id\":7,\"speech_detected\":true," +
                            "\"feature_window_seconds\":4,\"baseline_ready\":true," +
                            "\"arousal_score\":0.2,\"arousal_level\":\"low\"," +
                            "\"raw_arousal_score\":0.4,\"raw_dominance_score\":-0.2," +
                            "\"dominance_score\":-0.1,\"dominance_level\":\"low\"," +
                            "\"delivery_style\":\"subdued_hesitant\"," +
                            "\"relative_volume_score\":0.3}";

        bool parsed = AnalysisUdpReceiver.TryParseDeliveryPacket(
            json,
            out DeliveryPacket packet,
            out string error
        );

        Assert.That(parsed, Is.True, error);
        Assert.That(packet.sequence_id, Is.EqualTo(7));
        Assert.That(packet.speech_detected, Is.True);
        Assert.That(packet.baseline_ready, Is.True);
        Assert.That(packet.raw_arousal_score, Is.EqualTo(0.4d).Within(0.0001d));
        Assert.That(packet.relative_volume_score, Is.EqualTo(0.3d).Within(0.0001d));
    }

    [Test]
    public void LegacyAnalyzerEnvelopeIsRejected() {
        const string json = "{\"timestamp\":123.5,\"sequence_id\":7,\"speech_detected\":true," +
                            "\"feature_window_seconds\":4,\"enabled\":{\"delivery\":true}," +
                            "\"analyzers\":{\"delivery\":{\"baseline_ready\":true," +
                            "\"delivery_style\":\"calm_confident\"}}}";

        bool parsed = AnalysisUdpReceiver.TryParseDeliveryPacket(
            json,
            out DeliveryPacket packet,
            out string error
        );

        Assert.That(parsed, Is.False);
        Assert.That(packet, Is.Null);
        Assert.That(error, Does.Contain("delivery-only"));
    }

    private static TextItem CreateItem() {
        return new TextItem {
            lineId = "test-line",
            text = "これは評価用の台詞です",
            deliveryStyle = DeliveryStyle.EnergeticConfident,
            speed = Speed.Normal,
            volume = Volume.Normal,
        };
    }

    private static List<ScoringSample> CreateSamples(double receivedAt, float arousal, float dominance, float volume) {
        return new List<ScoringSample> {
            new ScoringSample {
                sequenceId = 1,
                sourceTimestamp = 100d,
                receivedAt = receivedAt,
                speechDetected = true,
                baselineReady = true,
                featureWindowSeconds = 4f,
                rawArousalScore = arousal,
                rawDominanceScore = dominance,
                relativeVolumeScore = volume,
            },
        };
    }
}
