using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 2D 이미지 몬스터. 항상 플레이어 쪽을 바라보고(빌보드), 활동 범위의 가운데 줄을 따라 좌우로만 걷는다.
    /// 좌우 끝에 닿으면 잠시(설정값) 멈춰 대기 애니메이션을 보여 준 뒤 방향을 바꾼다.
    /// 명중하면 번쩍인 뒤 옆으로 쓰러지고, 누운 채 서서히 사라진다.
    /// 위치는 활동 범위 기준 각도로 들고 있다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Monster : MonoBehaviour
    {
        private enum Motion { Walking, Resting, Dying, Dead }

        // 쓰러지는 동안의 구간(전체 시간 대비 비율): 맞은 순간 번쩍 → 옆으로 넘어짐 → 누운 채 사라짐
        private const float FlashEnd = 0.12f;
        private const float FallEnd = 0.6f;
        // 다 넘어졌을 때 기운 각도(도). 90도면 완전히 누운 모습.
        private const float FallenAngle = 88f;
        private static readonly Color HitTint = new(1f, 0.4f, 0.4f);

        [SerializeField] private GameConfig _config;
        [Tooltip("정확도 계산의 기준점. 이미지 안에서의 비율 위치(0~1). 기본은 정가운데. 몸통이나 약점으로 옮길 수 있다.")]
        [SerializeField] private Vector2 _targetPointNormalized = new(0.5f, 0.5f);

        private SpriteRenderer _renderer;
        private PlayArea _area;
        private Transform _viewer;
        private MonsterAnimationSet _animation;
        private Sprite _referenceSprite;
        private float _verticalOffsetMeters;
        private float _angleX;
        private int _direction;
        private Motion _motion;
        private float _restLeft;
        private float _frameTime;
        private float _deathTime;
        private float _fallSign;
        private float _fallAngle;

        /// <summary>정면 기준 현재 각도(x: 오른쪽 +, y: 위쪽 +). 가운데 줄을 걸으므로 y는 늘 0.</summary>
        public Vector2 Angles => new(_angleX, 0f);

        /// <summary>명중해서 쓰러지는 중인지. 다 쓰러져 사라지면 false.</summary>
        public bool IsDying => _motion == Motion.Dying;

        /// <summary>정확도 계산 기준점의 월드 위치.</summary>
        public Vector3 TargetPoint
        {
            get
            {
                Bounds b = _renderer.sprite.bounds;
                Vector3 local = b.min + Vector3.Scale(b.size, _targetPointNormalized);
                if (_renderer.flipX) local.x = -local.x;
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

        /// <summary>몬스터 평면 위의 점이 지금 보이는 그림의 사각형 안에 있는지.</summary>
        public bool Contains(Vector3 pointOnPlane)
        {
            Vector3 local = transform.InverseTransformPoint(pointOnPlane);
            if (_renderer.flipX) local.x = -local.x; // 좌우를 뒤집어 그릴 때는 기준점을 중심으로 거울처럼 뒤집힌다
            Bounds b = _renderer.sprite.bounds;
            return local.x >= b.min.x && local.x <= b.max.x && local.y >= b.min.y && local.y <= b.max.y;
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 활동 범위의 가운데 줄 아무 곳에 몬스터를 세우고 걷기 시작한다.
        /// animation이 있으면 대기·걷기 애니메이션을, 없으면 staticSprite 한 장을 쓴다.
        /// </summary>
        public void Init(PlayArea area, Transform viewer, Sprite staticSprite, MonsterAnimationSet animation)
        {
            _area = area;
            _viewer = viewer;
            _animation = animation;
            _referenceSprite = animation != null && animation.ReferenceFrame != null ? animation.ReferenceFrame : staticSprite;
            _renderer.sprite = _referenceSprite;
            _renderer.flipX = false;
            _renderer.color = Color.white;
            _fallAngle = 0f;

            // 크기는 기준 그림의 키로 정해서, 프레임이 바뀌어도 몬스터 크기가 일정하다.
            Bounds reference = _referenceSprite.bounds;
            float scale = _config.MonsterHeightMeters / reference.size.y;
            transform.localScale = Vector3.one * scale;
            // 그림의 기준점이 발밑이든 가운데든, 몸 가운데가 범위의 가운데 줄에 오게 맞춘다.
            _verticalOffsetMeters = -reference.center.y * scale;

            float limit = HorizontalLimitDegrees();
            _angleX = Random.Range(-limit, limit);
            _direction = Random.value < 0.5f ? -1 : 1;
            StartWalking();
            UpdatePose();
        }

        /// <summary>명중했을 때. 맞은 쪽 반대로 쓰러진다(왼쪽을 맞으면 오른쪽으로). 다 쓰러지면 스스로 꺼진다.</summary>
        public void Die(Vector3 hitPoint)
        {
            if (_motion is Motion.Dying or Motion.Dead) return;
            Vector3 local = transform.InverseTransformPoint(hitPoint);
            // 기울기 각도가 +면 화면에서 왼쪽으로, -면 오른쪽으로 넘어진다.
            _fallSign = local.x <= 0f ? -1f : 1f;
            _motion = Motion.Dying;
            _deathTime = 0f;
        }

        private void Update()
        {
            if (_area == null || !_area.IsSet || _motion == Motion.Dead) return;
            float dt = Time.deltaTime;
            if (_motion == Motion.Dying)
            {
                TickDeath(dt);
            }
            else
            {
                Move(dt);
                Animate(dt);
            }
            if (gameObject.activeSelf) UpdatePose();
        }

        private void TickDeath(float dt)
        {
            _deathTime += dt;
            float t = Mathf.Clamp01(_deathTime / _config.MonsterDeathSeconds);
            float fall = Mathf.InverseLerp(FlashEnd, FallEnd, t);

            if (_animation != null && _animation.HasDeathFrames)
            {
                // 쓰러지는 그림이 있으면 그대로 재생하고 마지막 프레임에서 멈춘다.
                Sprite[] frames = _animation.DeathFrames;
                int index = Mathf.Min((int)(Mathf.Max(0f, _deathTime - FlashEnd * _config.MonsterDeathSeconds) * _animation.DeathFramesPerSecond), frames.Length - 1);
                _renderer.sprite = frames[index];
            }
            else
            {
                // 없으면 발밑을 축으로 옆으로 넘어뜨린다. 처음엔 천천히, 갈수록 빨리(떨어지는 느낌).
                _fallAngle = _fallSign * FallenAngle * fall * fall;
            }

            Color color = t < FlashEnd ? HitTint : Color.white;
            color.a = 1f - Mathf.InverseLerp(FallEnd, 1f, t);
            _renderer.color = color;

            if (t < 1f) return;
            _motion = Motion.Dead;
            gameObject.SetActive(false);
        }

        private void Move(float dt)
        {
            if (_motion == Motion.Resting)
            {
                _restLeft -= dt;
                if (_restLeft > 0f) return;
                _direction = -_direction;
                StartWalking();
                return;
            }

            float limit = HorizontalLimitDegrees();
            _angleX += _direction * _config.MonsterMoveSpeedDegrees * dt;
            if (Mathf.Abs(_angleX) < limit) return;

            _angleX = Mathf.Clamp(_angleX, -limit, limit);
            StartResting();
        }

        private void StartWalking()
        {
            _motion = Motion.Walking;
            _frameTime = 0f;
        }

        private void StartResting()
        {
            _motion = Motion.Resting;
            _restLeft = _config.MonsterEdgeRestSeconds;
            _frameTime = 0f;
        }

        private void Animate(float dt)
        {
            if (_animation == null) return;

            bool walking = _motion == Motion.Walking;
            Sprite[] frames = walking ? _animation.WalkFrames : _animation.IdleFrames;
            if (frames == null || frames.Length == 0) return;

            _frameTime += dt;
            float fps = walking ? _animation.WalkFramesPerSecond : _animation.IdleFramesPerSecond;
            _renderer.sprite = frames[(int)(_frameTime * fps) % frames.Length];
            // 걷는 그림의 방향과 실제로 가는 방향이 다르면 좌우를 뒤집는다. 대기 중에는 그대로.
            _renderer.flipX = walking && (_direction < 0) == _animation.WalkFacesRight;
        }

        private void UpdatePose()
        {
            transform.position = _area.GetWorldPoint(Angles) + Vector3.up * _verticalOffsetMeters;

            // 위아래로는 세운 채 좌우로만 돌려 플레이어를 바라보게 한다. 쓰러지는 중이면 그만큼 옆으로 기울인다.
            Vector3 away = transform.position - _viewer.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(away, Vector3.up) * Quaternion.Euler(0f, 0f, _fallAngle);
        }

        /// <summary>몬스터가 범위 밖으로 삐져나가지 않고 걸을 수 있는 좌우 최대 각도.</summary>
        private float HorizontalLimitDegrees()
        {
            float halfWidthMeters = _referenceSprite.bounds.extents.x * transform.localScale.x;
            float margin = Mathf.Atan2(halfWidthMeters, _area.DistanceMeters) * Mathf.Rad2Deg;
            return Mathf.Max(0f, _area.HalfSizeDegrees.x - margin);
        }
    }
}
