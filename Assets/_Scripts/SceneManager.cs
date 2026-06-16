using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SceneManager : MonoBehaviour {
    [SerializeField] private AnalysisUdpReceiver _analysisUdpReceiver;
    public List<Role> _roles;

    private void Awake() {
        if (_analysisUdpReceiver == null) {
            _analysisUdpReceiver = FindFirstObjectByType<AnalysisUdpReceiver>();
        }
    }

    private void Update() {
        // if (_analysisUdpReceiver == null || !_analysisUdpReceiver.HasData) return;

        AnalysisData analysisData = _analysisUdpReceiver.LatestData;

        foreach (Role role in _roles) {
            if (role == null) continue;

            if(Keyboard.current.qKey.wasPressedThisFrame) {
                role.Refresh(analysisData);
            }
            if(Keyboard.current.nKey.wasPressedThisFrame) {
                StartCoroutine(role.PlayNod());
            }
        }
    }
}
