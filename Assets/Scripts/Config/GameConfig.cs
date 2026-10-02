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
        [Header("튜토리얼(중앙점 정하기)")]
        [Tooltip("\"방향 정하기\" 버튼을 누르기 이만큼(초) 전의 조준 방향을 중앙점으로 쓴다. 화면을 누르는 손에 폰이 흔들린 방향을 피하려고.")]
        [SerializeField, Min(0f)] private float _centerCaptureLookbackSeconds = 0.3f;

        [Tooltip("중앙점을 정한 뒤 1스테이지가 시작되기까지의 시간(초). 이 동안 범위를 확인하고 다시 정할 수 있다.")]
        [SerializeField, Min(0f)] private float _tutorialConfirmSeconds = 3f;

        [Header("몬스터 활동 범위")]
        [Tooltip("정면에서 좌우 한쪽으로 몇 도까지인지.")]
        [SerializeField, Range(1f, 60f)] private float _areaHalfWidthDegrees = 26f;

        [Tooltip("정면에서 위아래 한쪽으로 몇 도까지인지.")]
        [SerializeField, Range(1f, 45f)] private float _areaHalfHeightDegrees = 13f;

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

        [Header("스테이지")]
        [Tooltip("한 스테이지에서 쏠 수 있는 화살 수. 이 안에 몬스터를 맞히면 클리어.")]
        [SerializeField, Min(1)] private int _arrowsPerStage = 2;

        [Tooltip("한 스테이지 제한 시간(초). 다 되면 실패로 끝난다.")]
        [SerializeField, Min(1f)] private float _stageTimeLimitSeconds = 60f;

        [Tooltip("스테이지가 끝난 뒤 결과 화면을 띄우기까지 기다리는 시간(초). 마지막 발의 결과를 눈으로 볼 수 있게 한다.")]
        [SerializeField, Min(0f)] private float _resultDelaySeconds = 1f;

        [Tooltip("결과 화면에서 다음 스테이지로 넘어가기까지 세는 시간(초).")]
        [SerializeField, Min(1f)] private float _resultCountdownSeconds = 5f;

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

        public float CenterCaptureLookbackSeconds => _centerCaptureLookbackSeconds;
        public float TutorialConfirmSeconds => _tutorialConfirmSeconds;
        public Vector2 AreaHalfSizeDegrees => new(_areaHalfWidthDegrees, _areaHalfHeightDegrees);
        public float AreaDistanceMeters => _areaDistanceMeters;
        public float MonsterHeightMeters => _monsterHeightMeters;
        public float MonsterMoveSpeedDegrees => _monsterMoveSpeedDegrees;
        public float MonsterPauseMinSeconds => _monsterPauseMinSeconds;
        public float MonsterPauseMaxSeconds => Mathf.Max(_monsterPauseMinSeconds, _monsterPauseMaxSeconds);
        public int ArrowsPerStage => _arrowsPerStage;
        public float StageTimeLimitSeconds => _stageTimeLimitSeconds;
        public float ResultDelaySeconds => _resultDelaySeconds;
        public float ResultCountdownSeconds => _resultCountdownSeconds;
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
