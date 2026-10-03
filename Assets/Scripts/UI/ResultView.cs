using System;
using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>
    /// 스테이지 결과 화면. AR 위를 덮는 스크린 UI로 뜬다.
    /// 왼쪽에 결과 카드(제목, 최종 점수, 명중률, 조준 안정도), 오른쪽에 스테이지 몬스터,
    /// 오른쪽 아래에 다음으로 넘어가기까지의 카운트다운(5 → 1)을 보여 주고, 다 세면 CountdownFinished를 알린다.
    /// 몬스터는 고해상도 그림 한 장을 씬에서 정해 둔 칸에 발을 바닥에 붙여 그리고, 코드로 대기 동작을 준다.
    /// (게임 중 애니메이션 그림은 한 장이 110px 정도로 작아서, 크게 띄우는 결과 화면에서는 흐릿하게 보인다.)
    /// 걷는 몬스터는 발을 기준으로 숨 쉬듯 들썩이고, 떠다니는 몬스터는 위아래로 둥실거린다.
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

        [Header("몬스터 대기 동작")]
        [Tooltip("걷는 몬스터: 숨을 들이쉴 때 키가 커지는 비율(0.025 = 2.5%).")]
        [SerializeField, Range(0f, 0.1f)] private float _breathAmount = 0.025f;
        [Tooltip("걷는 몬스터: 숨 한 번(들이쉬고 내쉬기)에 걸리는 시간(초).")]
        [SerializeField, Min(0.1f)] private float _breathSeconds = 2.4f;
        [Tooltip("떠다니는 몬스터: 위아래로 오르내리는 폭(화면 기준 픽셀, 1920×1080 기준). 한 번 오르내리는 시간은 몬스터 애니메이션 설정을 따른다.")]
        [SerializeField, Min(0f)] private float _floatBobPixels = 18f;

        private float _countdownLeft;
        private bool _isCounting;

        private RectTransform _monsterRect;
        private Vector2 _monsterBoxSize;
        private Vector2 _monsterBasePosition;
        private bool _monsterFloats;
        private float _monsterFloatSeconds;
        private float _monsterIdleTime;

        public event Action CountdownFinished;

        private void Awake()
        {
            // 씬에서 정한 몬스터 칸(크기, 바닥 가운데 위치)을 기억해 둔다.
            _monsterRect = _monster.rectTransform;
            _monsterBoxSize = _monsterRect.sizeDelta;
            _monsterBasePosition = _monsterRect.anchoredPosition;
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

        /// <summary>몬스터 그림을 칸에 꼭 맞게(비율 유지, 발은 칸 바닥) 놓고 대기 동작을 처음부터 시작한다.</summary>
        private void ShowMonster(StageDefinition stage)
        {
            Sprite sprite = stage.MonsterSprite;
            _monster.sprite = sprite;
            Vector2 size = sprite.rect.size;
            _monsterRect.sizeDelta = size * Mathf.Min(_monsterBoxSize.x / size.x, _monsterBoxSize.y / size.y);

            MonsterAnimationSet animation = stage.MonsterAnimation;
            _monsterFloats = animation != null && animation.Movement == MonsterMovement.Float;
            _monsterFloatSeconds = animation != null ? animation.FloatCycleSeconds : 1f;
            _monsterIdleTime = 0f;
            TickMonster(0f);
        }

        private void TickMonster(float dt)
        {
            _monsterIdleTime += dt;
            if (_monsterFloats)
            {
                float bob = Mathf.Sin(_monsterIdleTime * 2f * Mathf.PI / _monsterFloatSeconds) * _floatBobPixels;
                _monsterRect.anchoredPosition = _monsterBasePosition + new Vector2(0f, bob);
                _monsterRect.localScale = Vector3.one;
                return;
            }

            // 0(내쉼) → 1(들이쉼) → 0. 칸의 기준점이 바닥 가운데라 발은 제자리에 있고 몸만 위로 커진다.
            float breath = 0.5f - 0.5f * Mathf.Cos(_monsterIdleTime * 2f * Mathf.PI / _breathSeconds);
            float grow = _breathAmount * breath;
            _monsterRect.anchoredPosition = _monsterBasePosition;
            _monsterRect.localScale = new Vector3(1f + grow * 0.4f, 1f + grow, 1f);
        }

        private void Update()
        {
            if (_root.activeSelf) TickMonster(Time.unscaledDeltaTime);
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
