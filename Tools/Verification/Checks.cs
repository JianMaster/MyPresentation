using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using PresentationRewrite;
using UnityEngine;

internal static class Checks {
    private static int _checks;
    private const string Json = "{\"utterance_started_at\":1000,\"segment_index\":1,\"segment_seconds\":3,\"speech_ended\":false,\"A\":1,\"D\":1,\"speech_rate\":3.5,\"loudness\":0.45,\"V\":-1}";

    private static void Assert(bool condition, string name) {
        if (!condition) throw new Exception(name);
        _checks++;
        Console.WriteLine("PASS " + name);
    }

    private static void Near(float actual, float expected, string name) => Assert(Math.Abs(actual - expected) < 0.001f, name);

    public static int Main() {
        try {
            var settings = new ScoringSettings();
            var packet = VoicePacket.Parse(Json);
            Assert(packet != null && packet.D == 1, "VoiceAD A/D packet accepted; V ignored");
            Assert(VoicePacket.Parse("invalid") == null, "Malformed JSON rejected");
            Assert(VoicePacket.Parse(Json.Replace("\"D\":1,", "")) == null, "Missing required dimension rejected");
            Assert(VoicePacket.Parse(Json.Replace("\"D\":1", "\"D\":null")) == null, "Null dimension rejected");
            Assert(VoicePacket.Parse(Json.Replace("\"D\":1", "\"D\":2")) == null, "Out-of-range dimension rejected");
            Assert(VoicePacket.Parse(Json.Replace("\"D\":1", "\"D\":\"NaN\"")) == null, "NaN rejected");
            Assert(VoicePacket.Parse(Json.Replace("\"segment_seconds\":3", "\"segment_seconds\":0")) == null, "Zero duration rejected");
            Assert(VoicePacket.Parse(Json.Replace("\"speech_rate\":3.5", "\"speech_rate\":\"Infinity\"")) == null, "Infinite acoustic value rejected");

            var utterance = new Utterance();
            Assert(!utterance.Add(packet), "Maximum-length segment does not end a line");
            var tail = VoicePacket.Parse(Json.Replace("\"segment_index\":1", "\"segment_index\":2")
                .Replace("\"segment_seconds\":3", "\"segment_seconds\":1").Replace("\"A\":1", "\"A\":-1"));
            Assert(!utterance.Add(tail), "Additional intermediate segment keeps line open");
            Near(utterance.Mean.x, 0.5f, "Segments weighted by duration");
            tail.speech_ended = true;
            Assert(utterance.Add(tail), "Repeated segment with endpoint completes utterance");
            Near(utterance.Mean.x, 0.5f, "Repeated final segment is not counted twice");
            var next = VoicePacket.Parse(Json.Replace("1000", "1001").Replace("\"A\":1", "\"A\":-1"));
            Assert(!utterance.Add(next), "New utterance replaces previous samples");
            Near(utterance.Mean.x, -1f, "Different utterances are not mixed");
            Assert(!utterance.Add(tail), "Delayed endpoint from older utterance is ignored");

            var line = new SpeechLine { deliveryStyle = Delivery.EnergeticConfident };
            var voice = new Vector4(1, 1, 3.5f, 0.45f);
            Near(Scoring.Total(Scoring.Evaluate(line, voice, true, settings), settings), 100f, "Matching voice and gaze score 100");
            Near(Scoring.Total(Scoring.Evaluate(line, voice, false, settings), settings), 75f, "Missing gaze subtracts 25 points");
            foreach (Delivery delivery in Enum.GetValues(typeof(Delivery))) {
                line.deliveryStyle = delivery;
                voice.x = delivery == Delivery.EnergeticConfident || delivery == Delivery.EnergeticHesitant ? 1 : -1;
                voice.y = delivery == Delivery.CalmConfident || delivery == Delivery.EnergeticConfident ? 1 : -1;
                Near(Scoring.Evaluate(line, voice, true, settings).x, 100f, "Delivery target " + delivery);
            }
            line.speed = Level.Low;
            line.volume = Level.High;
            voice.z = 2;
            voice.w = 0.6f;
            Near(Scoring.Evaluate(line, voice, true, settings).y, 100, "Slow speed boundary");
            Near(Scoring.Evaluate(line, voice, true, settings).z, 100, "Loud volume boundary");
            voice.z = 5;
            voice.w = 0.3f;
            Near(Scoring.Evaluate(line, voice, true, settings).y, 0, "Speed falloff");
            Near(Scoring.Evaluate(line, voice, true, settings).z, 0, "Volume falloff");

            Assert(settings.IsValid, "Default scoring configuration valid");
            settings.weights = new Vector4(0, 0, 0, 1);
            Near(Scoring.Total(new Vector4(100, 100, 100, 20), settings), 20, "Configured weights affect total");
            Assert(Scoring.Advice(new Vector4(0, 0, 0, 20), settings).Contains("視線"), "Advice excludes dimensions with zero weight");
            settings.weights = Vector4.zero;
            Assert(!settings.IsValid, "All-zero weights rejected");
            settings = new ScoringSettings { speedBand = new Vector2(5, 2) };
            Assert(!settings.IsValid, "Reversed scoring interval rejected");
            settings = new ScoringSettings { volumeFalloff = float.NaN };
            Assert(!settings.IsValid, "Non-finite scoring parameter rejected");
            settings = new ScoringSettings { speedBand = new Vector2(8, 10) };
            line.speed = Level.Normal;
            Near(Scoring.Evaluate(line, new Vector4(1, 1, 9, 0.45f), true, settings).y, 100, "Configured interval affects scoring");
            settings = new ScoringSettings();
            Assert(Scoring.Advice(new Vector4(90, 10, 80, 100), settings).Contains("話速"), "Advice selects weakest dimension");

            var annotated = new SpeechLine {
                text = "皆さん、本日はよろしくお願いします。",
                emphasis = new[] { "本日", "", null }, pause_after = new[] { "皆さん", null }
            };
            Assert(annotated.FormattedText() == "皆さん/<color=#FFD54A>本日</color>はよろしくお願いします。", "Pause and emphasis formatting");
            Assert(new SpeechLine { text = null, emphasis = null, pause_after = null }.FormattedText() == "", "Missing annotations are harmless");
            string waiting = TrainingView.StatusFor(false, 0, 10);
            string analyzing = TrainingView.StatusFor(true, 0, 10);
            Assert(waiting != analyzing && analyzing.Contains("分析中"), "Waiting and analyzing statuses differ");
            Assert(TrainingView.StatusFor(false, 10, 10).Contains("マイク"), "No-result timeout gives useful troubleshooting");
            Assert(TrainingView.StatusFor(true, 10, 10).Contains("終了通知"), "Missing-endpoint timeout keeps waiting for endpoint");
            Assert(TrainingView.StatusFor(true, 0, 10) == analyzing, "New result clears timeout warning");

            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            var log = new SessionLog(logDirectory, settings);
            log.Line(1, new SpeechLine { lineId = "company-01" }, new Vector4(1, -1, 3.5f, 0.45f), new Vector4(100, 90, 80, 0), 67.5f);
            Assert(File.ReadAllLines(log.FilePath).Length == 2, "Each line is saved before session completion");
            log.Finish(true, 1, new Vector4(100, 90, 80, 0), 67.5f);
            var records = File.ReadAllLines(log.FilePath);
            Assert(records.Length == 3 && log.Error == null, "One start, one line and one summary logged");
            var resultRecord = JObject.Parse(records[1]);
            Near(resultRecord["mean"]["speech_rate"].Value<float>(), 3.5f, "Log contains acoustic averages");
            Near(resultRecord["scores"][3].Value<float>(), 0, "Log preserves gaze score");
            Assert(JObject.Parse(records[2])["completed"].Value<bool>(), "Completed summary distinguishable");
            var interrupted = new SessionLog(logDirectory, settings);
            interrupted.Finish(false, 0, Vector4.zero, 0);
            Assert(!JObject.Parse(File.ReadAllLines(interrupted.FilePath)[1])["completed"].Value<bool>(), "Interrupted session is not recorded as completed");
            // A regular file cannot be used as a directory: deterministic failure without touching user data.
            var failedLog = new SessionLog(log.FilePath, settings);
            Assert(failedLog.Error != null, "Log failure is exposed without crashing training");

            // Real loopback datagrams, on a spare port so the current scene is unaffected.
            int port;
            using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) {
                port = ((IPEndPoint)probe.Client.LocalEndPoint).Port;
            }
            using (var receiver = new VoiceReceiver(port))
            using (var sender = new UdpClient()) {
                Assert(!receiver.TryReceive(out _), "Empty socket returns immediately");
                byte[] invalid = Encoding.UTF8.GetBytes("invalid");
                byte[] valid = Encoding.UTF8.GetBytes(Json);
                sender.Send(invalid, invalid.Length, new IPEndPoint(IPAddress.Loopback, port));
                sender.Send(valid, valid.Length, new IPEndPoint(IPAddress.Loopback, port));
                int received = 0;
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (received < 2 && DateTime.UtcNow < deadline) {
                    if (receiver.TryReceive(out var result)) {
                        Assert(received == 0 ? result == null : result != null, "UDP packet order and invalid-packet discard " + received);
                        received++;
                    }
                    else Thread.Sleep(1);
                }
                Assert(received == 2, "Both datagrams consumed");
            }
            using (var reopened = new VoiceReceiver(port)) {
                Assert(!reopened.TryReceive(out _), "Disposal releases port for next session");
            }
            Console.WriteLine($"{_checks} checks passed.");
            return 0;
        }
        catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

