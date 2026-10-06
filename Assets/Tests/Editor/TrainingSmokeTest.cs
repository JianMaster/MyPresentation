#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PresentationRewrite;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Main = PresentationRewrite.Main;

// Editor-only end-to-end check: actual keyboard events, physics and UDP, no calls to Main's private methods.
[InitializeOnLoad]
public static class TrainingSmokeTest {
    private const string ActiveKey = "Presentation.UDPValidation";
    private static Main _main;
    private static Keyboard _keyboard;
    private static UdpClient _sender;
    private static VoicePacket _packet;
    private static TMP_Text _text;
    private static int _step, _line, _checks;
    private static int _round, _remaining, _events;
    private static bool _event;
    private static AudienceRole _eventTarget;
    private static float _restoreAt;
    private static UnityEngine.Random.State _randomState;
    private static readonly List<object> Rounds = new List<object>();
    private static double _nextTick, _deadline;
    private static readonly List<string> Errors = new List<string>();
    private static readonly List<string> EditorIssues = new List<string>();
    private static InputSettings.BackgroundBehavior _background;
    private static InputSettings.EditorInputBehaviorInPlayMode _editorInput;

    static TrainingSmokeTest() {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("Presentation/Verify UDP Training")]
    public static void Run() {
        try {
            if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before running verification.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty) throw new Exception("Save scene changes before verification.");
            CheckRendererResources();
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Directory.CreateDirectory("ValidationResults/UDP");
            File.WriteAllText("ValidationResults/UDP/result.json", "{\"status\":\"running\"}");
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void OnPlayMode(PlayModeStateChange state) {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) {
            try {
                _main = UnityEngine.Object.FindFirstObjectByType<Main>();
                if (_main == null || !_main.enabled) throw new Exception("Scene Main is missing or disabled.");
                _text = new SerializedObject(Read<TrainingView>("_view")).FindProperty("_text").objectReferenceValue as TMP_Text;
                UnityEngine.Object.FindFirstObjectByType<PresentationRewrite.PlayerController>().enabled = false;
                _keyboard = InputSystem.AddDevice<Keyboard>();
                _background = InputSystem.settings.backgroundBehavior;
                _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                _sender = new UdpClient();
                Application.runInBackground = true;
                Errors.Clear();
                EditorIssues.Clear();
                Application.logMessageReceived += CaptureError;
                _step = _line = _checks = 0;
                _round = 0;
                Rounds.Clear();
                _randomState = UnityEngine.Random.state;
                Read<Camera>("_camera").transform.rotation = Quaternion.LookRotation(Vector3.up);
                _nextTick = EditorApplication.timeSinceStartup + 1;
                _deadline = EditorApplication.timeSinceStartup + 120;
                EditorApplication.update += Tick;
            }
            catch (Exception exception) { Fail(exception); }
        }
        else if (state == PlayModeStateChange.EnteredEditMode) {
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(ActiveKey + ".Exit", 1));
        }
    }

    private static void Tick() {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < _nextTick) return;
        _nextTick = EditorApplication.timeSinceStartup + 0.2;
        try {
            if (EditorApplication.timeSinceStartup > _deadline) throw new Exception("Validation timeout.");
            switch (_step++) {
                case 0:
                    _events = 0;
                    int count = Read<int>("_gazeEventCount");
                    _remaining = count < 0 ? Read<PresentationRewrite.SpeechText>("_speech").lines.Length / 3 : count;
                    Check(Read<AudienceRole[]>("_audience").All(r => RoleRead<Transform>(r, "_defaultTarget")?.name == "Screen"), "All NPCs have a default Screen reference");
                    Check(Read<int>("_line") == -1 && Read<VoiceReceiver>("_voice") == null, "Before Enter: no training or receiver");
                    _packet = PacketFor(0);
                    _packet.speech_ended = true;
                    Send();
                    break;
                case 1:
                    Check(Read<int>("_line") == -1, "UDP before Enter cannot advance training");
                    SetNextDecision(DecisionForLine(0));
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Enter));
                    break;
                case 2:
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    Check(Read<int>("_line") == 0 && Read<VoiceReceiver>("_voice") != null, "Enter starts first line");
                    _packet.utterance_started_at = Read<double>("_shownAt") - 10;
                    Send();
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.NumpadEnter));
                    break;
                case 3:
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    Check(Read<int>("_line") == 0, "Stale UDP and Enter during training do not skip a line");
                    break;
                case 4:
                    var target = Read<AudienceRole>("_target");
                    _event = _remaining > 0 && DecisionForLine(_line);
                    if (_event) { _remaining--; _events++; }
                    Check(Read<int>("_gazeEventsRemaining") == _remaining, "Each line makes one random decision only below the event cap");
                    _eventTarget = target;
                    _restoreAt = RoleRead<float>(target, "_restoreAt");
                    if (_event) Check(target.gameObject.layer == 6 && RoleRead<Transform>(target, "_target") == Read<Camera>("_camera").transform, "Triggered NPC looks at player and is highlighted");
                    var bounds = target.GetComponent<Collider>().bounds;
                    var camera = Read<Camera>("_camera");
                    camera.transform.position = bounds.center - Vector3.forward;
                    camera.transform.LookAt(bounds.center);
                    Physics.SyncTransforms();
                    _packet = PacketFor(_line);
                    break;
                case 5:
                    Check(Read<bool>("_looked"), "Real camera ray completes gaze on line " + (_line + 1));
                    Check(RoleRead<float>(_eventTarget, "_nodTime") >= 0, "Gaze still triggers the original nod feedback");
                    if (_event) Check(_eventTarget.gameObject.layer == 6 && RoleRead<float>(_eventTarget, "_restoreAt") == _restoreAt, "Gaze and nod do not cancel or restart NPC timer");
                    Read<Camera>("_camera").transform.rotation = Quaternion.LookRotation(Vector3.up);
                    Send();
                    break;
                case 6:
                    Check(Read<int>("_line") == _line, "Intermediate chunk does not advance");
                    Check(Read<Utterance>("_utterance").StartedAt == _packet.utterance_started_at, "Real UDP chunk received");
                    _packet.segment_index = 2;
                    _packet.segment_seconds = 1;
                    Send();
                    break;
                case 7:
                    Check(Read<int>("_line") == _line, "Tail without endpoint does not advance");
                    _packet.speech_ended = true;
                    SetNextDecision(DecisionForLine(_line + 1));
                    Send();
                    break;
                case 8:
                    Check(Read<int>("_line") == _line + 1, "Endpoint advances exactly one line");
                    if (_event) Check(Time.time < _restoreAt && RoleRead<Transform>(_eventTarget, "_target") == Read<Camera>("_camera").transform
                        && _eventTarget.gameObject.layer == 6, "Voice endpoint advances immediately while NPC continues its own timer");
                    Send();
                    break;
                case 9:
                    Check(Read<int>("_line") == _line + 1, "Repeated endpoint does not double-advance");
                    _line++;
                    if (_line < Read<PresentationRewrite.SpeechText>("_speech").lines.Length) _step = 4;
                    break;
                case 10:
                    Check(Read<VoiceReceiver>("_voice") == null, "Receiver closes at session end");
                    Check(_text.text.Contains("トレーニング完了"), "Final report displayed");
                    _text.ForceMeshUpdate();
                    Check(!_text.isTextOverflowing && _text.textBounds.size.y <= _text.rectTransform.rect.height + 1,
                        $"Final report fits text area (rendered={_text.textBounds.size}, rect={_text.rectTransform.rect}, font={_text.fontSize}, overflow={_text.isTextOverflowing})");
                    var log = Read<SessionLog>("_log");
                    Check(log.Error == null, "Session log saved without error");
                    var records = File.ReadAllLines(log.FilePath).Select(JObject.Parse).ToArray();
                    Check(records.Count(x => (string)x["type"] == "line") == _line, "Exactly ten line results logged");
                    var final = records.Single(x => (string)x["type"] == "finish");
                    Check((bool)final["completed"] && (int)final["lineCount"] == _line, "One completed summary logged");
                    Check(Math.Abs((float)final["total"] - 100f) < 0.01f, "Matching voice and gaze produce total 100");
                    foreach (var row in records.Where(x => (string)x["type"] == "line"))
                        Check(Math.Abs((float)row["total"] - 100f) < 0.01f, "Each line scored 100");
                    int expectedEvents = _round == 0 ? 3 : _round == 1 ? 2 : 0;
                    Check(_events == expectedEvents, "Random misses are not forced to fill the quota; event cap is respected");
                    Rounds.Add(new { cap = Read<int>("_gazeEventCount"), events = _events, remaining = _remaining, total = (float)final["total"] });
                    Directory.CreateDirectory("ValidationResults/UDP");
                    File.Copy(log.FilePath, _round == 0 ? "ValidationResults/UDP/session.jsonl" : $"ValidationResults/UDP/session-{_round}.jsonl", true);
                    break;
                case 11:
                    Check(Errors.Count == 0, "No runtime errors: " + string.Join(" | ", Errors));
                    if (_round < 3) {
                        _round++;
                        _main.enabled = false;
                        var config = new SerializedObject(_main);
                        config.FindProperty("_gazeEventCount").intValue = _round == 1 ? 2 : _round == 2 ? 3 : 0;
                        config.ApplyModifiedPropertiesWithoutUndo();
                        _main.enabled = true;
                        _step = _line = 0;
                        break;
                    }
                    _main.enabled = false;
                    _eventTarget = Read<AudienceRole[]>("_audience")[0];
                    _eventTarget.LookAt(Read<Camera>("_camera").transform);
                    _restoreAt = RoleRead<float>(_eventTarget, "_restoreAt");
                    Check(Math.Abs(_restoreAt - Time.time - 5f) < 0.05f, "NPC owns the five-second timer");
                    break;
                case 12:
                    if (Time.time < _restoreAt) {
                        Check(_eventTarget.gameObject.layer == 6, "NPC timer runs while Main is disabled");
                        _step = 12;
                        break;
                    }
                    Check(RoleRead<Transform>(_eventTarget, "_target") == RoleRead<Transform>(_eventTarget, "_defaultTarget")
                        && _eventTarget.gameObject.layer != 6, "NPC restores Screen by itself after five seconds");
                    Check(Errors.Count == 0, "No runtime errors after NPC timer");
                    File.WriteAllText("ValidationResults/UDP/result.json", JsonConvert.SerializeObject(new {
                        success = true, checks = _checks, completedLines = _line * Rounds.Count, totalScore = 100, rounds = Rounds,
                        unity = Application.unityVersion, scene = "Assets/Scenes/SampleScene.unity",
                        keyboard = "Enter starts; NumpadEnter while active does not advance",
                        transport = "Actual UDP 127.0.0.1:5005", gaze = "Per-line random check with cap; NPC owns timer; immediate voice advance; nod preserved; original scoring",
                        rendererResources = "Repeated Create releases old materials; Dispose releases final materials",
                        editorIssues = EditorIssues.ToArray(), utc = DateTime.UtcNow
                    }, Formatting.Indented));
                    Debug.Log("UDP_SMOKE_SUCCESS: " + _checks + " checks, 4 full sessions, original scoring preserved.");
                    Finish(0);
                    break;
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static T RoleRead<T>(AudienceRole role, string field) => (T)typeof(AudienceRole).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(role);
    private static bool DecisionForLine(int line) => _round == 0 ? line % 2 == 0 : _round != 2;
    private static void SetNextDecision(bool trigger) {
        // Control only the random input, so both branches and the cap are deterministic.
        for (int seed = 0; seed < 100; seed++) {
            UnityEngine.Random.InitState(seed);
            if ((UnityEngine.Random.value < 0.5f) != trigger) continue;
            UnityEngine.Random.InitState(seed);
            return;
        }
        throw new Exception("No seed found for random decision.");
    }

    private static void CheckRendererResources() {
        var feature = ScriptableObject.CreateInstance<RoleOutlineRendererFeature>();
        Material mask = null, outline = null;
        try {
            for (int i = 0; i < 3; i++) {
                feature.Create();
                if (mask != null || outline != null) throw new Exception("Renderer rebuild leaked previous materials.");
                mask = (Material)typeof(RoleOutlineRendererFeature).GetField("_maskMaterial", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feature);
                outline = (Material)typeof(RoleOutlineRendererFeature).GetField("_outlineMaterial", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feature);
                if (mask == null || outline == null) throw new Exception("Renderer materials missing after rebuild.");
            }
            feature.Dispose();
            if (mask != null || outline != null) throw new Exception("Renderer disposal leaked materials.");
            Debug.Log("RENDERER_RESOURCE_CHECK_PASS: 3 rebuilds and disposal");
        }
        finally {
            feature.Dispose();
            UnityEngine.Object.DestroyImmediate(feature);
        }
    }

    private static VoicePacket PacketFor(int index) {
        var line = Read<PresentationRewrite.SpeechText>("_speech").lines[index];
        return new VoicePacket {
            utterance_started_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d,
            segment_index = 1, segment_seconds = 3, speech_ended = false,
            A = line.deliveryStyle == Delivery.EnergeticConfident || line.deliveryStyle == Delivery.EnergeticHesitant ? 1 : -1,
            D = line.deliveryStyle == Delivery.CalmConfident || line.deliveryStyle == Delivery.EnergeticConfident ? 1 : -1,
            speech_rate = line.speed == Level.Low ? 1 : line.speed == Level.High ? 6 : 3.5f,
            loudness = line.volume == Level.Low ? 0.2f : line.volume == Level.High ? 0.8f : 0.45f
        };
    }

    private static void Send() {
        var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_packet));
        _sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, 5005));
    }

    private static T Read<T>(string field) => (T)typeof(Main).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_main);
    private static void Check(bool condition, string message) {
        if (!condition) throw new Exception(message);
        _checks++;
        Debug.Log("UDP_CHECK_PASS " + message);
    }
    private static void CaptureError(string message, string trace, LogType type) {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        // Unity's editor search index can fail during batch startup; retain it separately from gameplay failures.
        if (message.StartsWith("ArgumentOutOfRangeException") && trace.Contains("UnityEditor.Search.SearchDatabase"))
            EditorIssues.Add(message + "\n" + trace);
        else Errors.Add(message + "\n" + trace);
    }
    private static void Fail(Exception exception) {
        Directory.CreateDirectory("ValidationResults/UDP");
        File.WriteAllText("ValidationResults/UDP/result.json", JsonConvert.SerializeObject(new { success = false, step = _step, line = _line, error = exception.ToString() }, Formatting.Indented));
        Debug.LogException(exception);
        Finish(1);
    }
    private static void Finish(int exitCode) {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CaptureError;
        _sender?.Dispose();
        if (_keyboard != null) {
            UnityEngine.Random.state = _randomState;
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
        }
        SessionState.SetInt(ActiveKey + ".Exit", exitCode);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else {
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }
    }
}
#endif

