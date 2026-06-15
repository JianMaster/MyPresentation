using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform _cameraRoot;
    [SerializeField] private float _moveSpeed = 3.5f;
    [SerializeField] private float _lookSensitivity = 0.12f;

    private float _pitch;

    private void Awake()
    {
        if (_cameraRoot == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                _cameraRoot = mainCamera.transform;
            }
        }
    }

    private void Update()
    {
        HandleMove();
        HandleLook();
    }

    private void HandleLook()
    {
        if (_cameraRoot == null || Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * _lookSensitivity;

        transform.Rotate(Vector3.up, mouseDelta.x);
        _pitch = Mathf.Clamp(_pitch - mouseDelta.y, -80f, 80f);
        _cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void HandleMove()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 move = transform.forward * input.y + transform.right * input.x;
        transform.position += move * (_moveSpeed * Time.deltaTime);
    }
}
