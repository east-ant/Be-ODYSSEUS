using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>방향 정하기 단계의 안내 문구와 진행 막대.</summary>
    public class CalibrationView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _message;
        [Tooltip("Image Type이 Filled인 막대. 가만히 있는 시간만큼 찬다.")]
        [SerializeField] private Image _progressFill;

        public void Show(string message, float progress01)
        {
            if (!_panel.activeSelf) _panel.SetActive(true);
            _message.text = message;
            _progressFill.fillAmount = progress01;
        }

        public void Hide() => _panel.SetActive(false);
    }
}
