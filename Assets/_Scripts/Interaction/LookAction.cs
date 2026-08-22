using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Role))]
public sealed class LookAction : MonoBehaviour {
    private Role _role;
    private Coroutine _lookCoroutine;

    private void Awake() {
        _role = GetComponent<Role>();
    }

    public void LookAtPlayer(float duration) {
        StopLookingAtPlayer();

        _lookCoroutine = StartCoroutine(LookAtPlayerRoutine(duration));
    }

    public void StopLookingAtPlayer() {
        if (_lookCoroutine != null) {
            StopCoroutine(_lookCoroutine);
            _lookCoroutine = null;
        }
        if (_role != null) {
            _role.SetLookAtPlayer(false);
        }
    }

    private void OnDisable() {
        StopLookingAtPlayer();
    }

    private IEnumerator LookAtPlayerRoutine(float duration) {
        _role.SetLookAtPlayer(true);
        yield return new WaitForSeconds(duration);
        _role.SetLookAtPlayer(false);
        _lookCoroutine = null;
    }
}
