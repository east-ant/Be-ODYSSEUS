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
        [Tooltip("정확도 계산의 기준점. 이미지 안에서의 비율 위치(0~1). 기본은 정가운데. 몸통이나 약점으로 옮길 수 있다.")]
        [SerializeField] private Vector2 _targetPointNormalized = new(0.5f, 0.5f);

        private SpriteRenderer _renderer;
        private PlayArea _area;
        private Transform _viewer;
        private Vector2 _angles;
        private Vector2 _target;
        private float _pauseLeft;

        /// <summary>정면 기준 현재 각도(x: 오른쪽 +, y: 위쪽 +).</summary>
        public Vector2 Angles => _angles;

        /// <summary>정확도 계산 기준점의 월드 위치.</summary>
        public Vector3 TargetPoint
        {
            get
            {
                Bounds b = _renderer.sprite.bounds;
                Vector3 local = b.min + Vector3.Scale(b.size, _targetPointNormalized);
                return transform.TransformPoint(new Vector3(local.x, local.y, 0f));
            }
        }

        /// <summary>조준선(시작점, 방향)이 몬스터 이미지가 놓인 평면과 만나는 점. 반대쪽을 겨누면 false.</summary>
        public bool TryIntersect(Vector3 origin, Vector3 direction, out Vector3 point)
        {
            var plane = new Plane(transform.forward, transform.position);
            if (plane.Raycast(new Ray(origin, direction), out float distance))
            {
                point = origin + direction * distance;
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>몬스터 평면 위의 점이 이미지 사각형 안에 있는지.</summary>
        public bool Contains(Vector3 pointOnPlane)
        {
            Vector3 local = transform.InverseTransformPoint(pointOnPlane);
            Bounds b = _renderer.sprite.bounds;
            return local.x >= b.min.x && local.x <= b.max.x && local.y >= b.min.y && local.y <= b.max.y;
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>활동 범위에 몬스터를 세운다. randomPosition이 false면 정면 정가운데에서 시작한다.</summary>
        public void Init(PlayArea area, Transform viewer, Sprite sprite, bool randomPosition)
        {
            if (sprite != null) _renderer.sprite = sprite;
            _area = area;
            _viewer = viewer;
            float spriteHeight = _renderer.sprite.bounds.size.y;
            transform.localScale = Vector3.one * (_config.MonsterHeightMeters / spriteHeight);

            _angles = randomPosition ? _area.RandomAngles(HalfSizeDegrees()) : Vector2.zero;
            _target = _angles;
            _pauseLeft = _config.MonsterPauseMinSeconds;
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
