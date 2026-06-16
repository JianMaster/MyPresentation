using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Role : MonoBehaviour {
    [SerializeField] private Transform _lookTarget;
    [SerializeField] private Transform _lookPlayerTarget;
    private bool _isLookingAtPlayer;
    public bool IslookingAtPlayer {
        get => _isLookingAtPlayer;
        set => _isLookingAtPlayer = value;
    }

    private Animator _animator;

    private void Awake() {
        _animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex) {
        if (_lookTarget == null) return;

        var target = _isLookingAtPlayer ? _lookPlayerTarget : _lookTarget;
        _animator.SetLookAtWeight(0.8f, 0.2f, 0.8f, 0f, 0.6f);
        _animator.SetLookAtPosition(target.position);
    }

    public void PlayNod() {
        if (TryGetComponent<NodAnimation>(out var nodAnimation)) {
            nodAnimation.PlayNod();
        }
    }
}
