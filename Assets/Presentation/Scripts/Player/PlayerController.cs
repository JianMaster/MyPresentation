using UnityEngine;
using UnityEngine.InputSystem;

namespace PresentationRewrite {
    public sealed class PlayerController : MonoBehaviour {
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private float _moveSpeed = 3.5f;
        [SerializeField] private float _sensitivity = 0.12f;
        private float _pitch;

        private void Update() {
            var keyboard = Keyboard.current;
            if (keyboard != null) {
                var input = new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                input = Vector2.ClampMagnitude(input, 1f);
                transform.position += (transform.right * input.x + transform.forward * input.y) * (_moveSpeed * Time.deltaTime);
            }
            if (_cameraRoot == null || Mouse.current == null) return;
            var delta = Mouse.current.delta.ReadValue() * _sensitivity;
            transform.Rotate(Vector3.up, delta.x);
            _pitch = Mathf.Clamp(_pitch - delta.y, -80f, 80f);
            _cameraRoot.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }
    }
}
