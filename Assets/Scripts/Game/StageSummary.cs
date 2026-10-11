namespace BeOdysseus
{
    /// <summary>스테이지 한 판만의 결과(누적이 아님). 최종 결과 화면에서 스테이지별 줄에 쓴다.</summary>
    public readonly struct StageSummary
    {
        public readonly int StageNumber;
        public readonly bool Cleared;
        public readonly int Hits;
        public readonly int Shots;

        public StageSummary(int stageNumber, bool cleared, int hits, int shots)
        {
            StageNumber = stageNumber;
            Cleared = cleared;
            Hits = hits;
            Shots = shots;
        }
    }
}
