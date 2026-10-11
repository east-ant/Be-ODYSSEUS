using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BeOdysseus
{
    /// <summary>
    /// 게임 진행 순서:
    /// 메인 화면 → 튜토리얼(중앙점 정하기) → 도끼 튜토리얼(설정에서 끌 수 있음) → 스테이지(화살 3발, 60초)
    /// → 결과 화면(카운트다운) → 다음 스테이지 … → 최종 결과 화면("메인 화면으로" 버튼) → 메인 화면.
    /// 스테이지마다 몬스터 거리를 다르게 할 수 있다(StageDefinition, 가까울수록 크게 보인다).
    /// 도끼 튜토리얼은 건너뛸 수 없고, 화살을 다 쓰도록 통과하지 못하면 메인 화면으로 돌아간다.
    /// 도끼 튜토리얼에서 쏜 화살은 연습이라 점수·게임 기록(태블릿 전송 포함)에 넣지 않는다.
    /// 중앙점은 튜토리얼에서만 정할 수 있고, 스테이지와 결과 화면에서는 바꿀 수 없다.
    /// 몬스터를 쓰러뜨리면(체력을 다 깎으면) 클리어, 화살을 다 쓰거나 시간이 다 되면 실패. 실패해도 결과를 보여 주고 다음으로 넘어간다.
    /// AR 게임 화면(튜토리얼 포함)에서는 잠깐 정지할 수 있다. 정지하면 Time.timeScale을 0으로 해서 남은 시간·몬스터·효과를 멈추고,
    /// 그동안 쏜 화살은 무시한다.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        /// <summary>결과 화면 카운트다운 옆 글씨. 에디터 화면 미리보기에서도 쓴다.</summary>
        public const string NextStageLabel = "다음 스테이지";
        public const string FinalResultLabel = "최종 결과";

        private enum Phase { MainMenu, Tutorial, AxeTutorial, Playing, StageEnding, Result, FinalResult }

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
        [SerializeField] private FinalResultView _finalResultView;
        [SerializeField] private ShotJudge _shotJudge;
        [Tooltip("화살이 날아가는 모습. 판정한 발은 화살이 닿을 때 처리한다.")]
        [SerializeField] private ArrowFlight _arrows;
        [SerializeField] private Monster _monsterPrefab;
        [Tooltip("도끼 튜토리얼의 도끼 줄. 비워 두면 도끼 튜토리얼 없이 바로 1스테이지로 간다.")]
        [SerializeField] private AxeRow _axeRow;

        private readonly List<ShotResult> _allShots = new();
        private readonly List<StageSummary> _stageSummaries = new();
        private Phase _phase;
        private Monster _monster;
        private bool _isCenterSet;
        private float _tutorialLeft;
        // 튜토리얼이 시작된 입력 시각. 이보다 앞서 들어온 발사 신호로는 방향을 정하지 않는다.
        private double _tutorialStartInputTime;
        private int _stageIndex;
        private StageRun _stageRun;
        private StageResult _lastStageResult;
        private float _resultDelayLeft;
        private int _axeArrowsLeft;
        private bool _axePassed;
        // 도끼 튜토리얼이 끝나고(통과·화살 다 씀) 다음(1스테이지·메인 화면)으로 넘어가기까지 남은 시간. 끝나기 전에는 -1.
        private float _axeEndLeft = -1f;

        /// <summary>"게임 시작"을 눌렀을 때.</summary>
        public event Action GameStarted;
        /// <summary>스테이지가 시작될 때. 그 스테이지의 진행 기록과 설정(몬스터)이 담긴다.</summary>
        public event Action<StageRun, StageDefinition> StageStarted;
        /// <summary>스테이지 중 한 발의 판정이 기록될 때. bool은 이 발로 몬스터를 쓰러뜨렸는지.</summary>
        public event Action<ShotResult, StageRun, bool> ShotRecorded;
        /// <summary>스테이지가 끝날 때마다(클리어든 실패든). 결과 화면·음성 피드백에서 쓸 값이 담긴다.</summary>
        public event Action<StageResult> StageFinished;
        /// <summary>
        /// 게임이 끝났을 때. 마지막 스테이지까지 마쳤으면 최종 결과 화면이 뜨는 순간 true,
        /// 중간에 메인 화면으로 나갔으면 그때 false. 값은 누적 결과.
        /// </summary>
        public event Action<bool, StageResult> GameEnded;

        /// <summary>메인 화면이나 결과 화면이 아니라 AR 게임 화면(튜토리얼 포함)이 보이는 중인지.</summary>
        public bool IsInGameplay => _phase is Phase.Tutorial or Phase.AxeTutorial or Phase.Playing or Phase.StageEnding;
        /// <summary>정지 버튼으로 잠깐 멈춘 중인지.</summary>
        public bool IsPaused { get; private set; }
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
            _arrows.Landed += OnShotLanded;
            _shotJudge.DisarmedShot += OnDisarmedShot;
            _resultView.CountdownFinished += OnResultCountdownFinished;
            _finalResultView.MenuPressed += ShowMainMenu;
        }

        private void OnDisable()
        {
            _mainMenu.StartPressed -= OnStartPressed;
            _calibrationView.ButtonPressed -= OnCenterButtonPressed;
            _arrows.Landed -= OnShotLanded;
            _shotJudge.DisarmedShot -= OnDisarmedShot;
            _resultView.CountdownFinished -= OnResultCountdownFinished;
            _finalResultView.MenuPressed -= ShowMainMenu;
        }

        private void Start() => ShowMainMenu();

        /// <summary>게임을 잠깐 멈춘다. AR 게임 화면에서만 된다.</summary>
        public void Pause()
        {
            if (!IsInGameplay || IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            _shotJudge.IsArmed = false;
        }

        /// <summary>멈춘 게임을 이어서 한다. 화살은 스테이지·도끼 튜토리얼 진행 중일 때만 다시 받는다.</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            ClearPause();
            _shotJudge.IsArmed = _phase == Phase.Playing || (_phase == Phase.AxeTutorial && _axeEndLeft < 0f);
        }

        private void ClearPause()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }

        /// <summary>
        /// 게임을 멈추고 메인 화면으로 돌아간다. 게임 도중이었으면 GameEnded(중간에 나감)를 알린다.
        /// 최종 결과 화면에서 올 때는 이미 GameEnded(다 마침)를 알렸으니 다시 알리지 않는다.
        /// </summary>
        public void ShowMainMenu()
        {
            bool quitMidGame = _phase != Phase.MainMenu && _phase != Phase.FinalResult;
            ClearPause();
            StopGameplay();
            _stageRun = null;
            _gameplayHud.SetActive(false);
            _calibrationView.Hide();
            _resultView.Hide();
            _finalResultView.Hide();
            _mainMenu.Show();
            _phase = Phase.MainMenu;
            if (quitMidGame) GameEnded?.Invoke(false, StageResult.FromShots(_stageIndex + 1, false, _allShots));
        }

        private void OnStartPressed()
        {
            _mainMenu.Hide();
            _gameplayHud.SetActive(true);
            _allShots.Clear();
            _stageSummaries.Clear();
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
            _tutorialStartInputTime = InputState.currentTime;
            _phase = Phase.Tutorial;
        }

        private void StopGameplay()
        {
            _shotJudge.IsArmed = false;
            _playArea.Clear();
            HideMonster();
            if (_axeRow != null) _axeRow.Hide();
            _arrows.Clear();
        }

        private void Update()
        {
            if (_phase != Phase.MainMenu && BackPressed())
            {
                ShowMainMenu();
                return;
            }
            if (IsPaused) return;

            switch (_phase)
            {
                case Phase.Tutorial:
                    TickTutorial(Time.deltaTime);
                    break;

                case Phase.AxeTutorial:
                    if (_axeEndLeft < 0f) break;
                    _axeEndLeft -= Time.deltaTime;
                    if (_axeEndLeft <= 0f) FinishAxeTutorial();
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
            string next = HasAxeTutorial ? "튜토리얼" : $"{_stageIndex + 1}스테이지";
            _calibrationView.ShowConfirmed(next, Mathf.Max(1, Mathf.CeilToInt(_tutorialLeft)));
            if (_tutorialLeft > 0f) return;

            if (HasAxeTutorial)
            {
                BeginAxeTutorial();
                return;
            }
            _calibrationView.Hide();
            BeginStage();
        }

        private bool HasAxeTutorial => _config.AxeTutorialEnabled && _axeRow != null;

        private bool CanSetCenter =>_phase == Phase.Tutorial && !IsPaused && AimTracking.IsReliable;

        /// <summary>튜토리얼의 "방향 정하기"/"다시 정하기" 버튼. 지금 겨눈 방향을 중앙점으로 정하고 범위를 보여 준다.</summary>
        private void OnCenterButtonPressed()
        {
            if (CanSetCenter) SetCenter(_calibrator.CaptureFront());
        }

        /// <summary>튜토리얼에서 활을 쏘면 버튼을 누른 것과 같다. 충격 직전(발사 판정과 같은 시각)에 겨눈 방향을 쓴다.</summary>
        private void OnDisarmedShot(ShotEvent shot)
        {
            if (CanSetCenter && shot.Time > _tutorialStartInputTime) SetCenter(_calibrator.CaptureFrontAt(shot.Time - _config.ShotAimLookbackSeconds));
        }

        private void SetCenter(Pose front)
        {
            _playArea.SetFront(front);
            _isCenterSet = true;
            _tutorialLeft = _config.TutorialConfirmSeconds;
        }

        /// <summary>정한 방향으로 도끼를 한 줄로 세우고, 구멍을 꿰뚫어 보게 한다.</summary>
        private void BeginAxeTutorial()
        {
            _playArea.HideFrame();
            _axeRow.Show(_playArea.Front);
            _shotJudge.Target = _axeRow;
            _shotJudge.IsArmed = true;
            _axeArrowsLeft = _config.AxeTutorialArrows;
            _axePassed = false;
            _axeEndLeft = -1f;
            _calibrationView.ShowAxeTutorial(_axeArrowsLeft, missed: false);
            _phase = Phase.AxeTutorial;
        }

        /// <summary>도끼 튜토리얼에서 쏜 한 발. 점수·게임 기록에는 넣지 않는다.</summary>
        private void OnAxeShot(ShotResult result)
        {
            if (_axeEndLeft >= 0f) return;
            _axeArrowsLeft--;
            Debug.Log($"[AxeTutorial] passed={result.Hit} offset={result.OffsetDegrees:F1}° arrowsLeft={_axeArrowsLeft}");
            if (result.Hit || _axeArrowsLeft <= 0)
            {
                _axePassed = result.Hit;
                _shotJudge.IsArmed = false;
                _axeEndLeft = _config.AxeTutorialEndSeconds;
                _calibrationView.ShowAxeTutorialEnd(_axePassed);
                return;
            }
            _calibrationView.ShowAxeTutorial(_axeArrowsLeft, missed: true);
        }

        /// <summary>통과했으면 1스테이지로, 화살을 다 쓰도록 못 했으면 메인 화면으로 돌아간다.</summary>
        private void FinishAxeTutorial()
        {
            _axeEndLeft = -1f;
            if (!_axePassed)
            {
                ShowMainMenu();
                return;
            }
            _axeRow.Hide();
            _calibrationView.Hide();
            BeginStage();
        }

        private void BeginStage()
        {
            _stageRun = new StageRun(_stageIndex + 1, _config.ArrowsPerStage, _config.StageTimeLimitSeconds);
            _playArea.HideFrame();
            _playArea.SetDistance(CurrentStage.MonsterDistanceMeters);
            ShowMonster(CurrentStage);
            _shotJudge.IsArmed = true;
            _stageHud.Show(_stageRun);
            _phase = Phase.Playing;
            StageStarted?.Invoke(_stageRun, CurrentStage);
        }

        /// <summary>쏜 화살이 닿았을 때. 맞았는지는 쏜 순간 이미 판정돼 있다.</summary>
        private void OnShotLanded(ShotResult result)
        {
            if (_phase == Phase.AxeTutorial)
            {
                OnAxeShot(result);
                return;
            }
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
            int stageHits = 0;
            foreach (ShotResult shot in _stageRun.Shots)
                if (shot.Hit) stageHits++;
            _stageSummaries.Add(new StageSummary(_stageRun.StageNumber, _stageRun.IsCleared, stageHits, _stageRun.Shots.Count));
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
            _resultView.Show(_lastStageResult, CurrentStage, hasNextStage ? NextStageLabel : FinalResultLabel, _config.ResultCountdownSeconds);
            _phase = Phase.Result;
        }

        private void OnResultCountdownFinished()
        {
            if (_phase != Phase.Result) return;
            _resultView.Hide();
            if (_stageIndex + 1 >= _stages.Count)
            {
                ShowFinalResult();
                return;
            }

            _stageIndex++;
            _gameplayHud.SetActive(true);
            BeginStage();
        }

        /// <summary>마지막 스테이지까지 마쳤을 때. 게임 끝을 알리고, 버튼을 누를 때까지 최종 결과를 보여 준다.</summary>
        private void ShowFinalResult()
        {
            StageResult totals = StageResult.FromShots(_stageIndex + 1, false, _allShots);
            _finalResultView.Show(_stageSummaries, totals, CurrentStage.ResultBackground);
            _phase = Phase.FinalResult;
            GameEnded?.Invoke(true, totals);
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
