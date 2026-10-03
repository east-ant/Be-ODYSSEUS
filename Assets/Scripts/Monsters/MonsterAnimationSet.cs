using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 몬스터 한 종류의 애니메이션 그림: 대기(제자리)와 걷기 프레임, 각각의 재생 속도.
    /// 프레임 그림은 발밑을 기준점(pivot)으로 잘라 두어, 프레임이 바뀌어도 발 위치가 흔들리지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterAnimation", menuName = "Be ODYSSEUS/Monster Animation")]
    public class MonsterAnimationSet : ScriptableObject
    {
        [SerializeField] private Sprite[] _idleFrames;
        [Tooltip("대기 애니메이션 초당 프레임 수.")]
        [SerializeField, Min(0.1f)] private float _idleFramesPerSecond = 8f;

        [SerializeField] private Sprite[] _walkFrames;
        [Tooltip("걷기 애니메이션 초당 프레임 수.")]
        [SerializeField, Min(0.1f)] private float _walkFramesPerSecond = 10f;

        [Tooltip("걷기 그림이 오른쪽으로 걷는 모습이면 체크. 왼쪽으로 갈 때 좌우를 뒤집는다.")]
        [SerializeField] private bool _walkFacesRight = true;

        [Tooltip("쓰러지는 그림(선택). 비워 두면 서 있는 그림을 발밑 기준으로 옆으로 넘어뜨려 쓰러지는 모습을 만든다.")]
        [SerializeField] private Sprite[] _deathFrames;
        [Tooltip("쓰러지는 애니메이션 초당 프레임 수. 마지막 프레임에서 멈춘 채 사라진다.")]
        [SerializeField, Min(0.1f)] private float _deathFramesPerSecond = 10f;

        public Sprite[] IdleFrames => _idleFrames;
        public float IdleFramesPerSecond => _idleFramesPerSecond;
        public Sprite[] WalkFrames => _walkFrames;
        public float WalkFramesPerSecond => _walkFramesPerSecond;
        public bool WalkFacesRight => _walkFacesRight;
        public Sprite[] DeathFrames => _deathFrames;
        public float DeathFramesPerSecond => _deathFramesPerSecond;
        public bool HasDeathFrames => _deathFrames != null && _deathFrames.Length > 0;

        /// <summary>크기를 정할 때 기준으로 삼는 그림(대기 첫 프레임).</summary>
        public Sprite ReferenceFrame => _idleFrames != null && _idleFrames.Length > 0 ? _idleFrames[0] : null;
    }
}
