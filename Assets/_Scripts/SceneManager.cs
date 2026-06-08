using UnityEngine;
using UnityEngine.InputSystem;

public class SceneManager : MonoBehaviour {
    public Role _role;

    private void Awake() {

    }

    private void Update() {
        if (Keyboard.current.spaceKey.wasPressedThisFrame) {
            _role.PlayNod();
        }
    }
}
