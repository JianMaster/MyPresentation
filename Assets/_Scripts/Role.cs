using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Role : MonoBehaviour {
    [SerializeField] private string _roleId;
    [SerializeField] private Transform _lookTarget;
    [SerializeField] private Transform _lookPlayerTarget;
    [SerializeField] private float _lookTransitionTime = 0.25f;

    private bool _isLookingAtPlayer;
    public bool IslookingAtPlayer {
        get => _isLookingAtPlayer;
        set => SetLookAtPlayer(value);
    }

    private Animator _animator;
    private LookAction _lookAction;
    private float _responseTime = 1f;
    private Vector3 _currentLookPosition;
    private Vector3 _lookVelocity;
    private bool _hasLookPosition;
    public string RoleId => string.IsNullOrWhiteSpace(_roleId) ? gameObject.name : _roleId;

    private void Awake() {
        _animator = GetComponent<Animator>();
        _lookAction = GetComponent<LookAction>();
        if (_lookAction != null) {
            _lookAction.Init(this);
        }
    }


    private void OnAnimatorIK(int layerIndex) {
        if (_lookTarget == null) return;

        Transform target = _isLookingAtPlayer && _lookPlayerTarget != null ? _lookPlayerTarget : _lookTarget;
        Vector3 targetPosition = target.position;

        if (!_hasLookPosition) {
            _currentLookPosition = targetPosition;
            _hasLookPosition = true;
        }

        _currentLookPosition = Vector3.SmoothDamp(
            _currentLookPosition,
            targetPosition,
            ref _lookVelocity,
            Mathf.Max(0.01f, _lookTransitionTime)
        );

        _animator.SetLookAtWeight(0.8f, 0f, 1f, 0f, 0.6f);
        _animator.SetLookAtPosition(_currentLookPosition);
    }

    public IEnumerator PlayNod() {
        yield return new WaitForSeconds(Random.Range(0f, _responseTime));
        if (TryGetComponent<NodAnimation>(out var nodAnimation)) {
            nodAnimation.PlayNod();
        }
    }

    public void PlayNodImmediate() {
        if (TryGetComponent<NodAnimation>(out var nodAnimation)) {
            nodAnimation.PlayNod();
        }
    }

    public void PlayClap() {
        if (_animator != null) {
            _animator.SetTrigger("Clap");
        }
    }

    public void PromptLookAtPlayer(float duration = 10f, float responseTime = 0f) {
        if (_lookAction != null) {
            _lookAction.LookAtPlayer(duration, responseTime);
        }
    }

    public void CancelLookPrompt() {
        if (_lookAction != null) {
            _lookAction.StopLookingAtPlayer();
        }
        else {
            SetLookAtPlayer(false);
        }
    }

    public void Refresh(AnalysisData analysisData) {
        PromptLookAtPlayer(10f, _responseTime);
    }

    public void SetLookAtPlayer(bool lookAtPlayer) {
        if (_isLookingAtPlayer == lookAtPlayer) return;

        _isLookingAtPlayer = lookAtPlayer;
        _lookVelocity = Vector3.zero;
    }
}
