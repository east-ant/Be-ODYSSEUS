using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace BeOdysseus
{
    /// <summary>한 프레임의 조준 상태. 시각은 Input System 시간축이라 센서 값 시각과 바로 비교할 수 있다.</summary>
    public readonly struct AimSample
    {
        public readonly double Time;
        public readonly Vector3 Position;
        public readonly Vector3 Forward;

        public AimSample(double time, Vector3 position, Vector3 forward)
        {
            Time = time;
            Position = position;
            Forward = forward;
        }
    }

    /// <summary>
    /// 최근 몇 초 동안의 조준 방향을 계속 기록한다.
    /// 발사 충격으로 화면이 흔들리기 "직전" 방향으로 판정하고, 직전 1초의 흔들림(조준 안정도)을 재는 데 쓴다.
    /// </summary>
    public class AimHistory : MonoBehaviour
    {
        // 안정도 구간(기본 1초)과 판정 시점 여유를 넉넉히 덮는 기록 길이(초).
        private const double KeepSeconds = 3.0;

        [Tooltip("조준 방향을 주는 AR 카메라.")]
        [SerializeField] private Transform _aim;

        private readonly List<AimSample> _samples = new();

        // AR 카메라 위치는 Update 중에 갱신되므로 LateUpdate에서 기록한다.
        private void LateUpdate()
        {
            double now = InputState.currentTime;
            _samples.Add(new AimSample(now, _aim.position, _aim.forward));

            int stale = 0;
            while (stale < _samples.Count && _samples[stale].Time < now - KeepSeconds) stale++;
            if (stale > 0) _samples.RemoveRange(0, stale);
        }

        /// <summary>주어진 시각과 같거나 그보다 앞선 기록 중 가장 최근 것.</summary>
        public bool TryGetAtOrBefore(double time, out AimSample sample)
        {
            for (int i = _samples.Count - 1; i >= 0; i--)
            {
                if (_samples[i].Time > time) continue;
                sample = _samples[i];
                return true;
            }

            sample = default;
            return false;
        }

        /// <summary>[from, to] 구간의 기록을 into에 담는다.</summary>
        public void CollectBetween(double from, double to, List<AimSample> into)
        {
            into.Clear();
            foreach (AimSample s in _samples)
                if (s.Time >= from && s.Time <= to) into.Add(s);
        }
    }
}
