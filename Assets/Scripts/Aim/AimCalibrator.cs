using System;
using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 사용자가 쏠 방향을 정해진 시간 동안 가만히 겨누면, 그 방향을 "정면 기준점"으로 정한다.
    /// 활이 옆으로 기운 것(롤)은 조준점을 움직이지 않으므로, 흔들림 계산과 정면 방향에서 모두 뺀다.
    /// </summary>
    public class AimCalibrator : MonoBehaviour
    {
        // 프레임마다 잰 회전 속도는 튀기 쉬워서 이 시간(초) 정도로 부드럽게 평균 낸다.
        private const float SpeedSmoothingSeconds = 0.2f;

        [SerializeField] private GameConfig _config;
        [Tooltip("조준 방향을 주는 AR 카메라.")]
        [SerializeField] private Transform _aim;

        private bool _isRunning;
        private float _heldSeconds;
        private float _smoothedSpeed;
        private Vector3 _lastForward;

        /// <summary>정면이 정해졌을 때. 위치는 카메라 위치, 회전은 겨눈 방향(롤 제외).</summary>
        public event Action<Pose> Calibrated;

        public bool IsRunning => _isRunning;
        public float Progress01 => _isRunning ? Mathf.Clamp01(_heldSeconds / _config.CalibrationHoldSeconds) : 0f;

        public void Begin()
        {
            _isRunning = true;
            _heldSeconds = 0f;
            _smoothedSpeed = 0f;
            _lastForward = _aim.forward;
        }

        public void Stop() => _isRunning = false;

        private void Update()
        {
            if (!_isRunning) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 forward = _aim.forward;
            float speed = Vector3.Angle(_lastForward, forward) / dt;
            _lastForward = forward;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, 1f - Mathf.Exp(-dt / SpeedSmoothingSeconds));

            if (_smoothedSpeed > _config.CalibrationMaxAngularSpeed)
            {
                _heldSeconds = 0f;
                return;
            }

            _heldSeconds += dt;
            if (_heldSeconds < _config.CalibrationHoldSeconds) return;

            _isRunning = false;
            var front = new Pose(_aim.position, Quaternion.LookRotation(forward, Vector3.up));
            Vector3 euler = front.rotation.eulerAngles;
            Debug.Log($"[Calibration] front pos={front.position:F2} yaw={euler.y:F1} pitch={-Mathf.DeltaAngle(0f, euler.x):F1}");
            Calibrated?.Invoke(front);
        }
    }
}
