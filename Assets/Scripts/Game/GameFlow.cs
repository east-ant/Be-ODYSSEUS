using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 게임 진행 순서: AR 준비 대기 → 방향 정하기 → 활동 범위 표시와 몬스터 등장.
    /// 테스트 발사와 라운드는 다음 단계에서 이어 붙인다.
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
        [SerializeField] private Monster _monsterPrefab;

        private Phase _phase;
        private Monster _monster;
        private string _calibratingMessage;

        private void Awake()
        {
            _calibratingMessage = $"쏠 방향을 겨누고 {_config.CalibrationHoldSeconds:0.#}초 동안 가만히 계세요";
        }

        private void OnEnable() => _calibrator.Calibrated += OnCalibrated;
        private void OnDisable() => _calibrator.Calibrated -= OnCalibrated;
        private void Start() => Recalibrate();

        /// <summary>방향을 처음부터 다시 정한다. 화면의 "방향 다시 정하기" 버튼에 연결된다.</summary>
        public void Recalibrate()
        {
            _calibrator.Stop();
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
            }
        }

        private void OnCalibrated(Pose front)
        {
            _playArea.SetFront(front);
            _calibrationView.Hide();
            ShowMonster();
            _phase = Phase.Playing;
        }

        private void ShowMonster()
        {
            if (_monster == null) _monster = Instantiate(_monsterPrefab);
            _monster.gameObject.SetActive(true);
            _monster.Init(_playArea, _viewer, Vector2.zero);
        }
    }
}
