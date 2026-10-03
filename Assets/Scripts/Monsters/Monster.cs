using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 2D 이미지 몬스터. 항상 플레이어 쪽을 바라보고(빌보드), 몬스터 종류에 따라 활동 범위 안을 움직인다.
    /// - Walk: 가운데 줄을 따라 좌우로 걷다가 아무 데서나 멈춰 잠시 대기 애니메이션을 하고, 다시 아무 방향으로 걷는다.
    ///   한 번 멈춘 뒤에는 최소 몇 초(설정값) 동안 다시 멈추지 않는다. 좌우 끝에 닿으면 멈추지 않고 돌아선다.
    /// - Float: 좌우로 떠다니면서 위아래로도 오르내린다. 멈추지 않는다.
    /// 맞으면 체력이 1 줄어든다. 체력이 남아 있으면 잠깐 번쩍이며 움찔하고(멈춤 + 흔들림, 맞는 그림이 있으면 재생),
    /// 0이 되면 번쩍인 뒤 (쓰러지는 그림이 있으면 먼저 재생하고) 옆으로 쓰러지고, 누운 채 서서히 사라진다.
    /// 쓰러지는 그림에 누운 모습까지 그려져 있으면 옆으로 넘어뜨리지 않고 마지막 그림으로 잠시 있다가 사라진다.
    /// 위치는 활동 범위 기준 각도로 들고 있다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Monster : MonoBehaviour
    {
        private enum Motion { Walking, Resting, Dying, Dead }

        // 쓰러지는 동안의 구간(쓰러지는 시간 대비 비율): 맞은 순간 번쩍 → 옆으로 넘어짐 → 누운 채 사라짐
        private const float FlashEnd = 0.12f;
        private const float FallEnd = 0.6f;
        // 다 넘어졌을 때 기운 각도(도). 90도면 완전히 누운 모습.
        private const float FallenAngle = 88f;
        private static readonly Color HitTint = new(1f, 0.4f, 0.4f);
        // 맞았지만 쓰러지지 않을 때: 이 시간(초) 동안 멈춰서 좌우로 흔들린다. 흔들림 폭(도)은 점점 줄어든다.
        // 맞는 그림이 있으면 그 그림을 한 번 다 트는 시간만큼 움찔한다.
        private const float HurtSeconds = 0.35f;
        private const float HurtShakeDegrees = 1.2f;
        private const float HurtShakeCycles = 3f;
        // 움찔할 때 보여 줄 쓰러짐 그림의 프레임(두 번째 = 팔을 벌리며 맞는 자세).
        private const int HurtPoseFrame = 1;

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
        private float _angleY;
        private float _floatPhase;
        private int _direction;
        private Motion _motion;
        private float _restLeft;
        private float _walkLeftBeforeRest;
        private float _frameTime;
        private float _deathTime;
        private float _fallSign;
        private float _fallAngle;
        private int _health;
        private float _hurtSeconds;
        private float _hurtLeft;
        private float _shakeDegrees;

        /// <summary>정면 기준 현재 각도(x: 오른쪽 +, y: 위쪽 +).</summary>
        public Vector2 Angles => new(_angleX, _angleY);

        /// <summary>남은 체력(쓰러뜨리려면 더 맞혀야 하는 횟수).</summary>
        public int Health => _health;

        /// <summary>명중해서 쓰러지는 중인지. 다 쓰러져 사라지면 false.</summary>
        public bool IsDying => _motion == Motion.Dying;

        private bool IsFloating => _animation != null && _animation.Movement == MonsterMovement.Float;
        private bool HasHitFrames => _animation != null && _animation.HasHitFrames;
        private bool HasDeathFrames => _animation != null && _animation.HasDeathFrames;

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
        /// 활동 범위 아무 곳에 몬스터를 세우고 움직이기 시작한다.
        /// animation이 있으면 그 애니메이션과 이동 방식을, 없으면 staticSprite 한 장으로 걷는다.
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
            _health = animation != null ? animation.Health : 1;
            _hurtLeft = 0f;
            _shakeDegrees = 0f;

            // 크기는 기준 그림의 키로 정해서, 프레임이 바뀌어도 몬스터 크기가 일정하다.
            Bounds reference = _referenceSprite.bounds;
            float scale = _config.MonsterHeightMeters / reference.size.y;
            transform.localScale = Vector3.one * scale;
            // 그림의 기준점이 발밑이든 가운데든, 몸 가운데가 정해진 높이에 오게 맞춘다.
            _verticalOffsetMeters = -reference.center.y * scale;

            float limit = HorizontalLimitDegrees();
            _angleX = Random.Range(-limit, limit);
            _floatPhase = Random.Range(0f, 2f * Mathf.PI);
            _angleY = IsFloating ? FloatAngleY() : 0f;
            _direction = Random.value < 0.5f ? -1 : 1;
            StartWalking();
            UpdatePose();
        }

        /// <summary>
        /// 화살에 맞았을 때. 체력을 1 깎고, 0이 되면 쓰러지기 시작한다.
        /// </summary>
        /// <returns>이 발로 쓰러졌으면 true.</returns>
        public bool TakeHit(Vector3 hitPoint)
        {
            if (_motion is Motion.Dying or Motion.Dead) return false;
            _health--;
            if (_health <= 0)
            {
                Die(hitPoint);
                return true;
            }

            _hurtSeconds = HasHitFrames ? _animation.HitFrames.Length / _animation.HitFramesPerSecond : HurtSeconds;
            _hurtLeft = _hurtSeconds;
            return false;
        }

        /// <summary>쓰러진다. 맞은 쪽 반대로 넘어진다(왼쪽을 맞으면 오른쪽으로). 다 쓰러지면 스스로 꺼진다.</summary>
        private void Die(Vector3 hitPoint)
        {
            _hurtLeft = 0f;
            _shakeDegrees = 0f;
            Vector3 local = transform.InverseTransformPoint(hitPoint);
            // 기울기 각도가 +면 화면에서 왼쪽으로, -면 오른쪽으로 넘어진다.
            _fallSign = local.x <= 0f ? -1f : 1f;
            if (HasDeathFrames) _renderer.flipX = false; // 쓰러지는 그림은 그려진 방향 그대로 보여 준다
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
            else if (_hurtLeft > 0f)
            {
                TickHurt(dt);
            }
            else
            {
                Move(dt);
                Animate(dt);
            }
            if (gameObject.activeSelf) UpdatePose();
        }

        /// <summary>
        /// 맞았지만 쓰러지지 않았을 때: 멈춰서 빨갛게 번쩍이고 좌우로 흔들린다. 끝나면 하던 움직임을 이어 간다.
        /// 맞는 그림이 있으면 그 그림을 한 번 틀고, 없으면 쓰러지는 그림 한 장(맞는 자세)을 잠깐 보여 준다.
        /// </summary>
        private void TickHurt(float dt)
        {
            _hurtLeft = Mathf.Max(0f, _hurtLeft - dt);
            float t = 1f - _hurtLeft / _hurtSeconds;

            _renderer.color = Color.Lerp(HitTint, Color.white, t);
            _shakeDegrees = Mathf.Sin(t * HurtShakeCycles * 2f * Mathf.PI) * HurtShakeDegrees * (1f - t);
            if (_hurtLeft <= 0f)
            {
                if (HasHitFrames || HasDeathFrames) _renderer.sprite = _referenceSprite;
                return;
            }

            if (HasHitFrames)
            {
                Sprite[] frames = _animation.HitFrames;
                int index = Mathf.Min((int)((_hurtSeconds - _hurtLeft) * _animation.HitFramesPerSecond), frames.Length - 1);
                _renderer.sprite = frames[index];
            }
            else if (HasDeathFrames && _animation.DeathFrames.Length > HurtPoseFrame)
            {
                _renderer.sprite = _animation.DeathFrames[HurtPoseFrame];
            }
        }

        /// <summary>
        /// 쓰러지는 순서: [쓰러지는 그림 재생(없으면 짧게 번쩍)] → [발밑을 축으로 옆으로 넘어짐] → [누운 채 사라짐].
        /// 쓰러지는 그림에 누운 모습까지 그려져 있으면 넘어지는 대신 그 시간만큼 마지막 그림으로 가만히 있는다.
        /// 맞은 직후 잠깐은 빨갛게 번쩍인다.
        /// </summary>
        private void TickDeath(float dt)
        {
            _deathTime += dt;
            float total = _config.MonsterDeathSeconds;
            float flashSeconds = FlashEnd * total;
            float fallSeconds = (FallEnd - FlashEnd) * total;
            float fadeSeconds = (1f - FallEnd) * total;
            float motionSeconds = flashSeconds;

            if (HasDeathFrames)
            {
                Sprite[] frames = _animation.DeathFrames;
                motionSeconds = frames.Length / _animation.DeathFramesPerSecond;
                _renderer.sprite = frames[Mathf.Min((int)(_deathTime * _animation.DeathFramesPerSecond), frames.Length - 1)];
            }

            // 처음엔 천천히, 갈수록 빨리 넘어진다(떨어지는 느낌).
            bool drawnFall = HasDeathFrames && _animation.DeathFramesShowFall;
            float fall = drawnFall ? 0f : Mathf.Clamp01((_deathTime - motionSeconds) / fallSeconds);
            _fallAngle = _fallSign * FallenAngle * fall * fall;

            Color color = _deathTime < flashSeconds ? HitTint : Color.white;
            color.a = 1f - Mathf.Clamp01((_deathTime - motionSeconds - fallSeconds) / fadeSeconds);
            _renderer.color = color;

            if (_deathTime < motionSeconds + fallSeconds + fadeSeconds) return;
            _motion = Motion.Dead;
            gameObject.SetActive(false);
        }

        private void Move(float dt)
        {
            if (_motion == Motion.Resting)
            {
                _restLeft -= dt;
                if (_restLeft > 0f) return;
                _direction = Random.value < 0.5f ? -1 : 1; // 쉬고 나면 아무 방향으로나 다시 걷는다
                StartWalking();
                return;
            }

            if (IsFloating)
            {
                _floatPhase += dt * 2f * Mathf.PI / _animation.FloatCycleSeconds;
                _angleY = FloatAngleY();
            }

            float limit = HorizontalLimitDegrees();
            _angleX += _direction * _config.MonsterMoveSpeedDegrees * dt;
            if (Mathf.Abs(_angleX) >= limit)
            {
                // 좌우 끝에 닿으면 멈추지 않고 바로 돌아선다.
                _angleX = Mathf.Clamp(_angleX, -limit, limit);
                _direction = _angleX > 0f ? -1 : 1;
            }

            if (IsFloating) return;
            _walkLeftBeforeRest -= dt;
            if (_walkLeftBeforeRest <= 0f) StartResting();
        }

        private float FloatAngleY() => VerticalLimitDegrees() * _animation.FloatHeight01 * Mathf.Sin(_floatPhase);

        private void StartWalking()
        {
            _motion = Motion.Walking;
            _frameTime = 0f;
            // 다음에 멈출 때까지 걸을 시간. 최소값이 있어서 멈춘 직후 바로 또 멈추지는 않는다.
            _walkLeftBeforeRest = Random.Range(_config.MonsterRestIntervalMinSeconds, _config.MonsterRestIntervalMaxSeconds);
        }

        private void StartResting()
        {
            _motion = Motion.Resting;
            _restLeft = _config.MonsterRestSeconds;
            _frameTime = 0f;
        }

        private void Animate(float dt)
        {
            if (_animation == null) return;

            bool walking = _motion == Motion.Walking;
            Sprite[] frames = walking && _animation.WalkFrames is { Length: > 0 } ? _animation.WalkFrames : _animation.IdleFrames;
            if (frames == null || frames.Length == 0) return;

            _frameTime += dt;
            float fps = frames == _animation.WalkFrames ? _animation.WalkFramesPerSecond : _animation.IdleFramesPerSecond;
            _renderer.sprite = frames[(int)(_frameTime * fps) % frames.Length];
            // 가는 방향과 그림의 방향이 다르면 좌우를 뒤집는다. 대기 중에는 그대로.
            _renderer.flipX = walking && (_direction < 0) == _animation.WalkFacesRight;
        }

        private void UpdatePose()
        {
            transform.position = _area.GetWorldPoint(new Vector2(_angleX + _shakeDegrees, _angleY)) + Vector3.up * _verticalOffsetMeters;

            // 위아래로는 세운 채 좌우로만 돌려 플레이어를 바라보게 한다. 쓰러지는 중이면 그만큼 옆으로 기울인다.
            Vector3 away = transform.position - _viewer.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(away, Vector3.up) * Quaternion.Euler(0f, 0f, _fallAngle);
        }

        /// <summary>몬스터가 범위 밖으로 삐져나가지 않고 움직일 수 있는 좌우 최대 각도.</summary>
        private float HorizontalLimitDegrees()
        {
            float halfWidthMeters = _referenceSprite.bounds.extents.x * transform.localScale.x;
            return Mathf.Max(0f, _area.HalfSizeDegrees.x - MetersToDegrees(halfWidthMeters));
        }

        /// <summary>몬스터가 범위 밖으로 삐져나가지 않고 움직일 수 있는 위아래 최대 각도.</summary>
        private float VerticalLimitDegrees()
        {
            float halfHeightMeters = _referenceSprite.bounds.extents.y * transform.localScale.y;
            return Mathf.Max(0f, _area.HalfSizeDegrees.y - MetersToDegrees(halfHeightMeters));
        }

        private float MetersToDegrees(float meters) => Mathf.Atan2(meters, _area.DistanceMeters) * Mathf.Rad2Deg;
    }
}
