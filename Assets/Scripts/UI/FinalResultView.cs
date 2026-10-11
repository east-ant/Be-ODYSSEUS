using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>
    /// 마지막 스테이지 결과 다음에 뜨는 최종 결과 화면.
    /// 스테이지마다 한 줄(클리어/실패, 명중 수)과 전체 합계(최종 점수, 명중률, 조준 안정도)를 보여 준다.
    /// 저절로 넘어가지 않고 "메인 화면으로" 버튼을 눌러야 나간다.
    /// </summary>
    public class FinalResultView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Image _background;
        [Tooltip("\"최종 결과\" 제목.")]
        [SerializeField] private Text _title;
        [Tooltip("스테이지별 줄. 스테이지 수보다 많으면 남는 줄은 숨긴다.")]
        [SerializeField] private GameObject[] _stageRows;
        [SerializeField] private Text[] _stageNames;
        [SerializeField] private Text[] _stageResults;
        [SerializeField] private Text[] _stageHits;
        [SerializeField] private Text _scoreValue;
        [SerializeField] private Text _hitRateValue;
        [SerializeField] private Text _stabilityValue;
        [SerializeField] private Button _menuButton;
        [SerializeField] private Color _clearColor = new(1f, 0.82f, 0.25f);
        [SerializeField] private Color _failColor = new(0.85f, 0.85f, 0.9f);

        /// <summary>"메인 화면으로"를 눌렀을 때.</summary>
        public event Action MenuPressed;

        private void Awake()
        {
            _menuButton.onClick.AddListener(() => MenuPressed?.Invoke());
        }

        /// <param name="stages">스테이지별 결과(그 스테이지만).</param>
        /// <param name="totals">게임 전체 누적 결과.</param>
        /// <param name="background">배경 그림(마지막 스테이지 결과 배경).</param>
        public void Show(IReadOnlyList<StageSummary> stages, StageResult totals, Sprite background)
        {
            if (background != null) _background.sprite = background;

            for (int i = 0; i < _stageRows.Length; i++)
            {
                bool used = i < stages.Count;
                _stageRows[i].SetActive(used);
                if (!used) continue;

                StageSummary stage = stages[i];
                _stageNames[i].text = $"{stage.StageNumber} STAGE";
                _stageResults[i].text = stage.Cleared ? "CLEAR" : "FAILED";
                _stageResults[i].color = stage.Cleared ? _clearColor : _failColor;
                _stageHits[i].text = $"명중 {stage.Hits} / {stage.Shots}";
            }

            _title.text = "최종 결과";
            _title.color = _clearColor;
            _scoreValue.text = totals.TotalScore.ToString("N0");
            _hitRateValue.text = $"{totals.HitRate01 * 100f:0}% ({totals.TotalHits} / {totals.TotalShots})";
            _stabilityValue.text = totals.HasShots ? $"{totals.AverageStability01 * 100f:0}%" : "-";
            _root.SetActive(true);
        }

        public void Hide() => _root.SetActive(false);
    }
}
