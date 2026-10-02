using System;
using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>
    /// 튜토리얼(중앙점 정하기) 화면. AR 화면 위쪽에 안내 문구, 아래쪽에 "방향 정하기" 버튼을 띄운다.
    /// 중앙점은 이 화면에서만 정할 수 있다.
    /// </summary>
    public class CalibrationView : MonoBehaviour
    {
        private const string AskTitle = "활을 쏠 중앙점을 설정해주세요!";
        private static readonly Color DisabledButtonColor = new(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Text _subtitle;
        [SerializeField] private Button _button;
        [SerializeField] private Text _buttonLabel;

        public event Action ButtonPressed;

        private void Awake()
        {
            _button.onClick.AddListener(() => ButtonPressed?.Invoke());
        }

        /// <summary>AR이 아직 공간을 잡지 못해 방향을 정할 수 없을 때.</summary>
        public void ShowWaitingForAr() =>
            Show(AskTitle, "AR 준비 중… 주변을 천천히 비춰 주세요", "방향 정하기", false);

        public void ShowReady() =>
            Show(AskTitle, "화면 가운데 조준점을 쏠 방향에 맞추고 버튼을 누르세요", "방향 정하기", true);

        /// <summary>중앙점을 정한 뒤 스테이지 시작까지 세는 동안. 버튼을 다시 누르면 다시 정한다.</summary>
        public void ShowConfirmed(int stageNumber, int secondsLeft) =>
            Show("중앙점 설정 완료!", $"이 범위 안에 몬스터가 나타나요 · {secondsLeft}초 후 {stageNumber}스테이지 시작", "다시 정하기", true);

        public void Hide() => _panel.SetActive(false);

        private void Show(string title, string subtitle, string buttonLabel, bool interactable)
        {
            if (!_panel.activeSelf) _panel.SetActive(true);
            _title.text = title;
            _subtitle.text = subtitle;
            _buttonLabel.text = buttonLabel;
            _button.interactable = interactable;
            _button.image.color = interactable ? Color.white : DisabledButtonColor;
        }
    }
}
