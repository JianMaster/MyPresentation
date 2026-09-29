using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PerformanceEvaluatorTests {
    private ScoringProfile _profile;
    [SetUp] public void SetUp() => _profile = ScriptableObject.CreateInstance<ScoringProfile>();
    [TearDown] public void TearDown() => Object.DestroyImmediate(_profile);

    private static TextItem Item() => new TextItem {
        lineId = "test", deliveryStyle = DeliveryStyle.EnergeticConfident,
        speed = Speed.Normal, volume = Volume.Normal
    };
    private static ScoringSample Sample(int index = 1, float seconds = 0.6f, float a = 1f, float d = 1f) => new ScoringSample {
        segmentIndex = index, segmentSeconds = seconds, arousal = a, dominance = d,
        speechRateValue = 3.5f, volumeValue = 0.45f
    };
    private LineEvaluationResult Evaluate(bool gaze, params ScoringSample[] samples) =>
        PerformanceEvaluator.EvaluateLine(Item(), 0, gaze, samples, _profile);

    [Test] public void ShortVoiceAdUtteranceScoresWithoutWarmupOrFourSecondMinimum() {
        var result = Evaluate(true, Sample());
        Assert.That(result.valid, Is.True);
        Assert.That(result.totalScore, Is.EqualTo(100f).Within(0.001f));
        Assert.That(result.speechSeconds, Is.EqualTo(0.6f));
    }
    [Test] public void MissingGazeReducesTotalByTwentyFive() {
        Assert.That(Evaluate(false, Sample()).totalScore, Is.EqualTo(75f).Within(0.001f));
    }
    [TestCase(DeliveryStyle.EnergeticConfident, 1f, 1f)]
    [TestCase(DeliveryStyle.CalmConfident, -1f, 1f)]
    [TestCase(DeliveryStyle.EnergeticHesitant, 1f, -1f)]
    [TestCase(DeliveryStyle.CalmHesitant, -1f, -1f)]
    public void AdDirectionsMatchTargets(DeliveryStyle target, float a, float d) {
        Assert.That(PerformanceEvaluator.ScoreDelivery(target, a, d), Is.EqualTo(100f));
        Assert.That(PerformanceEvaluator.ScoreDelivery(target, -a, -d), Is.Zero);
    }
    [Test] public void EmptyUtteranceIsInvalid() {
        Assert.That(Evaluate(true).valid, Is.False);
    }
    [Test] public void SegmentsAreDurationWeightedAndDuplicateFinalIsNotCountedTwice() {
        var result = Evaluate(true, Sample(1, 3f, 1f, 1f), Sample(2, 1f, -1f, -1f), Sample(2, 1f, -1f, -1f));
        Assert.That(result.validSampleCount, Is.EqualTo(2));
        Assert.That(result.speechSeconds, Is.EqualTo(4f));
        Assert.That(result.meanArousal, Is.EqualTo(0.5f));
        Assert.That(result.meanDominance, Is.EqualTo(0.5f));
        Assert.That(result.deliveryScore, Is.EqualTo(75f));
    }
    [Test] public void AcousticBandsMatchVoiceAdDefaults() {
        Assert.That(PerformanceEvaluator.ScoreSpeed(Speed.Normal, 2f, _profile), Is.EqualTo(100f));
        Assert.That(PerformanceEvaluator.ScoreSpeed(Speed.Normal, 5f, _profile), Is.EqualTo(100f));
        Assert.That(PerformanceEvaluator.ScoreVolume(Volume.Normal, 0.3f, _profile), Is.EqualTo(100f));
        Assert.That(PerformanceEvaluator.ScoreVolume(Volume.Normal, 0.6f, _profile), Is.EqualTo(100f));
    }

    private const string Packet = "{\"A\":0.4,\"D\":-0.2,\"V\":0.9,\"speech_rate\":3.5,\"loudness\":0.45," +
        "\"speech_rate_level\":0,\"loudness_level\":0,\"utterance_started_at\":123.5," +
        "\"segment_index\":1,\"segment_seconds\":0.6,\"speech_ended\":true}";
    [Test] public void VoiceAdPacketUsesDominanceAndIgnoresValence() {
        Assert.That(AnalysisUdpReceiver.TryParseAnalysisPacket(Packet, out var packet, out var error), Is.True, error);
        var sample = ScoringSample.FromPacket(packet);
        Assert.That(sample.dominance, Is.EqualTo(-0.2f));
        Assert.That(packet.speech_ended, Is.True);
        Assert.That(packet.utterance_started_at, Is.EqualTo(123.5d));
    }
    [TestCase("\"D\":-0.2", "\"D\":null")]
    [TestCase("\"D\":-0.2", "\"D\":1.1")]
    [TestCase("\"D\":-0.2", "\"D\":\"0.4\"")]
    [TestCase("\"segment_index\":1", "\"segment_index\":0")]
    [TestCase("\"segment_index\":1", "\"segment_index\":1.5")]
    [TestCase("\"segment_seconds\":0.6", "\"segment_seconds\":0")]
    [TestCase("\"speech_ended\":true", "\"speech_ended\":\"true\"")]
    [TestCase("\"speech_rate_level\":0", "\"speech_rate_level\":\"medium\"")]
    [TestCase("\"loudness\":0.45", "\"loudness\":-1")]
    public void InvalidPacketsAreRejected(string before, string after) {
        Assert.That(AnalysisUdpReceiver.TryParseAnalysisPacket(Packet.Replace(before, after), out _, out _), Is.False);
    }
    [Test] public void PacketWithoutEndpointMetadataIsRejected() {
        Assert.That(AnalysisUdpReceiver.TryParseAnalysisPacket("{\"A\":0,\"D\":0}", out _, out _), Is.False);
    }
}

public sealed class PresentationTaskControllerTests {
    private GameObject _root;
    private GameObject _textRoot;
    private PresentationTaskController _controller;
    private SpeechText _text;
    private ScoringProfile _profile;
    private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);

    [SetUp] public void SetUp() {
        _root = new GameObject("Controller test");
        _root.SetActive(false);
        _controller = _root.AddComponent<PresentationTaskController>();
        var view = _root.AddComponent<AudienceView>();
        var ui = _root.AddComponent<UIManager>();
        _textRoot = new GameObject("Text", typeof(RectTransform));
        _textRoot.SetActive(false);
        Set(ui, "_textDisplay", _textRoot.AddComponent<TMPro.TextMeshProUGUI>());
        _text = ScriptableObject.CreateInstance<SpeechText>();
        Set(_text, "_texts", new[] {
            new TextItem { lineId = "first", text = "一", deliveryStyle = DeliveryStyle.EnergeticConfident },
            new TextItem { lineId = "second", text = "二", deliveryStyle = DeliveryStyle.CalmConfident }
        });
        _profile = ScriptableObject.CreateInstance<ScoringProfile>();
        Set(_controller, "_speechText", _text);
        Set(_controller, "_scoringProfile", _profile);
        Set(_controller, "_ui", ui);
        Set(_controller, "_audience", new AudienceFeedbackController(view));
        Set(_controller, "_initialized", true);
        Call(_controller, "EnterCurrentLine", new object[] { null });
        Set(_controller, "_lineShownAt", 100d);
    }
    [TearDown] public void TearDown() {
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_textRoot);
        Object.DestroyImmediate(_text);
        Object.DestroyImmediate(_profile);
    }
    private void Receive(bool ended, int index = 1, double startedAt = 101d) {
        Call(_controller, "HandleAnalysisReceived", new VoiceAnalysisPacket {
            utterance_started_at = startedAt, segment_index = index, segment_seconds = 0.6f,
            speech_ended = ended, A = 1, D = 1, speech_rate = 3.5, loudness = 0.45
        });
    }
    [Test] public void FirstFinalPacketScoresFirstLineWithoutDiscardingIt() {
        Receive(true);
        Assert.That(Get<int>(_controller, "_lineIndex"), Is.EqualTo(1));
        Assert.That(Get<List<LineEvaluationResult>>(_controller, "_lineResults").Count, Is.EqualTo(1));
    }
    [Test] public void MaximumLengthChunksWaitForActualEndpoint() {
        Receive(false);
        Receive(false, 2);
        Assert.That(Get<int>(_controller, "_lineIndex"), Is.Zero);
        Receive(true, 3);
        Assert.That(Get<int>(_controller, "_lineIndex"), Is.EqualTo(1));
        Assert.That(Get<List<LineEvaluationResult>>(_controller, "_lineResults")[0].validSampleCount, Is.EqualTo(3));
    }
    [Test] public void RepeatedFinalChunkDoesNotDoubleCountOrAdvanceAgain() {
        Receive(false);
        Receive(true);
        Receive(true);
        Assert.That(Get<int>(_controller, "_lineIndex"), Is.EqualTo(1));
        Assert.That(Get<List<LineEvaluationResult>>(_controller, "_lineResults")[0].validSampleCount, Is.EqualTo(1));
    }
    [Test] public void ResultsRecordedBeforeLineDisplayAreIgnored() {
        Receive(true, 1, 99d);
        Assert.That(Get<int>(_controller, "_lineIndex"), Is.Zero);
    }
    [Test] public void MissingEndpointDoesNotMixDifferentUtterances() {
        Receive(false, 1, 101d);
        Receive(true, 1, 102d);
        var result = Get<List<LineEvaluationResult>>(_controller, "_lineResults")[0];
        Assert.That(result.validSampleCount, Is.EqualTo(1));
    }
    [Test] public void FinalLineCompletesSessionAndIgnoresFurtherPackets() {
        Receive(true);
        double nextStart = Get<double>(_controller, "_lineShownAt") + 0.01d;
        Receive(true, 1, nextStart);
        Receive(true, 1, nextStart + 1d);
        Assert.That(Get<PresentationTaskState>(_controller, "_state"), Is.EqualTo(PresentationTaskState.Completed));
        Assert.That(Get<List<LineEvaluationResult>>(_controller, "_lineResults").Count, Is.EqualTo(2));
    }
}

public sealed class VoiceAdSceneIntegrationTests {
    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator UdpEndpointAdvancesConfiguredSceneExactlyOnce() {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        yield return new UnityEngine.TestTools.EnterPlayMode();
        yield return null;
        var controller = Object.FindFirstObjectByType<PresentationTaskController>();
        var receiver = Object.FindFirstObjectByType<AnalysisUdpReceiver>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var indexField = typeof(PresentationTaskController).GetField("_lineIndex", flags);
        var initializedField = typeof(PresentationTaskController).GetField("_initialized", flags);
        bool configured = controller != null && receiver != null && (bool)initializedField.GetValue(controller);
        bool waited = false, advanced = false, duplicateIgnored = false;
        if (configured) {
            double startedAt = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
            var packet = new VoiceAnalysisPacket {
                utterance_started_at = startedAt, segment_index = 1, segment_seconds = 3f,
                A = 0.7, D = 0.6, speech_rate = 3.5, loudness = 0.45, speech_ended = false
            };
            using (var sender = new System.Net.Sockets.UdpClient()) {
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                sender.Send(bytes, bytes.Length, "127.0.0.1", 5005);
                yield return new WaitForSecondsRealtime(0.2f);
                waited = (int)indexField.GetValue(controller) == 0;
                packet.speech_ended = true;
                bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                sender.Send(bytes, bytes.Length, "127.0.0.1", 5005);
                double deadline = Time.realtimeSinceStartupAsDouble + 3d;
                while ((int)indexField.GetValue(controller) == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                advanced = (int)indexField.GetValue(controller) == 1;
                sender.Send(bytes, bytes.Length, "127.0.0.1", 5005);
                yield return new WaitForSecondsRealtime(0.2f);
                duplicateIgnored = (int)indexField.GetValue(controller) == 1;
            }
        }
        yield return new UnityEngine.TestTools.ExitPlayMode();
        Assert.That(configured, Is.True, "The saved scene must initialize with all Inspector references.");
        Assert.That(waited, Is.True, "A maximum-length chunk must not advance the line.");
        Assert.That(advanced, Is.True, "The UDP end packet must advance without a key press.");
        Assert.That(duplicateIgnored, Is.True, "Repeated end packets must not advance another line.");
    }
}
