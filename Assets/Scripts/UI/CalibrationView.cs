using System;
using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>
    /// 튜토리얼(중앙점 정하기) 화면. AR 화면 위쪽에 안내 문구, 아래쪽에 "방향 정하기" 버튼을 띄운다.
    /// 중앙점은 이 화면에서만 정할 수 있다. 버튼 대신 활을 쏴도 된다(GameFlow).
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
        [Tooltip("도끼 시험을 통과했을 때 제목 글씨 크기. 부제는 숨기고 제목 한 줄이 안내 상자를 꽉 채운다.")]
        [SerializeField, Min(10)] private int _bigTitleFontSize = 100;
        [Tooltip("제목 한 줄로 채울 때 안내 상자 위아래 여백(px).")]
        [SerializeField, Min(0f)] private float _bigTitlePadding = 20f;

        // 씬에 놓인 제목 자리와 크기. 제목을 크게 띄웠다가 되돌릴 때 쓴다.
        private bool _hasTitleLayout;
        private Vector2 _titleAnchorMin;
        private Vector2 _titleAnchorMax;
        private Vector2 _titleOffsetMin;
        private Vector2 _titleOffsetMax;
        private int _titleFontSize;

        public event Action ButtonPressed;

        private void Awake()
        {
            _button.onClick.AddListener(() => ButtonPressed?.Invoke());
            RectTransform title = _title.rectTransform;
            _titleAnchorMin = title.anchorMin;
            _titleAnchorMax = title.anchorMax;
            _titleOffsetMin = title.offsetMin;
            _titleOffsetMax = title.offsetMax;
            _titleFontSize = _title.fontSize;
            _hasTitleLayout = true;
        }

        /// <summary>AR이 아직 공간을 잡지 못해 방향을 정할 수 없을 때.</summary>
        public void ShowWaitingForAr() =>
            Show(AskTitle, "AR 준비 중… 주변을 천천히 비춰 주세요", "방향 정하기", false);

        public void ShowReady() =>
            Show(AskTitle, "조준점을 쏠 방향에 맞추고 활을 쏘거나 버튼을 누르세요", "방향 정하기", true);

        /// <summary>
        /// 중앙점을 정한 뒤 다음 단계(도끼 튜토리얼, 꺼져 있으면 스테이지) 시작까지 세는 동안. 버튼을 다시 누르면 다시 정한다.
        /// </summary>
        /// <param name="next">시작할 단계 이름. 예: "튜토리얼", "1스테이지"</param>
        public void ShowConfirmed(string next, int secondsLeft) =>
            Show("중앙점 설정 완료!", $"이 범위 안에 몬스터가 나타나요 · {secondsLeft}초 후 {next} 시작", "다시 정하기", true);

        /// <summary>도끼 튜토리얼 중. 건너뛸 수 없으니 버튼은 숨긴다.</summary>
        public void ShowAxeTutorial(int arrowsLeft, bool missed) =>
            Show(missed ? "아깝다! 다시 노려 보세요" : "오디세우스의 도끼 시험",
                $"도끼 사이 구멍으로 화살을 꿰뚫어 보세요 · 남은 화살 {arrowsLeft}", "", true, false);

        /// <summary>
        /// 도끼 튜토리얼이 끝났을 때. 통과하면 "시험에 통과하였습니다!" 한 줄만 상자를 꽉 채워 크게 띄우고 스테이지로 간다.
        /// 못 하면 "GAME OVER"와 함께 띄우고 메인 화면으로 간다.
        /// </summary>
        public void ShowAxeTutorialEnd(bool passed)
        {
            if (passed) Show("시험에 통과하였습니다!", "", "", true, false, bigTitle: true);
            else Show("시험을 통과하지 못하였습니다..", "GAME OVER", "", true, false);
        }

        public void Hide() => _panel.SetActive(false);

        private void Show(string title, string subtitle, string buttonLabel, bool interactable, bool showButton = true, bool bigTitle = false)
        {
            if (!_panel.activeSelf) _panel.SetActive(true);
            SetBigTitle(bigTitle);
            _title.text = title;
            _subtitle.text = subtitle;
            _buttonLabel.text = buttonLabel;
            if (_button.gameObject.activeSelf != showButton) _button.gameObject.SetActive(showButton);
            _button.interactable = interactable;
            _button.image.color = interactable ? Color.white : DisabledButtonColor;
        }

        /// <summary>
        /// big이면 부제를 숨기고 제목이 안내 상자 전체(위아래 여백만 빼고)를 차지하게 크게 쓴다. 아니면 씬에 놓인 대로 되돌린다.
        /// 에디터 화면 미리보기(플레이 전)에서는 Awake가 불리지 않아 원래 자리를 모르니 손대지 않는다.
        /// </summary>
        private void SetBigTitle(bool big)
        {
            if (!_hasTitleLayout) return;
            RectTransform title = _title.rectTransform;
            if (_subtitle.gameObject.activeSelf == big) _subtitle.gameObject.SetActive(!big);
            if (big)
            {
                title.anchorMin = Vector2.zero;
                title.anchorMax = Vector2.one;
                title.offsetMin = new Vector2(_titleOffsetMin.x, _bigTitlePadding);
                title.offsetMax = new Vector2(_titleOffsetMax.x, -_bigTitlePadding);
                _title.fontSize = _bigTitleFontSize;
                return;
            }
            title.anchorMin = _titleAnchorMin;
            title.anchorMax = _titleAnchorMax;
            title.offsetMin = _titleOffsetMin;
            title.offsetMax = _titleOffsetMax;
            _title.fontSize = _titleFontSize;
        }
    }
}
