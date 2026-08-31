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

        LineEvaluationResult result = PerformanceEvaluator.EvaluateLine(
            item,
            0,
            0d,
            duration,
            true,
            CreateSamples(4d, 1f, 1f, 3.7f, 0.32f),
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

        LineEvaluationResult result = PerformanceEvaluator.EvaluateLine(
            item,
            0,
            0d,
            duration,
            false,
            CreateSamples(4d, 1f, 1f, 3.7f, 0.32f),
            _profile
        );

        Assert.That(result.totalScore, Is.EqualTo(75f).Within(0.001f));
    }

    [Test]
    public void OppositeArousalValenceDirectionScoresZero() {
        Assert.That(
            PerformanceEvaluator.ScoreDelivery(DeliveryStyle.EnergeticPositive, -1f, -1f),
            Is.EqualTo(0f)
        );
    }

    [Test]
    public void StaleOrShortAttemptsAreInvalidInsteadOfZeroScored() {
        TextItem item = CreateItem();
        List<ScoringSample> staleSamples = CreateSamples(4d, 1f, 1f, 3.7f, 0.32f);

        LineEvaluationResult stale = PerformanceEvaluator.EvaluateLine(
            item, 0, 0d, 7d, true, staleSamples, _profile
        );
        LineEvaluationResult shortAttempt = PerformanceEvaluator.EvaluateLine(
            item, 0, 0d, 3d, true, staleSamples, _profile
        );

        Assert.That(stale.valid, Is.False);
        Assert.That(shortAttempt.valid, Is.False);
    }

    [Test]
    public void ArousalValenceUdpContractDeserializesTrainingFields() {
        const string json = "{\"timestamp\":123.5,\"sequence_id\":7,\"speech_detected\":true," +
                            "\"feature_window_seconds\":2,\"arousal\":0.4,\"valence\":-0.2," +
                            "\"speech_rate_value\":3.7,\"speech_rate_level\":\"medium\"," +
                            "\"volume_value\":0.32,\"volume_level\":\"medium\"}";

        bool parsed = AnalysisUdpReceiver.TryParseAnalysisPacket(
            json,
            out VoiceAnalysisPacket packet,
            out string error
        );

        Assert.That(parsed, Is.True, error);
        Assert.That(packet.sequence_id, Is.EqualTo(7));
        Assert.That(packet.speech_detected, Is.True);
        Assert.That(packet.arousal, Is.EqualTo(0.4d).Within(0.0001d));
        Assert.That(packet.valence, Is.EqualTo(-0.2d).Within(0.0001d));
        Assert.That(packet.speech_rate_value, Is.EqualTo(3.7d).Within(0.0001d));
        Assert.That(packet.volume_value, Is.EqualTo(0.32d).Within(0.0001d));
    }

    [Test]
    public void NoSpeechFrameAllowsNullEmotionScores() {
        const string json = "{\"timestamp\":123.5,\"sequence_id\":7,\"speech_detected\":false," +
                            "\"feature_window_seconds\":2,\"arousal\":null,\"valence\":null," +
                            "\"speech_rate_value\":0.0,\"speech_rate_level\":\"low\"," +
                            "\"volume_value\":0.01,\"volume_level\":\"low\"}";

        bool parsed = AnalysisUdpReceiver.TryParseAnalysisPacket(
            json,
            out VoiceAnalysisPacket packet,
            out string error
        );

        Assert.That(parsed, Is.True, error);
        Assert.That(packet.speech_detected, Is.False);
        Assert.That(packet.arousal, Is.Zero);
        Assert.That(packet.valence, Is.Zero);
    }

    [Test]
    public void LegacyDeliveryRootIsRejected() {
        const string json = "{\"timestamp\":123.5,\"sequence_id\":7,\"speech_detected\":true," +
                            "\"feature_window_seconds\":4,\"baseline_ready\":true," +
                            "\"raw_arousal_score\":0.4,\"raw_dominance_score\":0.2," +
                            "\"delivery_style\":\"calm_confident\"}";

        bool parsed = AnalysisUdpReceiver.TryParseAnalysisPacket(
            json,
            out VoiceAnalysisPacket packet,
            out string error
        );

        Assert.That(parsed, Is.False);
        Assert.That(packet, Is.Null);
        Assert.That(error, Does.Contain("Arousal/Valence"));
    }

    [Test]
    public void LineEvaluationUsesVoiceModelValuesWithoutCalibrationState() {
        TextItem item = CreateItem();
        float duration = 5f;
        VoiceAnalysisPacket packet = new VoiceAnalysisPacket {
            timestamp = 100d,
            sequence_id = 1,
            speech_detected = true,
            feature_window_seconds = 2f,
            arousal = 1d,
            valence = 1d,
            speech_rate_value = 3.7d,
            speech_rate_level = "medium",
            volume_value = 0.32d,
            volume_level = "medium",
        };

        LineEvaluationResult result = PerformanceEvaluator.EvaluateLine(
            item,
            0,
            0d,
            duration,
            true,
            new List<ScoringSample> { ScoringSample.FromPacket(packet, 4d) },
            _profile
        );

        Assert.That(result.valid, Is.True);
    }

    private static TextItem CreateItem() {
        return new TextItem {
            lineId = "test-line",
            text = "これは評価用の台詞です",
            deliveryStyle = DeliveryStyle.EnergeticPositive,
            speed = Speed.Normal,
            volume = Volume.Normal,
        };
    }

    private static List<ScoringSample> CreateSamples(
        double receivedAt,
        float arousal,
        float valence,
        float speechRate,
        float volume
    ) {
        return new List<ScoringSample> {
            new ScoringSample {
                receivedAt = receivedAt,
                speechDetected = true,
                featureWindowSeconds = 2f,
                arousal = arousal,
                valence = valence,
                speechRateValue = speechRate,
                volumeValue = volume,
            },
        };
    }
}
