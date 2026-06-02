using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Role : MonoBehaviour
{
    [SerializeField] private Transform lookTarget;
    [SerializeField] private float lookWeight = 0.8f;
    [SerializeField] private float bodyWeight = 0.2f;
    [SerializeField] private float headWeight = 0.8f;
    [SerializeField] private float eyesWeight = 0f;
    [SerializeField] private float clampWeight = 0.6f;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (lookTarget == null) return;

        animator.SetLookAtWeight(
            lookWeight,
            bodyWeight,
            headWeight,
            eyesWeight,
            clampWeight
        );

        animator.SetLookAtPosition(lookTarget.position);
    }
}