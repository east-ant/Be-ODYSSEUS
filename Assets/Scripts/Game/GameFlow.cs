using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeOdysseus
{
    /// <summary>
    /// 게임 진행 순서:
    /// 메인 화면 → 튜토리얼(중앙점 정하기) → 스테이지(화살 3발, 60초) → 결과 화면(카운트다운) → 다음 스테이지 … → 메인 화면.
    /// 중앙점은 튜토리얼에서만 정할 수 있고, 스테이지와 결과 화면에서는 바꿀 수 없다.
    /// 몬스터를 쓰러뜨리면(체력을 다 깎으면) 클리어, 화살을 다 쓰거나 시간이 다 되면 실패. 실패해도 결과를 보여 주고 다음으로 넘어간다.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        private const string NextStageLabel = "다음 스테이지";
        private const string BackToMenuLabel = "메인 화면으로";

        private enum Phase { MainMenu, Tutorial, Playing, StageEnding, Result }

        [SerializeField] private GameConfig _config;
        [SerializeField] private StageList _stages;
        [Tooltip("AR 카메라. 몬스터가 이쪽을 바라본다.")]
        [SerializeField] private Transform _viewer;
        [SerializeField] private AimCalibrator _calibrator;
        [SerializeField] private PlayArea _playArea;
        [SerializeField] private MainMenuView _mainMenu;
        [Tooltip("AR 게임 화면에서만 보이는 요소(조준점, 스테이지 표시).")]
        [SerializeField] private GameObject _gameplayHud;
        [SerializeField] private StageHud _stageHud;
        [SerializeField] private CalibrationView _calibrationView;
        [SerializeField] private ResultView _resultView;
        [SerializeField] private ShotJudge _shotJudge;
        [SerializeField] private Monster _monsterPrefab;

        private readonly List<ShotResult> _allShots = new();
        private Phase _phase;
        private Monster _monster;
        private bool _isCenterSet;
        private float _tutorialLeft;
        private int _stageIndex;
        private StageRun _stageRun;
        private StageResult _lastStageResult;
        private float _resultDelayLeft;

        /// <summary>"게임 시작"을 눌렀을 때.</summary>
        public event Action GameStarted;
        /// <summary>스테이지가 시작될 때. 그 스테이지의 진행 기록과 설정(몬스터)이 담긴다.</summary>
        public event Action<StageRun, StageDefinition> StageStarted;
        /// <summary>스테이지 중 한 발의 판정이 기록될 때. bool은 이 발로 몬스터를 쓰러뜨렸는지.</summary>
        public event Action<ShotResult, StageRun, bool> ShotRecorded;
        /// <summary>스테이지가 끝날 때마다(클리어든 실패든). 결과 화면·음성 피드백에서 쓸 값이 담긴다.</summary>
        public event Action<StageResult> StageFinished;
        /// <summary>게임이 끝나 메인 화면으로 돌아갈 때. 마지막 스테이지까지 마쳤으면 true, 중간에 나갔으면 false. 값은 누적 결과.</summary>
        public event Action<bool, StageResult> GameEnded;

        /// <summary>메인 화면이나 결과 화면이 아니라 AR 게임 화면(튜토리얼 포함)이 보이는 중인지.</summary>
        public bool IsInGameplay => _phase is Phase.Tutorial or Phase.Playing or Phase.StageEnding;
        /// <summary>게임 시작부터 지금까지의 점수(명중 1발당 1점).</summary>
        public int Score { get; private set; }
        public int ShotsFired => _allShots.Count;
        public bool HasLastResult => _allShots.Count > 0;
        public ShotResult LastResult => _allShots[_allShots.Count - 1];

        private StageDefinition CurrentStage => _stages.Get(_stageIndex);

        private void OnEnable()
        {
            _mainMenu.StartPressed += OnStartPressed;
            _calibrationView.ButtonPressed += OnCenterButtonPressed;
            _shotJudge.Resolved += OnShotResolved;
            _resultView.CountdownFinished += OnResultCountdownFinished;
        }

        private void OnDisable()
        {
            _mainMenu.StartPressed -= OnStartPressed;
            _calibrationView.ButtonPressed -= OnCenterButtonPressed;
            _shotJudge.Resolved -= OnShotResolved;
            _resultView.CountdownFinished -= OnResultCountdownFinished;
        }

        private void Start() => ShowMainMenu();

        /// <summary>게임을 멈추고 메인 화면으로 돌아간다. 게임 도중이었으면 GameEnded(중간에 나감)를 알린다.</summary>
        public void ShowMainMenu() => ShowMainMenu(completed: false);

        private void ShowMainMenu(bool completed)
        {
            bool wasInGame = _phase != Phase.MainMenu;
            StopGameplay();
            _stageRun = null;
            _gameplayHud.SetActive(false);
            _calibrationView.Hide();
            _resultView.Hide();
            _mainMenu.Show();
            _phase = Phase.MainMenu;
            if (wasInGame) GameEnded?.Invoke(completed, StageResult.FromShots(_stageIndex + 1, false, _allShots));
        }

        private void OnStartPressed()
        {
            _mainMenu.Hide();
            _gameplayHud.SetActive(true);
            _allShots.Clear();
            Score = 0;
            _stageIndex = 0;
            _stageRun = null;
            GameStarted?.Invoke();
            BeginTutorial();
        }

        private void BeginTutorial()
        {
            StopGameplay();
            _stageHud.Hide();
            _isCenterSet = false;
            _phase = Phase.Tutorial;
        }

        private void StopGameplay()
        {
            _shotJudge.IsArmed = false;
            _playArea.Clear();
            HideMonster();
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
                case Phase.Tutorial:
                    TickTutorial(Time.deltaTime);
                    break;

                case Phase.Playing:
                    _stageRun.Tick(Time.deltaTime);
                    _stageHud.Show(_stageRun);
                    if (_stageRun.IsOver) EndStage();
                    break;

                case Phase.StageEnding:
                    // 몬스터가 쓰러지는 중이면 다 쓰러질 때까지 기다렸다가 결과로 넘어간다.
                    if (_monster != null && _monster.IsDying) break;
                    _resultDelayLeft -= Time.deltaTime;
                    if (_resultDelayLeft <= 0f) ShowResult();
                    break;
            }
        }

        // 안드로이드 뒤로 가기 버튼은 Input System에서 Escape 키로 들어온다.
        private static bool BackPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        private void TickTutorial(float dt)
        {
            if (!_isCenterSet)
            {
                if (AimTracking.IsReliable) _calibrationView.ShowReady();
                else _calibrationView.ShowWaitingForAr();
                return;
            }

            _tutorialLeft -= dt;
            _calibrationView.ShowConfirmed(_stageIndex + 1, Mathf.Max(1, Mathf.CeilToInt(_tutorialLeft)));
            if (_tutorialLeft > 0f) return;

            _calibrationView.Hide();
            BeginStage();
        }

        /// <summary>튜토리얼의 "방향 정하기"/"다시 정하기" 버튼. 지금 겨눈 방향을 중앙점으로 정하고 범위를 보여 준다.</summary>
        private void OnCenterButtonPressed()
        {
            if (_phase != Phase.Tutorial || !AimTracking.IsReliable) return;
            _playArea.SetFront(_calibrator.CaptureFront());
            _isCenterSet = true;
            _tutorialLeft = _config.TutorialConfirmSeconds;
        }

        private void BeginStage()
        {
            _stageRun = new StageRun(_stageIndex + 1, _config.ArrowsPerStage, _config.StageTimeLimitSeconds);
            ShowMonster(CurrentStage);
            _shotJudge.IsArmed = true;
            _stageHud.Show(_stageRun);
            _phase = Phase.Playing;
            StageStarted?.Invoke(_stageRun, CurrentStage);
        }

        private void OnShotResolved(ShotResult result)
        {
            if (_phase != Phase.Playing) return;

            // 맞히면 1점. 몬스터는 체력이 남아 있으면 움찔하고, 0이 되면 쓰러진다.
            bool killed = false;
            if (result.Hit)
            {
                Score++;
                killed = _monster.TakeHit(result.ImpactPoint);
            }

            _allShots.Add(result);
            _stageRun.Record(result, killed);
            ShotRecorded?.Invoke(result, _stageRun, killed);
            _stageHud.Show(_stageRun);
            if (_stageRun.IsOver) EndStage();
        }

        private void EndStage()
        {
            _shotJudge.IsArmed = false;
            _lastStageResult = StageResult.FromShots(_stageRun.StageNumber, _stageRun.IsCleared, _allShots);
            Debug.Log($"[Stage] {_lastStageResult.StageNumber} cleared={_lastStageResult.Cleared} " +
                      $"stageShots={_stageRun.Shots.Count} timeLeft={_stageRun.TimeLeft:F1}s | total score={_lastStageResult.TotalScore} " +
                      $"hits={_lastStageResult.TotalHits}/{_lastStageResult.TotalShots} stability={_lastStageResult.AverageStability01:P0}");
            StageFinished?.Invoke(_lastStageResult);
            _resultDelayLeft = _config.ResultDelaySeconds;
            _phase = Phase.StageEnding;
        }

        private void ShowResult()
        {
            HideMonster();
            _gameplayHud.SetActive(false);
            bool hasNextStage = _stageIndex + 1 < _stages.Count;
            _resultView.Show(_lastStageResult, CurrentStage, hasNextStage ? NextStageLabel : BackToMenuLabel, _config.ResultCountdownSeconds);
            _phase = Phase.Result;
        }

        private void OnResultCountdownFinished()
        {
            if (_phase != Phase.Result) return;
            _resultView.Hide();
            if (_stageIndex + 1 >= _stages.Count)
            {
                ShowMainMenu(completed: true);
                return;
            }

            _stageIndex++;
            _gameplayHud.SetActive(true);
            BeginStage();
        }

        private void ShowMonster(StageDefinition stage)
        {
            if (_monster == null) _monster = Instantiate(_monsterPrefab);
            _monster.gameObject.SetActive(true);
            _monster.Init(_playArea, _viewer, stage.MonsterSprite, stage.MonsterAnimation);
            _shotJudge.Target = _monster;
        }

        private void HideMonster()
        {
            if (_monster != null) _monster.gameObject.SetActive(false);
        }
    }
}
