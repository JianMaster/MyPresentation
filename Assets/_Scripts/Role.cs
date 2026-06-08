using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Role : MonoBehaviour
{
    [SerializeField] private Transform _lookTarget;

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_lookTarget == null) return;

        _animator.SetLookAtWeight(0.8f, 0.2f, 0.8f, 0f, 0.6f);
        _animator.SetLookAtPosition(_lookTarget.position);
    }

    public void PlayNod()
    {
        if (TryGetComponent<NodAnimation>(out var nodAnimation))
        {
            nodAnimation.PlayNod();
        }
    }
}
