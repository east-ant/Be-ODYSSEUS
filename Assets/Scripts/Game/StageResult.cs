using System.Collections.Generic;

namespace BeOdysseus
{
    /// <summary>
    /// 스테이지가 끝났을 때 결과 화면과 피드백(TTS 등)에 넘기는 값.
    /// 점수·명중률·안정도·정확도는 게임 시작부터 지금까지 쏜 모든 발의 누적이다.
    /// </summary>
    public readonly struct StageResult
    {
        public readonly int StageNumber;
        public readonly bool Cleared;
        public readonly int TotalScore;
        public readonly int TotalHits;
        public readonly int TotalShots;
        /// <summary>쏜 발이 없으면 0.</summary>
        public readonly float HitRate01;
        /// <summary>쏜 발이 없으면 0. HasShots로 구분한다.</summary>
        public readonly float AverageStability01;
        public readonly float AverageAccuracy01;

        public StageResult(int stageNumber, bool cleared, int totalScore, int totalHits, int totalShots,
                           float hitRate01, float averageStability01, float averageAccuracy01)
        {
            StageNumber = stageNumber;
            Cleared = cleared;
            TotalScore = totalScore;
            TotalHits = totalHits;
            TotalShots = totalShots;
            HitRate01 = hitRate01;
            AverageStability01 = averageStability01;
            AverageAccuracy01 = averageAccuracy01;
        }

        public bool HasShots => TotalShots > 0;

        /// <summary>지금까지 쏜 모든 발로 결과를 만든다. 점수는 명중 1발당 1점.</summary>
        public static StageResult FromShots(int stageNumber, bool cleared, IReadOnlyList<ShotResult> allShots)
        {
            int hits = 0;
            float stabilitySum = 0f;
            float accuracySum = 0f;
            foreach (ShotResult shot in allShots)
            {
                if (shot.Hit) hits++;
                stabilitySum += shot.Stability01;
                accuracySum += shot.Accuracy01;
            }

            int count = allShots.Count;
            float hitRate = count > 0 ? (float)hits / count : 0f;
            float stability = count > 0 ? stabilitySum / count : 0f;
            float accuracy = count > 0 ? accuracySum / count : 0f;
            return new StageResult(stageNumber, cleared, hits, hits, count, hitRate, stability, accuracy);
        }
    }
}
