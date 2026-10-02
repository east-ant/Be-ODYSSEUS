using System;
using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>
    /// 게임 시작 화면. AR 위에 화면 전체를 덮는 일반(스크린) UI로 뜬다.
    /// AR은 뒤에서 미리 켜져 있어서, 시작을 누르면 바로 방향 정하기로 넘어갈 수 있다.
    /// </summary>
    public class MainMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _startButton;

        public event Action StartPressed;

        private void Awake()
        {
            _startButton.onClick.AddListener(() => StartPressed?.Invoke());
        }

        public void Show() => _root.SetActive(true);
        public void Hide() => _root.SetActive(false);
    }
}
