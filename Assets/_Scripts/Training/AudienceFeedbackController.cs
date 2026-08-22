using System.Collections.Generic;

public sealed class AudienceFeedbackController {
    private readonly AudienceView _view;
    private readonly List<Role> _roles;
    private Role _targetRole;
    private int _targetRoleIndex = -1;
    private int _voiceMatchCount;
    private bool _voiceFeedbackPlayed;
    private bool _gazeCompleted;

    public AudienceFeedbackController(AudienceView view) {
        _view = view;
        _roles = new List<Role>();
        if (view?.Roles == null) return;

        foreach (Role role in view.Roles) {
            if (role != null) _roles.Add(role);
        }
    }

    public int TargetRoleIndex => _targetRoleIndex;
    public string TargetRoleId => _targetRole != null ? _targetRole.RoleId : string.Empty;
    public bool GazeCompleted => _gazeCompleted;

    public void BeginLine(TextItem item, int lineIndex) {
        _view?.CompleteGazePrompt(_targetRole);
        _voiceMatchCount = 0;
        _voiceFeedbackPlayed = false;
        _gazeCompleted = false;
        _targetRoleIndex = ResolveRoleIndex(item?.targetRoleIndex ?? -1, lineIndex);
        _targetRole = _targetRoleIndex >= 0 ? _roles[_targetRoleIndex] : null;
        _view?.BeginGazePrompt(_targetRole);
    }

    public bool TryCompleteGaze() {
        if (_gazeCompleted || _targetRole == null || _view == null) return false;
        if (!_view.IsPlayerLookingAt(_targetRole)) return false;

        _gazeCompleted = true;
        _view.CompleteGazePrompt(_targetRole);
        _targetRole.PlayNod();
        return true;
    }

    public void RegisterVoiceMatch(bool matches, int requiredConsecutiveMatches) {
        if (_voiceFeedbackPlayed) return;
        _voiceMatchCount = matches ? _voiceMatchCount + 1 : 0;
        if (_voiceMatchCount < requiredConsecutiveMatches) return;

        Role feedbackRole = _roles.Find(role => role != _targetRole) ?? _targetRole;
        feedbackRole?.PlayNod();
        _voiceFeedbackPlayed = true;
    }

    public void FinishSession(float totalScore, float applauseThreshold) {
        _view?.CompleteGazePrompt(_targetRole);
        if (totalScore >= applauseThreshold) {
            foreach (Role role in _roles) {
                role.PlayClap();
            }
            return;
        }

        if (totalScore >= 60f) {
            foreach (Role role in _roles) {
                role.PlayNod();
            }
        }
    }

    private int ResolveRoleIndex(int configuredIndex, int lineIndex) {
        if (_roles.Count == 0) return -1;
        if (configuredIndex >= 0 && configuredIndex < _roles.Count) return configuredIndex;
        return lineIndex % _roles.Count;
    }
}
