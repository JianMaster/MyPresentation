using UnityEngine;

[RequireComponent(typeof(Animator))]
public class NodAnimation : MonoBehaviour {
    [SerializeField] private float _nodAngle = 12f;
    [SerializeField] private float _nodDuration = 0.5f;
    [SerializeField] private int _nodCount = 1;

    private Transform _head;
    private Quaternion _headInitialLocalRotation;
    private float _elapsedTime;
    private bool _isPlaying;

    private void Awake() {
        _nodDuration = Mathf.Max(0.01f, _nodDuration);
        _nodCount = Mathf.Max(1, _nodCount);
        Animator animator = GetComponent<Animator>();
        _head = animator.GetBoneTransform(HumanBodyBones.Head);

        if (_head != null) {
            _headInitialLocalRotation = _head.localRotation;
        }
    }


    private void LateUpdate() {
        if (!_isPlaying || _head == null) return;

        _elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsedTime / _nodDuration);
        float cycleProgress = progress * _nodCount;
        float phase = cycleProgress - Mathf.Floor(cycleProgress);
        float angle = GetNodAngle(phase);

        _head.localRotation = _headInitialLocalRotation * Quaternion.Euler(angle, 0f, 0f);

        if (progress >= 1f) {
            _isPlaying = false;
            _head.localRotation = _headInitialLocalRotation;
        }
    }

    public void PlayNod() {
        if (_head == null) return;

        _elapsedTime = 0f;
        _headInitialLocalRotation = _head.localRotation;
        _isPlaying = true;
    }

    private float GetNodAngle(float phase) {
        if (phase < 0.5f) {
            float downProgress = EaseOut(phase / 0.5f);
            return Mathf.Lerp(0f, _nodAngle, downProgress);
        }

        float upProgress = EaseOut((phase - 0.5f) / 0.5f);
        return Mathf.Lerp(_nodAngle, 0f, upProgress);
    }

    private float EaseOut(float t) {
        return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
    }
}
