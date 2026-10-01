using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace BeOdysseus
{
    /// <summary>
    /// 에디터 전용 조준 흉내. 에디터에는 AR이 없으니 마우스 오른쪽 버튼을 누른 채 움직여 카메라(= 활) 방향을 돌린다.
    /// 폰 빌드에서는 스스로 꺼지고, 게임 코드는 어느 쪽이든 카메라 방향만 읽는다.
    /// </summary>
    public class EditorAimSimulator : MonoBehaviour
    {
        [SerializeField] private float _degreesPerPixel = 0.1f;
        [SerializeField] private float _maxPitchDegrees = 80f;

        private float _yaw;
        private float _pitch;

        public static bool IsActive { get; private set; }

        private void Awake()
        {
            if (!Application.isEditor)
            {
                enabled = false;
                return;
            }

            // 에디터에는 AR 추적값이 없으니 AR 카메라 추적을 끄고 직접 돌린다.
            if (TryGetComponent(out TrackedPoseDriver driver)) driver.enabled = false;
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = Mathf.DeltaAngle(0f, euler.x);
        }

        private void OnEnable() => IsActive = true;
        private void OnDisable() => IsActive = false;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed) return;

            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * _degreesPerPixel;
            _pitch = Mathf.Clamp(_pitch - delta.y * _degreesPerPixel, -_maxPitchDegrees, _maxPitchDegrees);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
