using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeOdysseus
{
    /// <summary>발사 한 번의 판정 결과. 나중에 "왜 맞았/빗나갔는지" 추적할 수 있게 판정에 쓴 값을 모두 담는다.</summary>
    public readonly struct ShotResult
    {
        public readonly ShotEvent Shot;
        public readonly AimSample Aim;
        public readonly bool Hit;
        /// <summary>조준점과 몬스터 중심 사이 각도(도). 몬스터가 없으면 NaN.</summary>
        public readonly float OffsetDegrees;
        public readonly float Accuracy01;
        /// <summary>발사 직전 구간의 평균 흔들림(도).</summary>
        public readonly float WobbleDegrees;
        public readonly float Stability01;

        public ShotResult(ShotEvent shot, AimSample aim, bool hit, float offsetDegrees, float accuracy01, float wobbleDegrees, float stability01)
        {
            Shot = shot;
            Aim = aim;
            Hit = hit;
            OffsetDegrees = offsetDegrees;
            Accuracy01 = accuracy01;
            WobbleDegrees = wobbleDegrees;
            Stability01 = stability01;
        }
    }

    /// <summary>
    /// 발사 신호를 받아 명중 여부, 정확도, 조준 안정도를 판정한다.
    /// 판정은 충격 직전 조준 방향으로 하고, 신호는 다음 Update에서 처리한다(센서 콜백 도중에 게임 상태를 바꾸지 않으려고).
    /// </summary>
    public class ShotJudge : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private AimHistory _aimHistory;
        [SerializeField] private AccelerometerShotDetector _accelerometerDetector;
        [SerializeField] private EditorShotTrigger _editorTrigger;

        private readonly Queue<ShotEvent> _pending = new();
        private readonly List<AimSample> _window = new();
        private double _lastAcceptedTime = double.NegativeInfinity;

        public event Action<ShotResult> Resolved;

        /// <summary>false면 발사 신호를 무시한다. 게임 진행이 정한다.</summary>
        public bool IsArmed { get; set; }
        public Monster Target { get; set; }

        private void OnEnable()
        {
            if (_accelerometerDetector != null) _accelerometerDetector.Fired += Enqueue;
            if (_editorTrigger != null) _editorTrigger.Fired += Enqueue;
        }

        private void OnDisable()
        {
            if (_accelerometerDetector != null) _accelerometerDetector.Fired -= Enqueue;
            if (_editorTrigger != null) _editorTrigger.Fired -= Enqueue;
        }

        private void Enqueue(ShotEvent shot) => _pending.Enqueue(shot);

        private void Update()
        {
            while (_pending.Count > 0)
            {
                ShotEvent shot = _pending.Dequeue();
                if (!IsArmed) continue;
                if (shot.Time - _lastAcceptedTime < _config.ShotCooldownSeconds) continue;
                if (!_aimHistory.TryGetAtOrBefore(shot.Time - _config.ShotAimLookbackSeconds, out AimSample aim)) continue;

                _lastAcceptedTime = shot.Time;
                ShotResult result = Evaluate(shot, aim);
                Debug.Log($"[Shot] {shot.Source} strength={shot.Strength:F2}g hit={result.Hit} offset={result.OffsetDegrees:F1}° " +
                          $"accuracy={result.Accuracy01:P0} wobble={result.WobbleDegrees:F2}° stability={result.Stability01:P0}");
                Resolved?.Invoke(result);
            }
        }

        private ShotResult Evaluate(ShotEvent shot, AimSample aim)
        {
            bool hit = false;
            float offset = float.NaN;
            float accuracy = 0f;
            if (Target != null && Target.gameObject.activeInHierarchy)
            {
                hit = Target.IsHitBy(aim.Position, aim.Forward);
                offset = Vector3.Angle(aim.Forward, Target.TargetPoint - aim.Position);
                accuracy = Mathf.Clamp01(1f - offset / _config.AccuracyZeroDegrees);
            }

            float wobble = MeasureWobbleDegrees(aim.Time);
            float stability = Mathf.InverseLerp(_config.StabilityZeroDegrees, _config.StabilityPerfectDegrees, wobble);
            return new ShotResult(shot, aim, hit, offset, accuracy, wobble, stability);
        }

        /// <summary>end 직전 구간에서 조준 방향이 평균 방향으로부터 평균 몇 도 벗어났는지. 롤은 조준점을 움직이지 않으므로 방향만 본다.</summary>
        private float MeasureWobbleDegrees(double end)
        {
            _aimHistory.CollectBetween(end - _config.StabilityWindowSeconds, end, _window);
            if (_window.Count < 2) return 0f;

            Vector3 sum = Vector3.zero;
            foreach (AimSample s in _window) sum += s.Forward;
            Vector3 mean = sum.normalized;

            float total = 0f;
            foreach (AimSample s in _window) total += Vector3.Angle(s.Forward, mean);
            return total / _window.Count;
        }
    }
}
