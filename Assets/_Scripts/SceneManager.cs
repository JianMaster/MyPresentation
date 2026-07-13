using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SceneManager : MonoBehaviour {
    [SerializeField] private AnalysisUdpReceiver _analysisUdpReceiver;
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private int _highlightLayer = 6;
    [SerializeField] private float _highlightDelay = 2f;

    public List<Role> _roles;

    private readonly Dictionary<Role, float> _lookAtPlayerTimes = new Dictionary<Role, float>();
    private readonly HashSet<Role> _highlightedRoles = new HashSet<Role>();
    private readonly Dictionary<Transform, int> _originalLayers = new Dictionary<Transform, int>();

    private void Awake() {

        if (_playerCamera == null) {
            _playerCamera = Camera.main;
        }
    }

    private void Update() {
        // if (_analysisUdpReceiver == null || !_analysisUdpReceiver.HasData) return;

        AnalysisData analysisData = _analysisUdpReceiver != null ? _analysisUdpReceiver.LatestData : null;
        Keyboard keyboard = Keyboard.current;

        foreach (Role role in _roles) {
            if (role == null) continue;

            UpdateRoleHighlight(role);

            if (keyboard != null && keyboard.nKey.wasPressedThisFrame) {
                StartCoroutine(role.PlayNod());
            }
        }
        if (keyboard != null && keyboard.qKey.wasPressedThisFrame) {
            _roles[Random.Range(0, _roles.Count)].Refresh(analysisData);
        }
    }

    private void UpdateRoleHighlight(Role role) {
        _lookAtPlayerTimes.TryAdd(role, 0f);

        if (role.IslookingAtPlayer) {
            _lookAtPlayerTimes[role] += Time.deltaTime;

            if (!_highlightedRoles.Contains(role) && _lookAtPlayerTimes[role] >= _highlightDelay) {
                SetHighlighted(role, true);
            }
        }
        else {
            _lookAtPlayerTimes[role] = 0f;
        }

        if (_highlightedRoles.Contains(role) && IsPlayerLookingAt(role)) {
            SetHighlighted(role, false);
            _lookAtPlayerTimes[role] = 0f;
        }
    }

    private void SetHighlighted(Role role, bool highlighted) {
        if (highlighted) {
            _highlightedRoles.Add(role);
        }
        else {
            _highlightedRoles.Remove(role);
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

    private bool IsPlayerLookingAt(Role role) {
        if (_playerCamera == null) return false;

        Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 100f)) {
            return hit.transform == role.transform || hit.transform.IsChildOf(role.transform);
        }

        return false;
    }
}
