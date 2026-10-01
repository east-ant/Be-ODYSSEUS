using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 실제 활로 시험하면서 맞춰야 하는 숫자를 한곳에 모은 설정 파일.
    /// 코드를 고치지 않고 인스펙터에서 값을 바꾼다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Be ODYSSEUS/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("방향 정하기")]
        [Tooltip("이 시간(초) 동안 가만히 겨누면 그 방향을 정면으로 정한다.")]
        [SerializeField, Min(0.1f)] private float _calibrationHoldSeconds = 2f;

        [Tooltip("조준 방향이 이보다 빨리(도/초) 움직이면 '움직이는 중'으로 보고 시간을 처음부터 다시 잰다.")]
        [SerializeField, Min(0f)] private float _calibrationMaxAngularSpeed = 6f;

        [Header("몬스터 활동 범위")]
        [Tooltip("정면에서 좌우 한쪽으로 몇 도까지인지.")]
        [SerializeField, Range(1f, 60f)] private float _areaHalfWidthDegrees = 20f;

        [Tooltip("정면에서 위아래 한쪽으로 몇 도까지인지.")]
        [SerializeField, Range(1f, 45f)] private float _areaHalfHeightDegrees = 10f;

        [Tooltip("몬스터가 떠 있는 가상의 거리(m). 화면에 보이는 크기와 움직일 때의 원근감에 영향을 준다.")]
        [SerializeField, Min(0.5f)] private float _areaDistanceMeters = 3f;

        [Header("몬스터")]
        [Tooltip("몬스터 키(m). 활동 범위 거리와 함께 화면에서 보이는 크기를 정한다.")]
        [SerializeField, Min(0.05f)] private float _monsterHeightMeters = 0.5f;

        [Tooltip("몬스터가 범위 안을 움직이는 속도(도/초).")]
        [SerializeField, Min(0f)] private float _monsterMoveSpeedDegrees = 6f;

        [Tooltip("목표 지점에 도착한 뒤 멈춰 있는 시간(초)의 최소값.")]
        [SerializeField, Min(0f)] private float _monsterPauseMinSeconds = 0.5f;

        [Tooltip("목표 지점에 도착한 뒤 멈춰 있는 시간(초)의 최대값.")]
        [SerializeField, Min(0f)] private float _monsterPauseMaxSeconds = 1.5f;

        public float CalibrationHoldSeconds => _calibrationHoldSeconds;
        public float CalibrationMaxAngularSpeed => _calibrationMaxAngularSpeed;
        public Vector2 AreaHalfSizeDegrees => new(_areaHalfWidthDegrees, _areaHalfHeightDegrees);
        public float AreaDistanceMeters => _areaDistanceMeters;
        public float MonsterHeightMeters => _monsterHeightMeters;
        public float MonsterMoveSpeedDegrees => _monsterMoveSpeedDegrees;
        public float MonsterPauseMinSeconds => _monsterPauseMinSeconds;
        public float MonsterPauseMaxSeconds => Mathf.Max(_monsterPauseMinSeconds, _monsterPauseMaxSeconds);
    }
}
