using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeOdysseus
{
    /// <summary>
    /// 게임 진행 순서:
    /// 메인 화면 → AR 준비 대기 → 방향 정하기 → 스테이지(화살 2발, 60초) → 결과 화면(카운트다운) → 다음 스테이지 … → 메인 화면.
    /// 몬스터를 맞히면 바로 클리어, 화살을 다 쓰거나 시간이 다 되면 실패. 실패해도 결과를 보여 주고 다음으로 넘어간다.
    /// 방향은 게임을 시작할 때 한 번 정하고 스테이지가 바뀌어도 유지한다(필요하면 "방향 다시 정하기").
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        private const string WaitingMessage = "AR 준비 중… 주변을 천천히 비춰 주세요";
        private const string NextStageLabel = "다음 스테이지";
        private const string BackToMenuLabel = "메인 화면으로";

        private enum Phase { MainMenu, WaitingForTracking, Calibrating, Playing, StageEnding, Result }

        [SerializeField] private GameConfig _config;
        [SerializeField] private StageList _stages;
        [Tooltip("AR 카메라. 몬스터가 이쪽을 바라본다.")]
        [SerializeField] private Transform _viewer;
        [SerializeField] private AimCalibrator _calibrator;
        [SerializeField] private PlayArea _playArea;
        [SerializeField] private MainMenuView _mainMenu;
        [Tooltip("게임 중에만 보이는 화면 요소(조준점, 방향 다시 정하기 버튼, 스테이지 표시).")]
        [SerializeField] private GameObject _gameplayHud;
        [SerializeField] private StageHud _stageHud;
        [SerializeField] private CalibrationView _calibrationView;
        [SerializeField] private ResultView _resultView;
        [SerializeField] private ShotJudge _shotJudge;
        [SerializeField] private Monster _monsterPrefab;

        private readonly List<ShotResult> _allShots = new();
        private Phase _phase;
        private Monster _monster;
        private string _calibratingMessage;
        private int _stageIndex;
        private StageRun _stageRun;
        private StageResult _lastStageResult;
        private float _resultDelayLeft;

        /// <summary>스테이지가 끝날 때마다(클리어든 실패든). 결과 화면·음성 피드백에서 쓸 값이 담긴다.</summary>
        public event Action<StageResult> StageFinished;

        /// <summary>메인 화면이나 결과 화면이 아니라 AR 게임 화면이 보이는 중인지.</summary>
        public bool IsInGameplay => _phase is Phase.WaitingForTracking or Phase.Calibrating or Phase.Playing or Phase.StageEnding;
        /// <summary>게임 시작부터 지금까지의 점수(명중 1발당 1점).</summary>
        public int Score { get; private set; }
        public int ShotsFired => _allShots.Count;
        public bool HasLastResult => _allShots.Count > 0;
        public ShotResult LastResult => _allShots[_allShots.Count - 1];

        private StageDefinition CurrentStage => _stages.Get(_stageIndex);

        private void Awake()
        {
            _calibratingMessage = $"쏠 방향을 겨누고 {_config.CalibrationHoldSeconds:0.#}초 동안 가만히 계세요";
        }

        private void OnEnable()
        {
            _mainMenu.StartPressed += OnStartPressed;
            _calibrator.Calibrated += OnCalibrated;
            _shotJudge.Resolved += OnShotResolved;
            _resultView.CountdownFinished += OnResultCountdownFinished;
        }

        private void OnDisable()
        {
            _mainMenu.StartPressed -= OnStartPressed;
            _calibrator.Calibrated -= OnCalibrated;
            _shotJudge.Resolved -= OnShotResolved;
            _resultView.CountdownFinished -= OnResultCountdownFinished;
        }

        private void Start() => ShowMainMenu();

        /// <summary>게임을 멈추고 메인 화면으로 돌아간다.</summary>
        public void ShowMainMenu()
        {
            StopGameplay();
            _stageRun = null;
            _gameplayHud.SetActive(false);
            _calibrationView.Hide();
            _resultView.Hide();
            _mainMenu.Show();
            _phase = Phase.MainMenu;
        }

        /// <summary>
        /// 방향을 처음부터 다시 정한다. 화면의 "방향 다시 정하기" 버튼에 연결된다.
        /// 스테이지 진행 중이면 남은 화살·시간은 그대로 두고, 다시 정한 뒤 이어서 한다.
        /// </summary>
        public void Recalibrate()
        {
            if (_phase is Phase.WaitingForTracking or Phase.Calibrating or Phase.Playing) BeginCalibration();
        }

        private void OnStartPressed()
        {
            _mainMenu.Hide();
            _gameplayHud.SetActive(true);
            _allShots.Clear();
            Score = 0;
            _stageIndex = 0;
            _stageRun = null;
            BeginCalibration();
        }

        private void BeginCalibration()
        {
            StopGameplay();
            _stageHud.Hide();
            _phase = Phase.WaitingForTracking;
        }

        private void StopGameplay()
        {
            _calibrator.Stop();
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
                    _stageRun.Tick(Time.deltaTime);
                    _stageHud.Show(_stageRun);
                    if (_stageRun.IsOver) EndStage();
                    break;

                case Phase.StageEnding:
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

        private void OnCalibrated(Pose front)
        {
            _playArea.SetFront(front);
            _calibrationView.Hide();
            if (_stageRun == null) BeginStage();
            else ResumeStage();
        }

        private void BeginStage()
        {
            _stageRun = new StageRun(_stageIndex + 1, _config.ArrowsPerStage, _config.StageTimeLimitSeconds);
            ResumeStage();
        }

        private void ResumeStage()
        {
            ShowMonster(CurrentStage.MonsterSprite);
            _shotJudge.IsArmed = true;
            _stageHud.Show(_stageRun);
            _phase = Phase.Playing;
        }

        private void OnShotResolved(ShotResult result)
        {
            if (_phase != Phase.Playing) return;

            _allShots.Add(result);
            _stageRun.Record(result);
            _stageHud.Show(_stageRun);
            if (result.Hit)
            {
                Score++;
                HideMonster();
            }
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
                ShowMainMenu();
                return;
            }

            _stageIndex++;
            _gameplayHud.SetActive(true);
            BeginStage();
        }

        private void ShowMonster(Sprite sprite)
        {
            if (_monster == null) _monster = Instantiate(_monsterPrefab);
            _monster.gameObject.SetActive(true);
            _monster.Init(_playArea, _viewer, sprite, randomPosition: true);
            _shotJudge.Target = _monster;
        }

        private void HideMonster()
        {
            if (_monster != null) _monster.gameObject.SetActive(false);
        }
    }
}
