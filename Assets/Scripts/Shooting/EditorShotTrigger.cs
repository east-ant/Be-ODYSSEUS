using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BeOdysseus
{
    /// <summary>에디터 전용 발사 흉내. 스페이스바를 누르면 쏜 것으로 본다. 폰 빌드에서는 스스로 꺼진다.</summary>
    public class EditorShotTrigger : MonoBehaviour
    {
        public event Action<ShotEvent> Fired;

        private void Awake()
        {
            if (!Application.isEditor) enabled = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame) return;
            Fired?.Invoke(new ShotEvent(InputState.currentTime, ShotSource.Keyboard, 0f));
        }
    }
}
