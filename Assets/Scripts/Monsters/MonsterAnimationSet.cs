using UnityEngine;

namespace BeOdysseus
{
    public enum MonsterMovement
    {
        /// <summary>활동 범위 가운데 줄을 따라 좌우로 걷다가, 아무 데서나 잠시 멈춰 대기 애니메이션.</summary>
        Walk,
        /// <summary>좌우로 떠다니면서 위아래로도 오르내린다. 끝에서 멈추지 않고 바로 방향을 바꾼다.</summary>
        Float,
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

        [Header("능력치")]
        [Tooltip("체력: 몇 번 맞혀야 쓰러지는지.")]
        [SerializeField, Min(1)] private int _health = 1;

        [Header("움직임")]
        [SerializeField] private MonsterMovement _movement = MonsterMovement.Walk;
        [Tooltip("Float일 때 위아래로 한 번 오르내리는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.1f)] private float _floatCycleSeconds = 3f;
        [Tooltip("Float일 때 위아래로 움직이는 폭. 1이면 활동 범위 위아래 끝까지.")]
        [SerializeField, Range(0f, 1f)] private float _floatHeight01 = 1f;

        public Sprite[] IdleFrames => _idleFrames;
        public float IdleFramesPerSecond => _idleFramesPerSecond;
        public Sprite[] WalkFrames => _walkFrames;
        public float WalkFramesPerSecond => _walkFramesPerSecond;
        public bool WalkFacesRight => _walkFacesRight;
        public Sprite[] HitFrames => _hitFrames;
        public float HitFramesPerSecond => _hitFramesPerSecond;
        public bool HasHitFrames => _hitFrames != null && _hitFrames.Length > 0;
        public Sprite[] DeathFrames => _deathFrames;
        public float DeathFramesPerSecond => _deathFramesPerSecond;
        public bool HasDeathFrames => _deathFrames != null && _deathFrames.Length > 0;
        public bool DeathFramesShowFall => _deathFramesShowFall;
        public int Health => _health;
        public MonsterMovement Movement => _movement;
        public float FloatCycleSeconds => _floatCycleSeconds;
        public float FloatHeight01 => _floatHeight01;

        /// <summary>크기를 정할 때 기준으로 삼는 그림(대기 첫 프레임, 없으면 걷기 첫 프레임).</summary>
        public Sprite ReferenceFrame =>
            _idleFrames != null && _idleFrames.Length > 0 ? _idleFrames[0]
            : _walkFrames != null && _walkFrames.Length > 0 ? _walkFrames[0] : null;
    }
}
