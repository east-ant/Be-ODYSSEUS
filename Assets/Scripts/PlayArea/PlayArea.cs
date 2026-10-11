using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 정면 기준점 앞에 펼쳐진 몬스터 활동 범위.
    /// 위치는 "정면에서 좌우로 몇 도, 위아래로 몇 도"로 다루고, 크기도 각도로 정해서 방 크기와 상관없이 난이도가 같다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PlayArea : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;

        private LineRenderer _frame;
        private Pose _front;
        private float _distanceOverride;

        public bool IsSet { get; private set; }
        /// <summary>방향을 정할 때의 카메라 위치와 겨눈 방향(롤 제외).</summary>
        public Pose Front => _front;
        public Vector2 HalfSizeDegrees => _config.AreaHalfSizeDegrees;
        /// <summary>몬스터가 떠 있는 거리(m). 스테이지마다 바꿀 수 있고(SetDistance), 정하지 않으면 설정의 기본 거리.</summary>
        public float DistanceMeters => _distanceOverride > 0f ? _distanceOverride : _config.AreaDistanceMeters;

        /// <summary>이 스테이지 몬스터 거리. 0 이하면 기본 거리로 돌아간다. 각도 범위는 그대로라 가까울수록 몬스터만 크게 보인다.</summary>
        public void SetDistance(float meters) => _distanceOverride = meters;

        private void Awake()
        {
            _frame = GetComponent<LineRenderer>();
            _frame.enabled = false;
        }

        public void SetFront(Pose front)
        {
            _front = front;
            IsSet = true;
            DrawFrame();
        }

        /// <summary>범위는 그대로 두고 테두리만 숨긴다. 방향을 정할 때만 보여 주고 스테이지에서는 숨긴다.</summary>
        public void HideFrame() => _frame.enabled = false;

        public void Clear()
        {
            IsSet = false;
            _distanceOverride = 0f;
            _frame.enabled = false;
        }

        /// <summary>정면에서 (x: 오른쪽 +, y: 위쪽 +) 도만큼 떨어진 방향으로, 활동 범위 거리만큼 떨어진 월드 위치.</summary>
        public Vector3 GetWorldPoint(Vector2 angles)
        {
            Quaternion offset = Quaternion.Euler(-angles.y, angles.x, 0f);
            return _front.position + _front.rotation * offset * Vector3.forward * DistanceMeters;
        }

        private void DrawFrame()
        {
            Vector2 h = HalfSizeDegrees;
            Vector2[] corners = { new(-h.x, h.y), new(h.x, h.y), new(h.x, -h.y), new(-h.x, -h.y) };
            _frame.useWorldSpace = true;
            _frame.loop = true;
            _frame.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++) _frame.SetPosition(i, GetWorldPoint(corners[i]));
            _frame.enabled = true;
        }
    }
}
