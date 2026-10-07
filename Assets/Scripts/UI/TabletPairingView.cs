using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>
    /// 메인 화면의 "태블릿 연결" 버튼과 연결 코드 창.
    /// 버튼을 누르면 4자리 코드가 뜨고, 태블릿 앱에 그 코드를 넣으면 연결된다.
    /// 연결되면 잠시 "연결됨"을 보여 주고 창을 닫는다.
    /// </summary>
    public class TabletPairingView : MonoBehaviour
    {
        private const float AutoCloseSeconds = 1.5f;
        private static readonly Color ConnectedColor = new(1f, 0.82f, 0.25f);
        private static readonly Color NormalColor = new(1f, 0.92f, 0.7f);

        [SerializeField] private TabletLink _link;
        [SerializeField] private Button _openButton;
        [SerializeField] private Text _openButtonLabel;
        [SerializeField] private GameObject _popup;
        [SerializeField] private Text _code;
        [SerializeField] private Text _guide;
        [SerializeField] private Text _status;
        [SerializeField] private Button _unpairButton;
        [SerializeField] private Button _closeButton;

        private TabletLinkState _shownState;
        private float _closeAt = -1f;

        private void Awake()
        {
            _openButton.onClick.AddListener(Open);
            _closeButton.onClick.AddListener(Close);
            _unpairButton.onClick.AddListener(() => _link.Unpair());
            _popup.SetActive(false);
        }

        private void Open()
        {
            _popup.SetActive(true);
            _closeAt = -1f;
            if (!_link.IsConnected) _link.StartPairing();
            _shownState = _link.State;
        }

        private void Close()
        {
            _link.CancelPairing();
            _popup.SetActive(false);
        }

        private void Update()
        {
            bool connected = _link.IsConnected;
            _openButtonLabel.text = connected ? "태블릿 연결됨"
                : _link.State == TabletLinkState.Reconnecting ? "태블릿 찾는 중…" : "태블릿 연결";
            _openButtonLabel.color = connected ? ConnectedColor : NormalColor;

            if (!_popup.activeSelf) return;

            // 연결 해제 등으로 짝이 없어졌으면 바로 새 코드로 연결을 시작한다.
            if (_link.State == TabletLinkState.NotPaired) _link.StartPairing();
            if (_shownState == TabletLinkState.Pairing && connected) _closeAt = Time.unscaledTime + AutoCloseSeconds;
            _shownState = _link.State;
            ShowState();

            if (_closeAt > 0f && Time.unscaledTime >= _closeAt) Close();
        }

        private void ShowState()
        {
            switch (_link.State)
            {
                case TabletLinkState.Pairing:
                    _code.text = _link.PairingCode;
                    _guide.text = "태블릿 앱에 이 코드를 입력하세요";
                    _status.text = "태블릿을 기다리는 중…";
                    _unpairButton.gameObject.SetActive(false);
                    break;

                case TabletLinkState.Connected:
                    _code.text = "연결됨";
                    _guide.text = string.IsNullOrEmpty(_link.TabletDevice) ? "태블릿과 연결되었습니다" : $"태블릿: {_link.TabletDevice}";
                    _status.text = "";
                    _unpairButton.gameObject.SetActive(true);
                    break;

                case TabletLinkState.Reconnecting:
                    _code.text = "…";
                    _guide.text = "연결했던 태블릿을 다시 찾는 중이에요";
                    _status.text = "태블릿 앱이 켜져 있고 같은 와이파이인지 확인하세요";
                    _unpairButton.gameObject.SetActive(true);
                    break;
            }
        }
    }
}
