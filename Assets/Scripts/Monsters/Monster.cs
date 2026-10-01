using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 2D 이미지 몬스터. 항상 플레이어 쪽을 바라보고(빌보드), 활동 범위 안을 천천히 돌아다닌다.
    /// 위치는 활동 범위 기준 각도로 들고 있어서, 정면을 다시 정해도 같은 규칙으로 움직인다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Monster : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;

        private SpriteRenderer _renderer;
        private PlayArea _area;
        private Transform _viewer;
        private Vector2 _angles;
        private Vector2 _target;
        private float _pauseLeft;

        /// <summary>정면 기준 현재 각도(x: 오른쪽 +, y: 위쪽 +).</summary>
        public Vector2 Angles => _angles;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Init(PlayArea area, Transform viewer, Vector2 startAngles)
        {
            _area = area;
            _viewer = viewer;
            _angles = startAngles;
            _target = startAngles;
            _pauseLeft = _config.MonsterPauseMinSeconds;

            float spriteHeight = _renderer.sprite.bounds.size.y;
            transform.localScale = Vector3.one * (_config.MonsterHeightMeters / spriteHeight);
            UpdatePose();
        }

        private void Update()
        {
            if (_area == null || !_area.IsSet) return;
            Wander(Time.deltaTime);
            UpdatePose();
        }

        private void Wander(float dt)
        {
            if (_pauseLeft > 0f)
            {
                _pauseLeft -= dt;
                return;
            }

            _angles = Vector2.MoveTowards(_angles, _target, _config.MonsterMoveSpeedDegrees * dt);
            if (_angles != _target) return;

            _pauseLeft = Random.Range(_config.MonsterPauseMinSeconds, _config.MonsterPauseMaxSeconds);
            _target = _area.RandomAngles(HalfSizeDegrees());
        }

        private void UpdatePose()
        {
            transform.position = _area.GetWorldPoint(_angles);

            // 위아래로는 세운 채 좌우로만 돌려 플레이어를 바라보게 한다.
            Vector3 away = transform.position - _viewer.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        /// <summary>몬스터 크기의 절반이 정면에서 몇 도를 차지하는지. 범위 밖으로 삐져나가지 않게 하는 여유로 쓴다.</summary>
        private Vector2 HalfSizeDegrees()
        {
            Vector2 halfMeters = (Vector2)_renderer.sprite.bounds.extents * transform.localScale.x;
            float distance = _area.DistanceMeters;
            return new Vector2(Mathf.Atan2(halfMeters.x, distance), Mathf.Atan2(halfMeters.y, distance)) * Mathf.Rad2Deg;
        }
    }
}
