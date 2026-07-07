using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SceneManager : MonoBehaviour {
    [SerializeField] private AnalysisUdpReceiver _analysisUdpReceiver;
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private int _highlightLayer = 6;
    [SerializeField] private float _highlightDelay = 2f;
    [SerializeField] private float _gazeMaxDistance = 100f;
    [SerializeField] private float _screenCenterThreshold = 0.08f;

    public List<Role> _roles;

    private readonly Dictionary<Role, HighlightState> _highlightStates = new Dictionary<Role, HighlightState>();

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
        HighlightState state = GetHighlightState(role);

        if (role.IslookingAtPlayer) {
            state.LookingAtPlayerTime += Time.deltaTime;

            if (!state.IsHighlighted && state.LookingAtPlayerTime >= _highlightDelay) {
                SetHighlighted(role, state, true);
            }
        }
        else {
            state.LookingAtPlayerTime = 0f;
        }

        if (state.IsHighlighted && IsPlayerLookingAt(role)) {
            SetHighlighted(role, state, false);
            state.LookingAtPlayerTime = 0f;
        }
    }

    private HighlightState GetHighlightState(Role role) {
        if (_highlightStates.TryGetValue(role, out HighlightState state)) {
            return state;
        }

        state = new HighlightState(role);
        _highlightStates.Add(role, state);
        return state;
    }

    private void SetHighlighted(Role role, HighlightState state, bool highlighted) {
        state.IsHighlighted = highlighted;

        foreach (Transform child in role.GetComponentsInChildren<Transform>(true)) {
            child.gameObject.layer = highlighted ? _highlightLayer : state.GetOriginalLayer(child);
        }
    }

    private bool IsPlayerLookingAt(Role role) {
        if (_playerCamera == null) return false;

        Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, _gazeMaxDistance)) {
            return hit.transform == role.transform || hit.transform.IsChildOf(role.transform);
        }

        Bounds bounds = GetRoleBounds(role);
        if (bounds.size == Vector3.zero) return false;

        Vector3 viewportPoint = _playerCamera.WorldToViewportPoint(bounds.center);
        if (viewportPoint.z <= 0f) return false;

        Vector2 offset = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
        return offset.magnitude <= _screenCenterThreshold;
    }

    private Bounds GetRoleBounds(Role role) {
        Renderer[] renderers = role.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(role.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private class HighlightState {
        public float LookingAtPlayerTime;
        public bool IsHighlighted;

        private readonly Dictionary<Transform, int> _originalLayers = new Dictionary<Transform, int>();

        public HighlightState(Role role) {
            foreach (Transform child in role.GetComponentsInChildren<Transform>(true)) {
                _originalLayers[child] = child.gameObject.layer;
            }
        }

        public int GetOriginalLayer(Transform transform) {
            return _originalLayers.TryGetValue(transform, out int layer) ? layer : transform.gameObject.layer;
        }
    }
}
