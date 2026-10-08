using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace BeOdysseus
{
    /// <summary>
    /// 튜토리얼에서 "방향 정하기" 버튼을 누르거나 활을 쏘면, 그때 겨누고 있던 방향을 "정면 기준점(중앙점)"으로 만든다.
    /// 화면을 누르는 손에 폰이 살짝 흔들리므로, 누르기 조금 전(설정값)의 조준 방향을 쓴다.
    /// 활이 옆으로 기운 것(롤)은 조준점을 움직이지 않으므로 정면 방향에서 뺀다.
    /// </summary>
    public class AimCalibrator : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private AimHistory _aimHistory;
        [Tooltip("조준 방향을 주는 AR 카메라. 기록이 없을 때 지금 방향을 쓴다.")]
        [SerializeField] private Transform _aim;

        /// <summary>버튼을 눌렀을 때. 위치는 카메라 위치, 회전은 겨눈 방향(롤 제외).</summary>
        public Pose CaptureFront() => CaptureFrontAt(InputState.currentTime - _config.CenterCaptureLookbackSeconds);

        /// <summary>time(입력 시각) 때 겨누고 있던 방향을 정면으로 만든다. 활을 쏴서 정할 때는 충격 직전 시각을 넘긴다.</summary>
        public Pose CaptureFrontAt(double time)
        {
            Vector3 position = _aim.position;
            Vector3 forward = _aim.forward;
            if (_aimHistory.TryGetAtOrBefore(time, out AimSample sample))
            {
                position = sample.Position;
                forward = sample.Forward;
            }

            var front = new Pose(position, Quaternion.LookRotation(forward, Vector3.up));
            Vector3 euler = front.rotation.eulerAngles;
            Debug.Log($"[Calibration] front pos={front.position:F2} yaw={euler.y:F1} pitch={-Mathf.DeltaAngle(0f, euler.x):F1}");
            return front;
        }
    }
}
