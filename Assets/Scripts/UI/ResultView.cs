using System;
using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>
    /// 스테이지 결과 화면. AR 위를 덮는 스크린 UI로 뜬다.
    /// 왼쪽에 결과 카드(제목, 최종 점수, 명중률, 조준 안정도), 오른쪽에 스테이지 몬스터,
    /// 오른쪽 아래에 다음으로 넘어가기까지의 카운트다운(5 → 1)을 보여 주고, 다 세면 CountdownFinished를 알린다.
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

        public event Action CountdownFinished;

        public void Show(StageResult result, StageDefinition stage, string countdownLabel, float countdownSeconds)
        {
            _background.sprite = stage.ResultBackground;
            _monster.sprite = stage.MonsterSprite;

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

        private void Update()
        {
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
