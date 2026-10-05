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
                    Check(Read<int>("_line") == -1 && Read<VoiceReceiver>("_voice") == null, "Before Enter: no training or receiver");
                    _packet = PacketFor(0);
                    _packet.speech_ended = true;
                    Send();
                    break;
                case 1:
                    Check(Read<int>("_line") == -1, "UDP before Enter cannot advance training");
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
                    if (!Read<bool>("_looked")) Check(target.gameObject.layer == 6, "Target highlighted before gaze");
                    var bounds = target.GetComponent<Collider>().bounds;
                    var camera = Read<Camera>("_camera");
                    camera.transform.position = bounds.center - Vector3.forward;
                    camera.transform.LookAt(bounds.center);
                    Physics.SyncTransforms();
                    _packet = PacketFor(_line);
                    break;
                case 5:
                    Check(Read<bool>("_looked"), "Real camera ray completes gaze on line " + (_line + 1));
                    Check(Read<AudienceRole>("_target").gameObject.layer != 6, "Gaze clears target highlight");
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
                    Send();
                    break;
                case 8:
                    Check(Read<int>("_line") == _line + 1, "Endpoint advances exactly one line");
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
                    Directory.CreateDirectory("ValidationResults/UDP");
                    File.Copy(log.FilePath, "ValidationResults/UDP/session.jsonl", true);
                    break;
                case 11:
                    Check(Errors.Count == 0, "No runtime errors: " + string.Join(" | ", Errors));
                    File.WriteAllText("ValidationResults/UDP/result.json", JsonConvert.SerializeObject(new {
                        success = true, checks = _checks, completedLines = _line, totalScore = 100,
                        unity = Application.unityVersion, scene = "Assets/Scenes/SampleScene.unity",
                        keyboard = "Enter starts; NumpadEnter while active does not advance",
                        transport = "Actual UDP 127.0.0.1:5005", gaze = "Actual Physics.Raycast",
                        rendererResources = "Repeated Create releases old materials; Dispose releases final materials",
                        editorIssues = EditorIssues.ToArray(), utc = DateTime.UtcNow
                    }, Formatting.Indented));
                    Debug.Log("UDP_SMOKE_SUCCESS: " + _checks + " checks, " + _line + " lines, score 100.");
                    Finish(0);
                    break;
            }
        }
        catch (Exception exception) { Fail(exception); }
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

