using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PresentationRewrite {
    public sealed class Main : MonoBehaviour {
        [SerializeField] private SpeechText _speech;
        [SerializeField] private ScoringProfile _profile;
        [SerializeField] private TrainingView _view;
        [SerializeField] private Camera _camera;
        [SerializeField] private AudienceRole[] _audience;
        [SerializeField, Min(1f)] private float _resultTimeout = 10f;
        [SerializeField, Min(-1), Tooltip("-1：总台词数 / 3；0：关闭；正数：次数上限")]
        private int _gazeEventCount = -1;
        private int _gazeEventsRemaining;
        private int _gazeEvents, _gazeHits;
        private readonly HashSet<AudienceRole> _pendingGaze = new HashSet<AudienceRole>();

        private VoiceReceiver _voice;
        private Utterance _utterance;
        private AudienceRole _target;
        private Vector4 _scores;
        private int _line = -1;
        private double _shownAt;
        private double _completedSpeech;
        private bool _looked;
        private float _lastResultAt;
        private SessionLog _log;

        private void OnEnable() {
            if (_speech == null || _speech.lines == null || _speech.lines.Length == 0 ||
                Array.Exists(_speech.lines, line => line == null || (uint)line.deliveryStyle > 3 || (uint)line.speed > 2 || (uint)line.volume > 2) ||
                _profile == null || _profile.settings == null || !_profile.settings.IsValid ||
                _view == null || !_view.IsConfigured || _camera == null ||
                _audience == null || _audience.Length == 0 || Array.Exists(_audience, role => role == null)) {
                Debug.LogError("Main: 请检查台词、评分配置、界面、摄像机和观众引用。", this);
                enabled = false;
                return;
            }
            _line = -1;
            _scores = Vector4.zero;
            _gazeEvents = _gazeHits = 0;
            _pendingGaze.Clear();
            _completedSpeech = 0;
            _log = null;
            _view.ShowMessage("Enter キーでトレーニング開始");
        }

        private void Update() {
            if (_line < 0) {
                var keyboard = Keyboard.current;
                if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) {
                    BeginTraining();
                }
                return;
            }
            if (_voice == null) return;

            if (Physics.Raycast(_camera.ViewportPointToRay(new Vector3(0.5f, 0.5f)), out var hit, 100f)) {
                var role = hit.transform.GetComponentInParent<AudienceRole>();
                if (role != null && role.IsLookingAt(_camera.transform) && _pendingGaze.Remove(role)) {
                    _gazeHits++;
                    role.Nod();
                }
                if (!_looked && hit.transform.IsChildOf(_target.transform)) {
                    _looked = true;
                    _target.Nod();
                }
            }

            try {
                // Limit work per frame; no receive thread, event queue or blocking wait.
                for (int i = 0; i < 64 && _voice != null && _voice.TryReceive(out var packet); i++) {
                    if (packet == null || packet.utterance_started_at < _shownAt ||
                        packet.utterance_started_at <= _completedSpeech || packet.utterance_started_at < _utterance.StartedAt) continue;
                    _lastResultAt = Time.realtimeSinceStartup;
                    if (_utterance.Add(packet)) CompleteLine();
                }
                if (_voice != null) UpdateStatus();
            }
            catch (SocketException exception) {
                _view.ShowMessage($"音声受信エラー: {exception.Message}");
                enabled = false;
            }
        }

        private void BeginTraining() {
            try {
                _voice = new VoiceReceiver();
            }
            catch (SocketException exception) {
                _view.ShowMessage($"音声受信を開始できません: {exception.Message}\nEnter キーで再試行");
                return;
            }
            _line = 0;
            _gazeEventsRemaining = _gazeEventCount < 0 ? _speech.lines.Length / 3 : _gazeEventCount;
            _log = new SessionLog(Path.Combine(Application.persistentDataPath, "Sessions"), _profile.settings);
            ShowLine();
        }

        private void ShowLine() {
            _utterance = new Utterance();
            _looked = false;
            _shownAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
            _lastResultAt = Time.realtimeSinceStartup;
            int index = _speech.lines[_line].targetRoleIndex;
            _target = _audience[index >= 0 && index < _audience.Length ? index : _line % _audience.Length];
            if (_gazeEventsRemaining > 0 && UnityEngine.Random.value < 0.5f) {
                _gazeEventsRemaining--;
                _gazeEvents++;
                _pendingGaze.Add(_target);
                _target.LookAt(_camera.transform);
            }
            _view.ShowLine(_speech.lines[_line], _line, _speech.lines.Length);
            UpdateStatus();
        }

        private void UpdateStatus() {
            _view.ShowStatus(TrainingView.StatusFor(_utterance.StartedAt > 0,
                Time.realtimeSinceStartup - _lastResultAt, _resultTimeout), _log.Error);
        }

        private void CompleteLine() {
            _completedSpeech = _utterance.StartedAt;
            var settings = _profile.settings;
            var scores = Scoring.Evaluate(_speech.lines[_line], _utterance.Mean, settings);
            _scores += scores;
            _log.Line(_line + 1, _speech.lines[_line], _utterance.Mean, scores, Scoring.Total(scores, settings, false));
            _line++;
            if (_line < _speech.lines.Length) {
                ShowLine();
                return;
            }
            _voice.Dispose();
            _voice = null;
            var average = AverageScores();
            _log.Finish(true, _line, average, Scoring.Total(average, settings, _gazeEvents > 0));
            _view.ShowResult(average, settings, _log.Error, _gazeEvents > 0);
            if (Scoring.Total(average, settings, _gazeEvents > 0) >= settings.applauseThreshold) {
                foreach (var role in _audience) role.Clap();
            }
        }

        private Vector4 AverageScores() {
            var average = _line > 0 ? _scores / _line : Vector4.zero;
            average.w = _gazeEvents > 0 ? 100f * _gazeHits / _gazeEvents : 0f;
            return average;
        }

        private void OnDisable() {
            if (_voice != null && _log != null) {
                var average = AverageScores();
                _log.Finish(false, Math.Max(0, _line), average, Scoring.Total(average, _profile.settings, _gazeEvents > 0));
            }
            _voice?.Dispose();
            _voice = null;
        }
    }
}
