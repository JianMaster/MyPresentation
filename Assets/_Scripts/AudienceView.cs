using System.Collections.Generic;
using UnityEngine;

public sealed class AudienceView : MonoBehaviour {
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private List<Role> _roles = new List<Role>();
    [SerializeField] private int _highlightLayer = 6;
    [SerializeField, Min(0f)] private float _highlightDelay = 2f;
    [SerializeField, Min(0.1f)] private float _gazePromptDuration = 10f;
    [SerializeField, Min(0.1f)] private float _gazeMaxDistance = 100f;

    private readonly Dictionary<Role, float> _lookAtPlayerTimes = new Dictionary<Role, float>();
    private readonly HashSet<Role> _highlightedRoles = new HashSet<Role>();
    private readonly Dictionary<Transform, int> _originalLayers = new Dictionary<Transform, int>();

    public IReadOnlyList<Role> Roles => _roles;
    public bool IsConfigured => _playerCamera != null && _roles != null && _roles.Exists(role => role != null);

    private void Update() {
        if (_roles == null) return;

        foreach (Role role in _roles) {
            if (role != null) UpdateRoleHighlight(role);
        }
    }

    private void OnDisable() {
        if (_roles == null) return;

        foreach (Role role in _roles) {
            if (role == null) continue;
            role.CancelLookPrompt();
            SetHighlighted(role, false);
        }

        _lookAtPlayerTimes.Clear();
        _highlightedRoles.Clear();
        _originalLayers.Clear();
    }

    public bool IsPlayerLookingAt(Role role) {
        if (_playerCamera == null || role == null) return false;

        Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, _gazeMaxDistance)) return false;
        return hit.transform == role.transform || hit.transform.IsChildOf(role.transform);
    }

    public void BeginGazePrompt(Role role) {
        if (role == null) return;
        _lookAtPlayerTimes[role] = 0f;
        role.PromptLookAtPlayer(_gazePromptDuration);
    }

    public void CompleteGazePrompt(Role role) {
        if (role == null) return;
        role.CancelLookPrompt();
        SetHighlighted(role, false);
        _lookAtPlayerTimes[role] = 0f;
    }

    private void UpdateRoleHighlight(Role role) {
        if (!role.IsLookingAtPlayer) {
            _lookAtPlayerTimes[role] = 0f;
            return;
        }

        _lookAtPlayerTimes.TryGetValue(role, out float elapsed);
        elapsed += Time.deltaTime;
        _lookAtPlayerTimes[role] = elapsed;

        if (!_highlightedRoles.Contains(role) && elapsed >= _highlightDelay) {
            SetHighlighted(role, true);
        }
    }

    private void SetHighlighted(Role role, bool highlighted) {
        if (highlighted) {
            if (!_highlightedRoles.Add(role)) return;
        }
        else if (!_highlightedRoles.Remove(role)) {
            return;
        }

        foreach (Transform child in role.GetComponentsInChildren<Transform>(true)) {
            if (highlighted) {
                _originalLayers.TryAdd(child, child.gameObject.layer);
                child.gameObject.layer = _highlightLayer;
            }
            else if (_originalLayers.TryGetValue(child, out int originalLayer)) {
                child.gameObject.layer = originalLayer;
            }
        }
    }
}
