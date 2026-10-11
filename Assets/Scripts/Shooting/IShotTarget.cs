using UnityEngine;

namespace BeOdysseus
{
    /// <summary>화살 판정 대상(몬스터, 도끼 튜토리얼). ShotJudge가 조준선으로 맞았는지 묻는다.</summary>
    public interface IShotTarget
    {
        /// <summary>지금 화면에 있어서 맞힐 수 있는지.</summary>
        bool IsActive { get; }

        /// <summary>정확도(조준이 얼마나 가운데로 갔는지)를 재는 기준점.</summary>
        Vector3 TargetPoint { get; }

        /// <summary>
        /// 조준선(시작점, 방향)으로 쐈을 때 맞았는지(hit)와 화살이 닿는 자리(impact).
        /// 닿는 자리를 정할 수 없으면(반대쪽을 겨눔) false.
        /// </summary>
        bool TryHit(Vector3 origin, Vector3 direction, out bool hit, out Vector3 impact);

        /// <summary>
        /// 화살이 닿은 자리에 꽂히는지(아니면 지나쳐 날아가는지). 몬스터는 맞으면 꽂히고,
        /// 도끼 줄은 통과(hit)하면 지나가고 막히면 도끼에 꽂힌다.
        /// </summary>
        bool ArrowSticks(bool hit);
    }
}
