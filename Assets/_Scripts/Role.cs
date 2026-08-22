using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class Role : MonoBehaviour {
    [SerializeField] private string _roleId;
    [SerializeField] private Transform _lookTarget;
    [SerializeField] private Transform _lookPlayerTarget;
    [SerializeField] private float _lookTransitionTime = 0.25f;

    private bool _isLookingAtPlayer;
    public bool IsLookingAtPlayer => _isLookingAtPlayer;

    private Animator _animator;
    private LookAction _lookAction;
    private NodAnimation _nodAnimation;
    private Vector3 _currentLookPosition;
    private Vector3 _lookVelocity;
    private bool _hasLookPosition;
    public string RoleId => string.IsNullOrWhiteSpace(_roleId) ? gameObject.name : _roleId;

    private void Awake() {
        _animator = GetComponent<Animator>();
        _lookAction = GetComponent<LookAction>();
        _nodAnimation = GetComponent<NodAnimation>();
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

    public void PlayNod() {
        _nodAnimation?.PlayNod();
    }

    public void PlayClap() {
        if (_animator != null) {
            _animator.SetTrigger("Clap");
        }
    }

    public void PromptLookAtPlayer(float duration) {
        _lookAction?.LookAtPlayer(duration);
    }

    public void CancelLookPrompt() {
        if (_lookAction != null) {
            _lookAction.StopLookingAtPlayer();
        }
        else {
            SetLookAtPlayer(false);
        }
    }

    public void SetLookAtPlayer(bool lookAtPlayer) {
        if (_isLookingAtPlayer == lookAtPlayer) return;

        _isLookingAtPlayer = lookAtPlayer;
        _lookVelocity = Vector3.zero;
    }
}
