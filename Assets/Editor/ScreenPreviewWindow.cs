using UnityEditor;
using UnityEngine;

namespace BeOdysseus.EditorTools
{
    /// <summary>
    /// "Be ODYSSEUS/화면 미리보기" 창. 버튼을 누르면 플레이하지 않고 그 화면을 Game 창에 띄운다(ScreenPreview).
    /// 띄운 상태에서 글씨 위치·크기를 고치고 저장하면 된다.
    /// </summary>
    internal class ScreenPreviewWindow : EditorWindow
    {
        private int _stage = 1;
        private Vector2 _scroll;

        [MenuItem("Be ODYSSEUS/화면 미리보기")]
        private static void Open() => GetWindow<ScreenPreviewWindow>("화면 미리보기");

        private void OnEnable() => ScreenPreview.Changed += Repaint;
        private void OnDisable() => ScreenPreview.Changed -= Repaint;

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("플레이 중에는 쓸 수 없어요. 플레이 중에 옮긴 위치는 플레이를 멈추면 사라져요.", MessageType.Info);
                return;
            }

            int stageCount = ScreenPreview.StageCount();
            if (stageCount == 0)
            {
                EditorGUILayout.HelpBox("Game 씬(Assets/Scenes/Game.unity)을 열어 주세요.", MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("지금 띄운 화면", ScreenPreview.Title(ScreenPreview.CurrentKey) ?? "없음 (원래 상태)", EditorStyles.boldLabel);

            Section("메인");
            Row(("메인 화면", ScreenPreview.MainMenuKey), ("태블릿 연결 창", ScreenPreview.TabletKey));

            Section("방향 정하기");
            Row(("방향 정하기", ScreenPreview.CalibrationKey), ("방향 정한 뒤", ScreenPreview.CalibrationDoneKey));

            Section("스테이지");
            var labels = new string[stageCount];
            for (int i = 0; i < stageCount; i++) labels[i] = $"{i + 1}스테이지";
            _stage = Mathf.Clamp(GUILayout.Toolbar(_stage - 1, labels) + 1, 1, stageCount);
            Row(("게임 화면", ScreenPreview.StageKey(_stage)), ("정지 창", ScreenPreview.PauseKey(_stage)));
            Row(("결과 (클리어)", ScreenPreview.ResultKey(_stage, true)), ("결과 (실패)", ScreenPreview.ResultKey(_stage, false)));

            EditorGUILayout.Space(12);
            using (new EditorGUI.DisabledScope(ScreenPreview.CurrentKey == null))
            {
                if (GUILayout.Button("미리보기 끝내기 (원래대로)", GUILayout.Height(30))) ScreenPreview.End();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "• 화면을 띄운 채 Hierarchy에서 글씨를 골라 위치·크기를 고치고 Ctrl+S로 저장하세요.\n" +
                "• 저장할 때 화면 켜짐/꺼짐과 글자 내용은 원래대로 저장되고, 고친 위치·크기만 남아요.\n" +
                "• 결과 화면 숫자는 예시예요(스테이지마다 3발씩 쏜 누적).\n" +
                "• Game 창 위 해상도를 2316x1080으로 맞추면 폰(S23 Ultra)과 같은 비율로 보여요.",
                MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        }

        private static void Row(params (string Label, string Key)[] buttons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var (label, key) in buttons)
                {
                    bool selected = ScreenPreview.CurrentKey == key;
                    if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(28)) && !selected)
                        ScreenPreview.Show(key);
                }
            }
        }
    }
}
