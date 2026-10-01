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

        [Tooltip("처치된 몬스터가 다시 나타날 때까지의 시간(초).")]
        [SerializeField, Min(0f)] private float _monsterRespawnSeconds = 1f;

        [Header("발사 감지")]
        [Tooltip("가속도 센서에 요청할 측정 빈도(Hz). 기기가 지원하는 만큼만 적용된다.")]
        [SerializeField, Min(1f)] private float _accelerometerSamplingHz = 200f;

        [Tooltip("충격(평소 가속도와의 차이, g)이 이 값 이상이면 발사로 본다. 1g = 중력 가속도.")]
        [SerializeField, Min(0.1f)] private float _shotAccelerationThreshold = 2f;

        [Tooltip("한 번 발사로 본 뒤 이 시간(초) 동안은 다시 발사로 보지 않는다. 떨림이 여러 번 튀는 것을 막는다.")]
        [SerializeField, Min(0f)] private float _shotCooldownSeconds = 1f;

        [Tooltip("충격이 온 시각보다 이만큼(초) 앞의 조준 방향으로 명중을 판정한다. 충격에 흔들린 화면을 쓰지 않기 위해서다.")]
        [SerializeField, Min(0f)] private float _shotAimLookbackSeconds = 0.05f;

        [Header("판정")]
        [Tooltip("정확도: 조준점이 몬스터 중심에서 이 각도(도) 이상 벗어나면 0%, 정중앙이면 100%.")]
        [SerializeField, Min(0.1f)] private float _accuracyZeroDegrees = 10f;

        [Tooltip("조준 안정도: 발사 직전 이 시간(초) 동안의 흔들림을 본다.")]
        [SerializeField, Min(0.1f)] private float _stabilityWindowSeconds = 1f;

        [Tooltip("조준 안정도: 평균 흔들림이 이 각도(도) 이하면 100%.")]
        [SerializeField, Min(0f)] private float _stabilityPerfectDegrees = 0.5f;

        [Tooltip("조준 안정도: 평균 흔들림이 이 각도(도) 이상이면 0%.")]
        [SerializeField, Min(0.1f)] private float _stabilityZeroDegrees = 5f;

        public float CalibrationHoldSeconds => _calibrationHoldSeconds;
        public float CalibrationMaxAngularSpeed => _calibrationMaxAngularSpeed;
        public Vector2 AreaHalfSizeDegrees => new(_areaHalfWidthDegrees, _areaHalfHeightDegrees);
        public float AreaDistanceMeters => _areaDistanceMeters;
        public float MonsterHeightMeters => _monsterHeightMeters;
        public float MonsterMoveSpeedDegrees => _monsterMoveSpeedDegrees;
        public float MonsterPauseMinSeconds => _monsterPauseMinSeconds;
        public float MonsterPauseMaxSeconds => Mathf.Max(_monsterPauseMinSeconds, _monsterPauseMaxSeconds);
        public float MonsterRespawnSeconds => _monsterRespawnSeconds;
        public float AccelerometerSamplingHz => _accelerometerSamplingHz;
        public float ShotAccelerationThreshold => _shotAccelerationThreshold;
        public float ShotCooldownSeconds => _shotCooldownSeconds;
        public float ShotAimLookbackSeconds => _shotAimLookbackSeconds;
        public float AccuracyZeroDegrees => _accuracyZeroDegrees;
        public float StabilityWindowSeconds => _stabilityWindowSeconds;
        public float StabilityPerfectDegrees => _stabilityPerfectDegrees;
        public float StabilityZeroDegrees => Mathf.Max(_stabilityPerfectDegrees + 0.01f, _stabilityZeroDegrees);
    }
}
