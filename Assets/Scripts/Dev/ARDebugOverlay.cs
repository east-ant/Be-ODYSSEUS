using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace BeOdysseus.Dev
{
    /// <summary>
    /// 개발용 진단 표시. 화면 왼쪽 위에 글자로 띄운다.
    /// - AR 세션 상태, 추적이 안 되는 이유, 카메라 위치·회전, FPS
    /// - 가속도: 크기(가만히 있으면 약 1g), 지금 충격, 최근 2초 최고 충격, 발사 기준값, 센서 측정 빈도
    /// - 점수와 마지막 발사 판정
    /// </summary>
    public class ARDebugOverlay : MonoBehaviour
    {
        [SerializeField] private Camera _arCamera;
        [SerializeField] private GameConfig _config;
        [SerializeField] private AccelerometerShotDetector _accelerometer;
        [SerializeField] private GameFlow _game;
        [SerializeField] private int _fontSize = 32;
        [SerializeField] private Color _textColor = Color.yellow;

        private readonly StringBuilder _text = new();
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
            if (_game != null && _game.IsOnMainMenu) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = _fontSize, normal = { textColor = _textColor } };

            _text.Clear();
            AppendTracking();
            AppendAccelerometer();
            AppendShots();
            GUI.Label(new Rect(20, 20, Screen.width - 40, Screen.height - 40), _text.ToString(), _style);
        }

        private void AppendTracking()
        {
            _text.Append("AR: ").Append(ARSession.state).Append(" (").Append(ARSession.notTrackingReason).Append(")\n");
            if (_arCamera != null)
            {
                Transform t = _arCamera.transform;
                _text.Append("pos: ").Append(t.position.ToString("F2")).Append("  rot: ").Append(t.eulerAngles.ToString("F0")).Append('\n');
            }
            _text.Append("FPS: ").Append(_smoothedFps.ToString("F0")).Append('\n');
        }

        private void AppendAccelerometer()
        {
            if (_accelerometer == null) return;
            if (!_accelerometer.IsAvailable)
            {
                _text.Append("accel: (no sensor)\n");
                return;
            }

            _text.Append("accel: ").Append(_accelerometer.RawMagnitude.ToString("F2")).Append("g")
                 .Append("  shock: ").Append(_accelerometer.Shock.ToString("F2")).Append("g")
                 .Append("  peak2s: ").Append(_accelerometer.RecentPeak.ToString("F2")).Append("g")
                 .Append("  fire>=").Append(_config.ShotAccelerationThreshold.ToString("F2")).Append("g\n");
            _text.Append("sensor: ").Append(_accelerometer.SamplesPerSecond.ToString("F0")).Append("Hz (req ")
                 .Append(_accelerometer.RequestedHz.ToString("F0")).Append("Hz)\n");
        }

        private void AppendShots()
        {
            if (_game == null) return;
            _text.Append("score: ").Append(_game.Score).Append(" / shots: ").Append(_game.ShotsFired).Append('\n');
            if (!_game.HasLastResult) return;

            ShotResult r = _game.LastResult;
            _text.Append("last: ").Append(r.Hit ? "HIT" : "MISS")
                 .Append("  acc ").Append(r.Accuracy01.ToString("P0"))
                 .Append("  stab ").Append(r.Stability01.ToString("P0"))
                 .Append(" (wobble ").Append(r.WobbleDegrees.ToString("F2")).Append("°)")
                 .Append("  ").Append(r.Shot.Source).Append(' ').Append(r.Shot.Strength.ToString("F2")).Append("g\n");
        }
    }
}
