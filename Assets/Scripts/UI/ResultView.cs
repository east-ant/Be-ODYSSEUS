using System;
using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>
    /// 스테이지 결과 화면. AR 위를 덮는 스크린 UI로 뜬다.
    /// 왼쪽에 결과 카드(제목, 최종 점수, 명중률, 조준 안정도), 오른쪽에 스테이지 몬스터(대기 애니메이션),
    /// 오른쪽 아래에 다음으로 넘어가기까지의 카운트다운(5 → 1)을 보여 주고, 다 세면 CountdownFinished를 알린다.
    /// 몬스터는 씬에서 정해 둔 칸 안에 그린다. 애니메이션 프레임은 모두 같은 배율로, 발밑을 칸 바닥 가운데에 맞춰서
    /// 프레임이 바뀌어도 크기와 서 있는 자리가 흔들리지 않는다.
    /// </summary>
    public class ResultView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Image _background;
        [SerializeField] private Image _monster;
        [SerializeField] private GameObject _trophy;
        [SerializeField] private Text _title;
        [SerializeField] private Text _scoreValue;
        [SerializeField] private Text _hitRateValue;
        [SerializeField] private Text _stabilityValue;
        [SerializeField] private Text _countdownLabel;
        [SerializeField] private Text _countdownNumber;
        [SerializeField] private Color _clearTitleColor = new(1f, 0.82f, 0.25f);
        [SerializeField] private Color _failTitleColor = new(0.85f, 0.85f, 0.9f);

        private float _countdownLeft;
        private bool _isCounting;

        private RectTransform _monsterRect;
        private Vector2 _monsterBoxSize;
        private Vector2 _monsterBoxPivot;
        private Sprite[] _monsterFrames;
        private float _monsterFramesPerSecond;
        private float _monsterScale;
        private float _monsterFrameTime;
        private int _monsterFrameIndex;

        public event Action CountdownFinished;

        private void Awake()
        {
            // 씬에서 정한 몬스터 칸(크기, 바닥 가운데 기준점)을 기억해 둔다.
            _monsterRect = _monster.rectTransform;
            _monsterBoxSize = _monsterRect.sizeDelta;
            _monsterBoxPivot = _monsterRect.pivot;
        }

        public void Show(StageResult result, StageDefinition stage, string countdownLabel, float countdownSeconds)
        {
            _background.sprite = stage.ResultBackground;
            ShowMonster(stage);

            _trophy.SetActive(result.Cleared);
            _title.text = $"{result.StageNumber}STAGE {(result.Cleared ? "CLEAR!" : "FAILED")}";
            _title.color = result.Cleared ? _clearTitleColor : _failTitleColor;

            _scoreValue.text = result.TotalScore.ToString("N0");
            _hitRateValue.text = $"{result.HitRate01 * 100f:0}% ({result.TotalHits} / {result.TotalShots})";
            _stabilityValue.text = result.HasShots ? $"{result.AverageStability01 * 100f:0}%" : "-";

            _countdownLabel.text = countdownLabel;
            _countdownLeft = countdownSeconds;
            _isCounting = true;
            UpdateCountdownNumber();
            _root.SetActive(true);
        }

        public void Hide()
        {
            _isCounting = false;
            _root.SetActive(false);
        }

        /// <summary>몬스터 대기 애니메이션을 처음부터 튼다. 애니메이션이 없으면 몬스터 그림 한 장을 칸에 맞춰 보여 준다.</summary>
        private void ShowMonster(StageDefinition stage)
        {
            MonsterAnimationSet animation = stage.MonsterAnimation;
            _monsterFrames = animation != null ? animation.StandingFrames : null;
            if (_monsterFrames == null || _monsterFrames.Length == 0)
            {
                _monsterFrames = null;
                _monster.preserveAspect = true;
                _monsterRect.sizeDelta = _monsterBoxSize;
                _monsterRect.pivot = _monsterBoxPivot;
                _monster.sprite = stage.MonsterSprite;
                return;
            }

            // 가장 큰 프레임이 칸에 딱 들어가는 배율 하나를 모든 프레임에 쓴다.
            Vector2 largest = Vector2.zero;
            foreach (Sprite frame in _monsterFrames) largest = Vector2.Max(largest, frame.rect.size);
            _monsterScale = Mathf.Min(_monsterBoxSize.x / largest.x, _monsterBoxSize.y / largest.y);
            _monsterFramesPerSecond = animation.StandingFramesPerSecond;
            _monsterFrameTime = 0f;
            _monster.preserveAspect = false;
            SetMonsterFrame(0);
        }

        private void SetMonsterFrame(int index)
        {
            Sprite frame = _monsterFrames[index];
            _monsterFrameIndex = index;
            _monster.sprite = frame;
            _monsterRect.sizeDelta = frame.rect.size * _monsterScale;
            // 그림의 기준점(발밑)을 칸의 기준점(바닥 가운데) 자리에 둔다.
            _monsterRect.pivot = frame.pivot / frame.rect.size;
        }

        private void TickMonster(float dt)
        {
            _monsterFrameTime += dt;
            int index = (int)(_monsterFrameTime * _monsterFramesPerSecond) % _monsterFrames.Length;
            if (index != _monsterFrameIndex) SetMonsterFrame(index);
        }

        private void Update()
        {
            if (_monsterFrames != null && _root.activeSelf) TickMonster(Time.unscaledDeltaTime);
            if (!_isCounting) return;
            _countdownLeft -= Time.unscaledDeltaTime;
            UpdateCountdownNumber();
            if (_countdownLeft > 0f) return;

            _isCounting = false;
            CountdownFinished?.Invoke();
        }

        // 5.0초 남음 → "5", 0.1초 남음 → "1".
        private void UpdateCountdownNumber()
        {
            _countdownNumber.text = Mathf.Max(1, Mathf.CeilToInt(_countdownLeft)).ToString();
        }
    }
}
