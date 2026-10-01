using UnityEngine.XR.ARFoundation;

namespace BeOdysseus
{
    /// <summary>지금 조준 방향(= AR 카메라가 보는 방향)을 믿을 수 있는지 알려 준다.</summary>
    public static class AimTracking
    {
        /// <summary>폰에서는 AR이 공간을 추적하고 있을 때, 에디터에서는 마우스 조준 시뮬레이터가 켜져 있을 때 true.</summary>
        public static bool IsReliable => ARSession.state == ARSessionState.SessionTracking || EditorAimSimulator.IsActive;
    }
}
