using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace BeOdysseus
{
    /// <summary>
    /// 에디터 전용 조준 흉내. 에디터에는 AR이 없으니 카메라(= 활) 방향을 직접 돌린다.
    /// - 키보드 방향키: 누르고 있는 동안 그 방향으로 돌아간다.
    /// - 마우스 오른쪽 버튼을 누른 채 움직이기.
    /// 폰 빌드에서는 스스로 꺼지고, 게임 코드는 어느 쪽이든 카메라 방향만 읽는다.
    /// </summary>
    public class EditorAimSimulator : MonoBehaviour
    {
        [SerializeField] private float _degreesPerPixel = 0.1f;
        [Tooltip("방향키를 누르고 있을 때 도는 속도(도/초).")]
        [SerializeField] private float _keyDegreesPerSecond = 20f;
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
            // 오른쪽·위쪽이 + 인 회전량(도).
            Vector2 turn = KeyTurn() * (_keyDegreesPerSecond * Time.deltaTime) + MouseTurn();
            if (turn == Vector2.zero) return;

            _yaw += turn.x;
            _pitch = Mathf.Clamp(_pitch - turn.y, -_maxPitchDegrees, _maxPitchDegrees);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private static Vector2 KeyTurn()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            var turn = Vector2.zero;
            if (keyboard.leftArrowKey.isPressed) turn.x -= 1f;
            if (keyboard.rightArrowKey.isPressed) turn.x += 1f;
            if (keyboard.downArrowKey.isPressed) turn.y -= 1f;
            if (keyboard.upArrowKey.isPressed) turn.y += 1f;
            return turn;
        }

        private Vector2 MouseTurn()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed) return Vector2.zero;
            return mouse.delta.ReadValue() * _degreesPerPixel;
        }
    }
}
