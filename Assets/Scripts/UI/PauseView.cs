using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>
    /// AR 게임 화면 오른쪽 위의 정지 버튼과 정지 창("계속하기" / "메인 화면으로").
    /// 버튼은 AR 게임 화면(튜토리얼 포함)에서만 보이고, 정지 창은 게임이 멈춘 동안만 뜬다.
    /// 정지 창의 어두운 배경이 뒤쪽 버튼(방향 정하기 등)을 눌리지 않게 막는다.
    /// </summary>
    public class PauseView : MonoBehaviour
    {
        [SerializeField] private GameFlow _game;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private GameObject _popup;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _menuButton;

        private void Awake()
        {
            _pauseButton.onClick.AddListener(_game.Pause);
            _resumeButton.onClick.AddListener(_game.Resume);
            _menuButton.onClick.AddListener(_game.ShowMainMenu);
            Refresh();
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            SetActive(_pauseButton.gameObject, _game.IsInGameplay && !_game.IsPaused);
            SetActive(_popup, _game.IsPaused);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active) target.SetActive(active);
        }
    }
}
