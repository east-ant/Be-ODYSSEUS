using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 게임 진행 순서: AR 준비 대기 → 방향 정하기 → 활동 범위 표시와 몬스터 등장 → 발사·명중 처리.
    /// 라운드(2발, 60초)와 결과 화면은 다음 단계에서 이어 붙인다.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        private const string WaitingMessage = "AR 준비 중… 주변을 천천히 비춰 주세요";

        private enum Phase { WaitingForTracking, Calibrating, Playing }

        [SerializeField] private GameConfig _config;
        [Tooltip("AR 카메라. 몬스터가 이쪽을 바라본다.")]
        [SerializeField] private Transform _viewer;
        [SerializeField] private AimCalibrator _calibrator;
        [SerializeField] private PlayArea _playArea;
        [SerializeField] private CalibrationView _calibrationView;
        [SerializeField] private ShotJudge _shotJudge;
        [SerializeField] private Monster _monsterPrefab;

        private Phase _phase;
        private Monster _monster;
        private string _calibratingMessage;
        private float _respawnLeft;

        public int Score { get; private set; }
        public int ShotsFired { get; private set; }
        public bool HasLastResult { get; private set; }
        public ShotResult LastResult { get; private set; }

        private void Awake()
        {
            _calibratingMessage = $"쏠 방향을 겨누고 {_config.CalibrationHoldSeconds:0.#}초 동안 가만히 계세요";
        }

        private void OnEnable()
        {
            _calibrator.Calibrated += OnCalibrated;
            _shotJudge.Resolved += OnShotResolved;
        }

        private void OnDisable()
        {
            _calibrator.Calibrated -= OnCalibrated;
            _shotJudge.Resolved -= OnShotResolved;
        }

        private void Start() => Recalibrate();

        /// <summary>방향을 처음부터 다시 정한다. 화면의 "방향 다시 정하기" 버튼에 연결된다.</summary>
        public void Recalibrate()
        {
            _calibrator.Stop();
            _shotJudge.IsArmed = false;
            _playArea.Clear();
            if (_monster != null) _monster.gameObject.SetActive(false);
            _phase = Phase.WaitingForTracking;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.WaitingForTracking:
                    _calibrationView.Show(WaitingMessage, 0f);
                    if (!AimTracking.IsReliable) break;
                    _calibrator.Begin();
                    _phase = Phase.Calibrating;
                    break;

                case Phase.Calibrating:
                    if (!AimTracking.IsReliable)
                    {
                        _calibrator.Stop();
                        _phase = Phase.WaitingForTracking;
                        break;
                    }
                    _calibrationView.Show(_calibratingMessage, _calibrator.Progress01);
                    break;

                case Phase.Playing:
                    TickRespawn(Time.deltaTime);
                    break;
            }
        }

        private void OnCalibrated(Pose front)
        {
            _playArea.SetFront(front);
            _calibrationView.Hide();
            ShowMonster(randomPosition: false);
            _shotJudge.IsArmed = true;
            _phase = Phase.Playing;
        }

        private void OnShotResolved(ShotResult result)
        {
            ShotsFired++;
            LastResult = result;
            HasLastResult = true;
            if (!result.Hit) return;

            Score++;
            _monster.gameObject.SetActive(false);
            _respawnLeft = _config.MonsterRespawnSeconds;
        }

        private void TickRespawn(float dt)
        {
            if (_monster.gameObject.activeSelf) return;
            _respawnLeft -= dt;
            if (_respawnLeft <= 0f) ShowMonster(randomPosition: true);
        }

        private void ShowMonster(bool randomPosition)
        {
            if (_monster == null) _monster = Instantiate(_monsterPrefab);
            _monster.gameObject.SetActive(true);
            _monster.Init(_playArea, _viewer, randomPosition);
            _shotJudge.Target = _monster;
        }
    }
}
