using System.Collections;
using UnityEngine;

public class LookAction : MonoBehaviour {
    private Role _role;
    private Coroutine _lookCoroutine;

    public void Init(Role role) {
        _role = role;
    }

    public void LookAtPlayer(float duration, float responseTime) {
        if (_lookCoroutine != null) return;

        _lookCoroutine = StartCoroutine(LookAtPlayerRoutine(duration, responseTime));
    }

    private IEnumerator LookAtPlayerRoutine(float duration, float responseTime) {
        yield return new WaitForSeconds(Random.Range(0f, responseTime));
        _role.IslookingAtPlayer = true;
        yield return new WaitForSeconds(duration);
        _role.IslookingAtPlayer = false;
        _lookCoroutine = null;
    }
}
