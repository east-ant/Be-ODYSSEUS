using UnityEngine;
using UnityEngine.InputSystem;

namespace BeOdysseus
{
    /// <summary>
    /// 게임 진행 순서: 메인 화면 → AR 준비 대기 → 방향 정하기 → 활동 범위 표시와 몬스터 등장 → 발사·명중 처리.
    /// 라운드(2발, 60초)와 결과 화면은 다음 단계에서 이어 붙인다.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        private const string WaitingMessage = "AR 준비 중… 주변을 천천히 비춰 주세요";

        private enum Phase { MainMenu, WaitingForTracking, Calibrating, Playing }

        [SerializeField] private GameConfig _config;
        [Tooltip("AR 카메라. 몬스터가 이쪽을 바라본다.")]
        [SerializeField] private Transform _viewer;
        [SerializeField] private AimCalibrator _calibrator;
        [SerializeField] private PlayArea _playArea;
        [SerializeField] private MainMenuView _mainMenu;
        [Tooltip("게임 중에만 보이는 화면 요소(조준점, 방향 다시 정하기 버튼).")]
        [SerializeField] private GameObject _gameplayHud;
        [SerializeField] private CalibrationView _calibrationView;
        [SerializeField] private ShotJudge _shotJudge;
        [SerializeField] private Monster _monsterPrefab;

        private Phase _phase;
        private Monster _monster;
        private string _calibratingMessage;
        private float _respawnLeft;

        public bool IsOnMainMenu => _phase == Phase.MainMenu;
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
            _mainMenu.StartPressed += OnStartPressed;
            _calibrator.Calibrated += OnCalibrated;
            _shotJudge.Resolved += OnShotResolved;
        }

        private void OnDisable()
        {
            _mainMenu.StartPressed -= OnStartPressed;
            _calibrator.Calibrated -= OnCalibrated;
            _shotJudge.Resolved -= OnShotResolved;
        }

        private void Start() => ShowMainMenu();

        /// <summary>게임을 멈추고 메인 화면으로 돌아간다.</summary>
        public void ShowMainMenu()
        {
            StopGameplay();
            _gameplayHud.SetActive(false);
            _calibrationView.Hide();
            _mainMenu.Show();
            _phase = Phase.MainMenu;
        }

        /// <summary>방향을 처음부터 다시 정한다. 화면의 "방향 다시 정하기" 버튼에 연결된다.</summary>
        public void Recalibrate()
        {
            StopGameplay();
            _phase = Phase.WaitingForTracking;
        }

        private void OnStartPressed()
        {
            _mainMenu.Hide();
            _gameplayHud.SetActive(true);
            Score = 0;
            ShotsFired = 0;
            HasLastResult = false;
            Recalibrate();
        }

        private void StopGameplay()
        {
            _calibrator.Stop();
            _shotJudge.IsArmed = false;
            _playArea.Clear();
            if (_monster != null) _monster.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_phase != Phase.MainMenu && BackPressed())
            {
                ShowMainMenu();
                return;
            }

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

        // 안드로이드 뒤로 가기 버튼은 Input System에서 Escape 키로 들어온다.
        private static bool BackPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
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
