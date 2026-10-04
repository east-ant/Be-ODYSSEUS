using UnityEngine;

namespace BeOdysseus
{
    public enum MonsterMovement
    {
        /// <summary>활동 범위 가운데 줄을 따라 좌우로 걷다가, 아무 데서나 잠시 멈춰 대기 애니메이션.</summary>
        Walk,
        /// <summary>좌우로 떠다니면서 위아래로도 오르내린다. 끝에서 멈추지 않고 바로 방향을 바꾼다.</summary>
        Float,
        /// <summary>한 자리에 떠서 잠시 대기 애니메이션을 하다가, 활동 범위 아무 곳(위아래 포함)으로 휙 날아간다. 이를 반복한다.</summary>
        Swoop,
    }

    /// <summary>
    /// 몬스터 한 종류의 그림과 움직이는 방식: 대기·걷기(또는 떠다니기)·맞기·쓰러지기 프레임, 재생 속도, 이동 방식.
    /// 프레임 그림은 발밑을 기준점(pivot)으로 잘라 두어, 프레임이 바뀌어도 발 위치가 흔들리지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterAnimation", menuName = "Be ODYSSEUS/Monster Animation")]
    public class MonsterAnimationSet : ScriptableObject
    {
        [SerializeField] private Sprite[] _idleFrames;
        [Tooltip("대기 애니메이션 초당 프레임 수.")]
        [SerializeField, Min(0.1f)] private float _idleFramesPerSecond = 8f;

        [Tooltip("걷기(떠다니기) 프레임.")]
        [SerializeField] private Sprite[] _walkFrames;
        [Tooltip("걷기(떠다니기) 애니메이션 초당 프레임 수.")]
        [SerializeField, Min(0.1f)] private float _walkFramesPerSecond = 10f;

        [Tooltip("걷기 그림이 오른쪽으로 가는 모습이면 체크. 왼쪽으로 갈 때 좌우를 뒤집는다.")]
        [SerializeField] private bool _walkFacesRight = true;
        [Tooltip("가는 방향에 맞춰 그림 좌우를 뒤집을지. 정면을 보고 움직이는 그림이면 끈다.")]
        [SerializeField] private bool _flipToMoveDirection = true;

        [Tooltip("맞았지만 아직 쓰러지지 않을 때 한 번 재생하는 그림(선택). 없으면 쓰러지는 그림 한 장으로 잠깐 움찔한다.")]
        [SerializeField] private Sprite[] _hitFrames;
        [Tooltip("맞는 애니메이션 초당 프레임 수.")]
        [SerializeField, Min(0.1f)] private float _hitFramesPerSecond = 12f;

        [Tooltip("쓰러지는 그림(선택). 있으면 먼저 한 번 재생한다. 없으면 바로 옆으로 넘어뜨린다.")]
        [SerializeField] private Sprite[] _deathFrames;
        [Tooltip("쓰러지는 애니메이션 초당 프레임 수.")]
        [SerializeField, Min(0.1f)] private float _deathFramesPerSecond = 10f;
        [Tooltip("쓰러지는 그림에 끝(누운 모습, 흩어져 사라지는 모습)까지 그려져 있으면 체크. " +
                 "그림을 다 튼 뒤 옆으로 넘어뜨리지 않고, 마지막 그림으로 잠시 있다가 사라진다.")]
        [SerializeField] private bool _deathFramesShowFall;
        [Tooltip("쓰러질 때 먼저 활동 범위 바닥까지 떨어진다. 공중에 떠 있다가 바닥에 무너지는 그림을 쓰는 몬스터용.")]
        [SerializeField] private bool _deathDropsToGround;
        [Tooltip("바닥까지 떨어지는 데 걸리는 시간(초). 클수록 천천히 떨어진다.")]
        [SerializeField, Min(0.05f)] private float _deathDropSeconds = 0.45f;

        [Header("능력치")]
        [Tooltip("체력: 몇 번 맞혀야 쓰러지는지.")]
        [SerializeField, Min(1)] private int _health = 1;

        [Header("움직임")]
        [SerializeField] private MonsterMovement _movement = MonsterMovement.Walk;
        [Tooltip("Walk·Float일 때 좌우로 움직이는 속도 배율. 1이면 GameConfig의 기본 속도. 그림 넘기는 속도와는 따로다.")]
        [SerializeField, Range(0.1f, 2f)] private float _moveSpeedScale = 1f;
        [Tooltip("Float일 때 위아래로 한 번 오르내리는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.1f)] private float _floatCycleSeconds = 3f;
        [Tooltip("Float일 때 위아래로 움직이는 폭. 1이면 활동 범위 위아래 끝까지.")]
        [SerializeField, Range(0f, 1f)] private float _floatHeight01 = 1f;
        [Tooltip("Swoop일 때 한 자리에 떠 있는 최소 시간(초). 최소~최대 사이에서 매번 무작위로 정한다.")]
        [SerializeField, Min(0f)] private float _swoopHoverMinSeconds = 2f;
        [Tooltip("Swoop일 때 한 자리에 떠 있는 최대 시간(초).")]
        [SerializeField, Min(0f)] private float _swoopHoverMaxSeconds = 3f;
        [Tooltip("Swoop일 때 다음 자리까지 날아가는 시간(초). 멀든 가깝든 이 시간에 도착한다.")]
        [SerializeField, Min(0.1f)] private float _swoopFlightSeconds = 1f;
        [Tooltip("Swoop일 때 다음 자리는 지금 자리에서 적어도 이만큼(활동 범위 폭 대비) 떨어진 곳으로 고른다.")]
        [SerializeField, Range(0f, 1f)] private float _swoopMinJump01 = 0.35f;
        [Tooltip("Swoop로 날아가는 동안 몸에서 떨어뜨리는 조각(깃털 등). 무작위로 골라 쓴다. 비워 두면 안 떨어뜨린다.")]
        [SerializeField] private Sprite[] _swoopTrailSprites;
        [Tooltip("날아가는 동안 초당 떨어뜨리는 조각 수.")]
        [SerializeField, Min(0f)] private float _swoopTrailPerSecond = 14f;
        [Tooltip("날아오르는 순간 한꺼번에 떨어뜨리는 조각 수(푸드덕).")]
        [SerializeField, Min(0)] private int _swoopTrailBurst = 5;
        [Tooltip("조각 크기 배율. 1이면 몬스터 그림과 같은 비율.")]
        [SerializeField, Min(0.1f)] private float _swoopTrailScale = 1.6f;
        [Tooltip("떨어진 조각이 사라질 때까지 걸리는 시간(초).")]
        [SerializeField, Min(0.1f)] private float _swoopTrailLifeSeconds = 1.4f;
        [Tooltip("날아갈 때 가는 쪽으로 기우는 최대 각도(도). 날아가는 중간에 가장 많이 기운다.")]
        [SerializeField, Range(0f, 45f)] private float _swoopBankDegrees = 12f;
        [Tooltip("날갯짓: 날아가는 동안 몸 좌우 폭이 커졌다 작아지는 정도. 0.1이면 ±10%.")]
        [SerializeField, Range(0f, 0.5f)] private float _swoopFlapAmount = 0.08f;
        [Tooltip("날갯짓 횟수(초당).")]
        [SerializeField, Min(0f)] private float _swoopFlapsPerSecond = 6f;

        public Sprite[] IdleFrames => _idleFrames;
        public float IdleFramesPerSecond => _idleFramesPerSecond;
        public Sprite[] WalkFrames => _walkFrames;
        public float WalkFramesPerSecond => _walkFramesPerSecond;
        public bool WalkFacesRight => _walkFacesRight;
        public bool FlipToMoveDirection => _flipToMoveDirection;
        public Sprite[] HitFrames => _hitFrames;
        public float HitFramesPerSecond => _hitFramesPerSecond;
        public bool HasHitFrames => _hitFrames != null && _hitFrames.Length > 0;
        public Sprite[] DeathFrames => _deathFrames;
        public float DeathFramesPerSecond => _deathFramesPerSecond;
        public bool HasDeathFrames => _deathFrames != null && _deathFrames.Length > 0;
        public bool DeathFramesShowFall => _deathFramesShowFall;
        public bool DeathDropsToGround => _deathDropsToGround;
        public float DeathDropSeconds => _deathDropSeconds;
        public int Health => _health;
        public MonsterMovement Movement => _movement;
        public float MoveSpeedScale => _moveSpeedScale;
        public float FloatCycleSeconds => _floatCycleSeconds;
        public float FloatHeight01 => _floatHeight01;
        public float SwoopHoverMinSeconds => _swoopHoverMinSeconds;
        public float SwoopHoverMaxSeconds => Mathf.Max(_swoopHoverMinSeconds, _swoopHoverMaxSeconds);
        public float SwoopFlightSeconds => _swoopFlightSeconds;
        public float SwoopMinJump01 => _swoopMinJump01;
        public Sprite[] SwoopTrailSprites => _swoopTrailSprites;
        public bool HasSwoopTrail => _swoopTrailSprites != null && _swoopTrailSprites.Length > 0 && _swoopTrailPerSecond > 0f;
        public float SwoopTrailPerSecond => _swoopTrailPerSecond;
        public int SwoopTrailBurst => _swoopTrailBurst;
        public float SwoopTrailScale => _swoopTrailScale;
        public float SwoopTrailLifeSeconds => _swoopTrailLifeSeconds;
        public float SwoopBankDegrees => _swoopBankDegrees;
        public float SwoopFlapAmount => _swoopFlapAmount;
        public float SwoopFlapsPerSecond => _swoopFlapsPerSecond;

        /// <summary>땅을 걷지 않고 공중에 떠 있는 몬스터인지(Float, Swoop).</summary>
        public bool Flies => _movement != MonsterMovement.Walk;

        /// <summary>크기를 정할 때 기준으로 삼는 그림(대기 첫 프레임, 없으면 걷기 첫 프레임).</summary>
        public Sprite ReferenceFrame =>
            _idleFrames != null && _idleFrames.Length > 0 ? _idleFrames[0]
            : _walkFrames != null && _walkFrames.Length > 0 ? _walkFrames[0] : null;
    }
}
