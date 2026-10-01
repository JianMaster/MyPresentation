using UnityEngine;

namespace PresentationRewrite {
    [RequireComponent(typeof(Animator))]
    public sealed class AudienceRole : MonoBehaviour {
        [SerializeField, Range(0, 31)] private int _highlightLayer = 6;
        [SerializeField, Min(0.01f)] private float _lookTransition = 0.25f;
        private Animator _animator;
        private Transform _target;
        private Transform _head;
        private Quaternion _headRotation;
        private float _nodTime = -1f;
        private Transform[] _parts;
        private int[] _layers;
        private float _lookWeight;
        private Vector3 _lookPosition;
        private Vector3 _lookVelocity;

        private void Awake() {
            _animator = GetComponent<Animator>();
            if (_animator.isHuman) _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _parts = GetComponentsInChildren<Transform>(true);
            _layers = new int[_parts.Length];
            for (int i = 0; i < _parts.Length; i++) _layers[i] = _parts[i].gameObject.layer;
            _lookPosition = transform.position + transform.forward * 2f + Vector3.up;
        }

        public void LookAt(Transform target) {
            _target = target;
            // Reuse the existing RoleOutline layer, renderer feature and shaders.
            if (_parts == null) return;
            for (int i = 0; i < _parts.Length; i++) {
                if (_parts[i] != null) _parts[i].gameObject.layer = target != null ? _highlightLayer : _layers[i];
            }
        }
        public void Clap() => _animator.SetTrigger("Clap");

        public void Nod() {
            if (_head == null || _nodTime >= 0) return;
            _headRotation = _head.localRotation;
            _nodTime = 0;
        }

        private void OnAnimatorIK(int layerIndex) {
            _animator.SetLookAtWeight(_lookWeight, 0f, 1f, 0f, 0.6f);
            _animator.SetLookAtPosition(_lookPosition);
        }

        private void Update() {
            _lookWeight = Mathf.MoveTowards(_lookWeight, _target == null ? 0f : 0.8f, Time.deltaTime / Mathf.Max(0.01f, _lookTransition));
            if (_target != null) {
                _lookPosition = Vector3.SmoothDamp(_lookPosition, _target.position, ref _lookVelocity, _lookTransition);
            }
        }

        private void LateUpdate() {
            if (_nodTime < 0) return;
            _nodTime += Time.deltaTime;
            float progress = Mathf.Clamp01(_nodTime / 0.5f);
            _head.localRotation = _headRotation * Quaternion.Euler(12f * Mathf.Sin(progress * Mathf.PI), 0, 0);
            if (progress >= 1f) {
                _head.localRotation = _headRotation;
                _nodTime = -1f;
            }
        }

        private void OnDisable() {
            LookAt(null);
            _lookWeight = 0;
            _lookVelocity = Vector3.zero;
            if (_nodTime >= 0 && _head != null) _head.localRotation = _headRotation;
            _nodTime = -1f;
        }
    }
}
