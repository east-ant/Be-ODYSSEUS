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

        public bool IsSet { get; private set; }
        public Vector2 HalfSizeDegrees => _config.AreaHalfSizeDegrees;
        public float DistanceMeters => _config.AreaDistanceMeters;

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
