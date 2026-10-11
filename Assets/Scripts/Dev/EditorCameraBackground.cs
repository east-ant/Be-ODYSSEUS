using UnityEngine;

namespace BeOdysseus.Dev
{
    /// <summary>
    /// 에디터 전용: 카메라 영상이 없는 에디터에서 AR 화면 뒤를 흰색으로 칠해 도끼·몬스터가 잘 보이게 한다.
    /// 폰 빌드에서는 아무것도 하지 않아서 씬에 정해 둔 배경(검정, 그 위에 카메라 영상)이 그대로 나온다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class EditorCameraBackground : MonoBehaviour
    {
        [SerializeField] private Color _editorColor = Color.white;

        private void Awake()
        {
            if (!Application.isEditor) return;
            var camera = GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = _editorColor;
        }
    }
}
