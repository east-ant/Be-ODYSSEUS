using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace BeOdysseus.Dev
{
    /// <summary>
    /// 개발용 진단 표시. AR 세션 상태, 추적이 안 되는 이유, 카메라 위치·회전, FPS를 화면 왼쪽 위에 글자로 띄운다.
    /// 폰에서 화면이 검거나 물체가 안 보일 때 원인을 바로 알 수 있게 한다.
    /// </summary>
    public class ARDebugOverlay : MonoBehaviour
    {
        [SerializeField] private Camera _arCamera;
        [SerializeField] private int _fontSize = 32;
        [SerializeField] private Color _textColor = Color.yellow;

        private GUIStyle _style;
        private float _smoothedFps;

        private void Awake()
        {
            if (_arCamera == null) _arCamera = Camera.main;
        }

        private void Update()
        {
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-5f);
            _smoothedFps = Mathf.Lerp(_smoothedFps, fps, 0.1f);
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = _fontSize, normal = { textColor = _textColor } };

            string pose = "camera: 없음";
            if (_arCamera != null)
            {
                Transform t = _arCamera.transform;
                pose = $"pos: {t.position.ToString("F2")}\nrot: {t.eulerAngles.ToString("F0")}";
            }

            string text = $"AR: {ARSession.state} ({ARSession.notTrackingReason})\n{pose}\nFPS: {_smoothedFps:F0}";
            GUI.Label(new Rect(20, 20, Screen.width - 40, Screen.height - 40), text, _style);
        }
    }
}
