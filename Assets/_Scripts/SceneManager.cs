using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SceneManager : MonoBehaviour {
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private int _highlightLayer = 6;
    [SerializeField] private float _highlightDelay = 2f;

    public List<Role> _roles;

    private readonly Dictionary<Role, float> _lookAtPlayerTimes = new Dictionary<Role, float>();
    private readonly HashSet<Role> _highlightedRoles = new HashSet<Role>();
    private readonly Dictionary<Transform, int> _originalLayers = new Dictionary<Transform, int>();
    public IReadOnlyList<Role> Roles => _roles;
    public Camera PlayerCamera => _playerCamera;

    private void Awake() {

        if (_playerCamera == null) {
            _playerCamera = Camera.main;
        }
    }

    private void Update() {
        Keyboard keyboard = Keyboard.current;

        foreach (Role role in _roles) {
            if (role == null) continue;

            UpdateRoleHighlight(role);

            if (keyboard != null && keyboard.nKey.wasPressedThisFrame) {
                StartCoroutine(role.PlayNod());
            }
        }
        if (keyboard != null && keyboard.qKey.wasPressedThisFrame) {
            if (_roles == null || _roles.Count == 0) return;
            _roles[Random.Range(0, _roles.Count)].Refresh();
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

    public bool IsPlayerLookingAt(Role role) {
        if (_playerCamera == null) return false;

        Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 100f)) {
            return hit.transform == role.transform || hit.transform.IsChildOf(role.transform);
        }

        return false;
    }

    public void BeginGazePrompt(Role role) {
        if (role == null) return;
        role.PromptLookAtPlayer(10f, 0f);
    }

    public void CompleteGazePrompt(Role role) {
        if (role == null) return;
        role.CancelLookPrompt();
        SetHighlighted(role, false);
        _lookAtPlayerTimes[role] = 0f;
    }
}
