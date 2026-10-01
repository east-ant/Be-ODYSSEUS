namespace BeOdysseus
{
    public enum ShotSource
    {
        Accelerometer,
        Keyboard,
    }

    /// <summary>"지금 쐈다"는 신호. 시각은 Input System 시간축(InputState.currentTime)이다.</summary>
    public readonly struct ShotEvent
    {
        public readonly double Time;
        public readonly ShotSource Source;
        /// <summary>가속도 센서로 감지했을 때의 충격 크기(g). 키보드는 0.</summary>
        public readonly float Strength;

        public ShotEvent(double time, ShotSource source, float strength)
        {
            Time = time;
            Source = source;
            Strength = strength;
        }
    }
}
