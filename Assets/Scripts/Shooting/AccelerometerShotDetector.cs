using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BeOdysseus
{
    /// <summary>
    /// 폰 가속도 센서로 시위를 놓는 순간의 충격을 감지한다.
    /// 화면은 초당 30번만 갱신되지만, 센서 값은 프레임 사이에 들어온 것까지 하나도 빠짐없이 받아서 짧은 충격도 놓치지 않는다.
    /// 충격 = 지금 가속도와, 천천히 따라가는 평균(중력 + 손의 느린 움직임) 사이의 차이(g).
    /// </summary>
    public class AccelerometerShotDetector : MonoBehaviour
    {
        // 평균이 실제 값을 따라가는 시간(초). 충격처럼 빠른 변화는 평균에 거의 반영되지 않아 차이로 드러난다.
        private const double BaselineSeconds = 0.3;
        // 진단 표시용 "최근 최고 충격"을 들고 있는 시간(초).
        private const double PeakHoldSeconds = 2.0;

        [SerializeField] private GameConfig _config;

        private Accelerometer _accelerometer;
        private Vector3 _baseline;
        private bool _hasBaseline;
        private bool _isAboveThreshold;
        private bool _warnedTimeMismatch;
        private double _lastSampleTime;
        private double _peakTime;
        private double _rateWindowStart;
        private int _rateWindowCount;

        /// <summary>충격이 기준값을 넘는 순간마다(넘었다가 내려온 뒤 다시 넘을 때) 한 번씩.</summary>
        public event Action<ShotEvent> Fired;

        public bool IsAvailable => _accelerometer != null;
        /// <summary>가속도 크기(g). 가만히 있으면 중력 때문에 약 1.</summary>
        public float RawMagnitude { get; private set; }
        /// <summary>지금 충격(g).</summary>
        public float Shock { get; private set; }
        /// <summary>최근 2초 동안 가장 큰 충격(g).</summary>
        public float RecentPeak { get; private set; }
        /// <summary>실제로 들어오는 센서 값 개수(초당).</summary>
        public float SamplesPerSecond { get; private set; }
        public float RequestedHz => _config.AccelerometerSamplingHz;

        private void OnEnable()
        {
            _accelerometer = Accelerometer.current;
            if (_accelerometer == null) return; // 에디터처럼 센서가 없는 환경

            InputSystem.EnableDevice(_accelerometer);
            try
            {
                _accelerometer.samplingFrequency = _config.AccelerometerSamplingHz;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Accelerometer] 측정 빈도 설정 실패: {e.Message}");
            }

            _hasBaseline = false;
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device != _accelerometer) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (!_accelerometer.acceleration.ReadValueFromEvent(eventPtr, out Vector3 value)) return;
            ProcessSample(value, SampleTime(eventPtr.time));
        }

        /// <summary>
        /// 센서 값의 시각. 조준 기록(InputState.currentTime)과 같은 시간축이어야 "충격 직전 방향"을 찾을 수 있어서,
        /// 기기에 따라 시간축이 1초 넘게 어긋나면 현재 입력 시각으로 대신한다.
        /// </summary>
        private double SampleTime(double eventTime)
        {
            double now = InputState.currentTime;
            if (Math.Abs(now - eventTime) <= 1.0) return eventTime;
            if (!_warnedTimeMismatch)
            {
                Debug.LogWarning($"[Accelerometer] 센서 시각({eventTime:F3})과 입력 시각({now:F3})이 어긋나 입력 시각을 씁니다.");
                _warnedTimeMismatch = true;
            }
            return now;
        }

        private void ProcessSample(Vector3 acceleration, double time)
        {
            if (!_hasBaseline)
            {
                _baseline = acceleration;
                _lastSampleTime = time;
                _rateWindowStart = time;
                _hasBaseline = true;
            }

            float dt = (float)Math.Max(time - _lastSampleTime, 0.0);
            _lastSampleTime = time;

            RawMagnitude = acceleration.magnitude;
            Shock = (acceleration - _baseline).magnitude;
            _baseline = Vector3.Lerp(_baseline, acceleration, 1f - Mathf.Exp(-dt / (float)BaselineSeconds));

            UpdatePeak(time);
            CountSampleRate(time);
            DetectCrossing(time);
        }

        private void UpdatePeak(double time)
        {
            if (Shock < RecentPeak && time - _peakTime < PeakHoldSeconds) return;
            RecentPeak = Shock;
            _peakTime = time;
        }

        private void CountSampleRate(double time)
        {
            _rateWindowCount++;
            double elapsed = time - _rateWindowStart;
            if (elapsed < 1.0) return;
            SamplesPerSecond = (float)(_rateWindowCount / elapsed);
            _rateWindowCount = 0;
            _rateWindowStart = time;
        }

        private void DetectCrossing(double time)
        {
            bool isAbove = Shock >= _config.ShotAccelerationThreshold;
            if (isAbove && !_isAboveThreshold) Fired?.Invoke(new ShotEvent(time, ShotSource.Accelerometer, Shock));
            _isAboveThreshold = isAbove;
        }
    }
}
