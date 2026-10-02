using System.Collections.Generic;
using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 스테이지 한 판의 진행 기록: 남은 화살, 남은 시간, 쏜 발의 판정.
    /// 몬스터를 맞히면 바로 클리어, 화살을 다 쓰거나 시간이 다 되면 실패로 끝난다.
    /// </summary>
    public class StageRun
    {
        private readonly List<ShotResult> _shots = new();

        public StageRun(int stageNumber, int arrows, float seconds)
        {
            StageNumber = stageNumber;
            ArrowsTotal = arrows;
            ArrowsLeft = arrows;
            TimeLeft = seconds;
        }

        /// <summary>1부터 시작하는 스테이지 번호.</summary>
        public int StageNumber { get; }
        public int ArrowsTotal { get; }
        public int ArrowsLeft { get; private set; }
        public float TimeLeft { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsOver => IsCleared || ArrowsLeft <= 0 || TimeLeft <= 0f;
        public IReadOnlyList<ShotResult> Shots => _shots;

        public void Tick(float dt) => TimeLeft = Mathf.Max(0f, TimeLeft - dt);

        public void Record(ShotResult shot)
        {
            _shots.Add(shot);
            ArrowsLeft = Mathf.Max(0, ArrowsLeft - 1);
            if (shot.Hit) IsCleared = true;
        }
    }
}
