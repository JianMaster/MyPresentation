using System.Collections.Generic;
using System.Linq;

public sealed class AudienceFeedbackController {
    private readonly SceneManager _sceneManager;
    private readonly List<Role> _roles;
    private Role _targetRole;
    private int _targetRoleIndex = -1;
    private int _voiceMatchCount;
    private bool _voiceFeedbackPlayed;
    private bool _gazeCompleted;

    public AudienceFeedbackController(SceneManager sceneManager) {
        _sceneManager = sceneManager;
        _roles = sceneManager?.Roles?.Where(role => role != null).ToList() ?? new List<Role>();
    }

    public int TargetRoleIndex => _targetRoleIndex;
    public string TargetRoleId => _targetRole != null ? _targetRole.RoleId : string.Empty;
    public bool GazeCompleted => _gazeCompleted;

    public void BeginLine(TextItem item, int lineIndex) {
        _sceneManager?.CompleteGazePrompt(_targetRole);
        _voiceMatchCount = 0;
        _voiceFeedbackPlayed = false;
        _gazeCompleted = false;
        _targetRoleIndex = ResolveRoleIndex(item?.targetRoleIndex ?? -1, lineIndex);
        _targetRole = _targetRoleIndex >= 0 ? _roles[_targetRoleIndex] : null;
        _sceneManager?.BeginGazePrompt(_targetRole);
    }

    public bool TryCompleteGaze() {
        if (_gazeCompleted || _targetRole == null || _sceneManager == null) return false;
        if (!_sceneManager.IsPlayerLookingAt(_targetRole)) return false;

        _gazeCompleted = true;
        _sceneManager.CompleteGazePrompt(_targetRole);
        _targetRole.PlayNodImmediate();
        return true;
    }

    public void RegisterVoiceMatch(bool matches, int requiredConsecutiveMatches) {
        if (_voiceFeedbackPlayed) return;
        _voiceMatchCount = matches ? _voiceMatchCount + 1 : 0;
        if (_voiceMatchCount < requiredConsecutiveMatches) return;

        Role feedbackRole = _roles.FirstOrDefault(role => role != _targetRole) ?? _targetRole;
        feedbackRole?.PlayNodImmediate();
        _voiceFeedbackPlayed = true;
    }

    public void FinishSession(float totalScore, float applauseThreshold) {
        _sceneManager?.CompleteGazePrompt(_targetRole);
        if (totalScore >= applauseThreshold) {
            foreach (Role role in _roles) {
                role.PlayClap();
            }
            return;
        }

        if (totalScore >= 60f) {
            foreach (Role role in _roles) {
                role.PlayNodImmediate();
            }
        }
    }

    private int ResolveRoleIndex(int configuredIndex, int lineIndex) {
        if (_roles.Count == 0) return -1;
        if (configuredIndex >= 0 && configuredIndex < _roles.Count) return configuredIndex;
        return lineIndex % _roles.Count;
    }
}
