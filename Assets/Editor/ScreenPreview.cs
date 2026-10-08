using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BeOdysseus.EditorTools
{
    /// <summary>
    /// 플레이하지 않고 게임 화면(메인, 방향 정하기, 스테이지별 게임·결과 화면, 정지 창 등)을 Game 창에 띄운다.
    /// 화면은 실제 게임 코드(StageHud.Show, ResultView.Show 등)로 채우므로 게임에서 보이는 모습과 같다.
    /// 미리보기가 바꾼 값(켜짐/꺼짐, 글자 내용, 그림 등)만 기억했다가 끝낼 때 되돌린다.
    /// 미리보는 동안 사람이 바꾼 위치·크기·글자 크기 등은 그대로 남는다.
    /// 씬을 저장할 때는 잠깐 되돌려서 미리보기 상태가 저장되지 않게 하고, 저장 뒤 다시 띄운다.
    /// 플레이를 시작하거나 씬을 닫을 때는 끝내고, 스크립트를 다시 컴파일할 때는 되돌렸다가 다시 띄운다.
    /// </summary>
    public static class ScreenPreview
    {
        private const string KeyState = "BeOdysseus.ScreenPreview.Key";
        // 화면 UI가 들어 있는 캔버스. 이 아래만 기억하고 되돌린다.
        private static readonly string[] CanvasRoots = { "MainMenu", "UI", "Result" };
        // 부모·자식 연결은 되돌리지 않는다(복제한 화살은 따로 지운다).
        private static readonly string[] SkippedPaths = { "m_Children", "m_Father" };

        private readonly struct Change
        {
            public readonly Object Target;
            public readonly string Path;
            public readonly object Original;
            public readonly object Applied;

            public Change(Object target, string path, object original, object applied)
            {
                Target = target;
                Path = path;
                Original = original;
                Applied = applied;
            }
        }

        private sealed class Views
        {
            public GameConfig Config;
            public StageList Stages;
            public MainMenuView MainMenu;
            public GameObject TabletPopup;
            public GameObject GameplayHud;
            public StageHud StageHud;
            public CalibrationView Calibration;
            public ResultView Result;
            public GameObject PauseButton;
            public GameObject PausePopup;
        }

        private static readonly List<Change> Changes = new();
        private static readonly List<GameObject> Created = new();
        private static string _keyBeforeSave;

        /// <summary>지금 띄운 화면. 없으면 null.</summary>
        public static string CurrentKey { get; private set; }
        public static event Action Changed;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorSceneManager.sceneClosing += (_, _) => End();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) End();
            };
            AssemblyReloadEvents.beforeAssemblyReload += Revert;

            // 스크립트를 다시 컴파일하기 전에 띄워 두었던 화면을 다시 띄운다.
            string key = SessionState.GetString(KeyState, "");
            if (key.Length > 0 && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += () => Show(key);
        }

        // ── 화면 이름 ─────────────────────────────

        public static string MainMenuKey => "main";
        public static string TabletKey => "tablet";
        public static string CalibrationKey => "calib";
        public static string CalibrationDoneKey => "calibDone";
        public static string StageKey(int stage) => $"stage/{stage}";
        public static string PauseKey(int stage) => $"pause/{stage}";
        public static string ResultKey(int stage, bool cleared) => $"{(cleared ? "clear" : "fail")}/{stage}";

        public static string Title(string key)
        {
            if (key == null) return null;
            string[] parts = key.Split('/');
            string stage = parts.Length > 1 ? $"{parts[1]}스테이지 " : "";
            return parts[0] switch
            {
                "main" => "메인 화면",
                "tablet" => "태블릿 연결 창",
                "calib" => "방향 정하기",
                "calibDone" => "방향 정한 뒤(카운트다운)",
                "stage" => stage + "게임 화면",
                "pause" => stage + "정지 창",
                "clear" => stage + "결과(클리어)",
                "fail" => stage + "결과(실패)",
                _ => key,
            };
        }

        /// <summary>지금 열린 씬의 스테이지 수. 게임 씬이 아니면 0.</summary>
        public static int StageCount()
        {
            Views views = FindViews();
            return views?.Stages != null ? views.Stages.Count : 0;
        }

        // ── 띄우기 / 되돌리기 ─────────────────────────────

        /// <summary>화면을 띄운다. 앞에 띄운 화면은 먼저 되돌린다. 게임 씬이 아니면 false.</summary>
        public static bool Show(string key)
        {
            Revert();
            Views views = FindViews();
            if (views == null) return false;

            Scene scene = SceneManager.GetActiveScene();
            List<Object> targets = Targets(scene);
            var before = Capture(targets);
            var objectsBefore = new HashSet<GameObject>(targets.OfType<GameObject>());

            Apply(views, key);

            foreach (var pair in Capture(targets))
            {
                if (before.TryGetValue(pair.Key, out object original) && !Equals(original, pair.Value))
                    Changes.Add(new Change(pair.Key.Target, pair.Key.Path, original, pair.Value));
            }
            Created.AddRange(Targets(scene).OfType<GameObject>().Where(go => !objectsBefore.Contains(go)));

            CurrentKey = key;
            SessionState.SetString(KeyState, key);
            Refresh();
            return true;
        }

        /// <summary>미리보기를 끝내고 원래대로 되돌린다.</summary>
        public static void End()
        {
            Revert();
            SessionState.EraseString(KeyState);
            Refresh();
        }

        /// <summary>미리보기가 바꾼 값을 되돌린다. 미리보는 동안 사람이 다시 바꾼 값은 그대로 둔다.</summary>
        private static void Revert()
        {
            foreach (GameObject go in Created)
                if (go != null) Object.DestroyImmediate(go);
            Created.Clear();

            foreach (IGrouping<Object, Change> group in Changes.GroupBy(c => c.Target))
            {
                if (group.Key == null) continue;
                var so = new SerializedObject(group.Key);
                foreach (Change change in group)
                {
                    SerializedProperty property = so.FindProperty(change.Path);
                    if (property == null || !TryGetValue(property, out object now)) continue;
                    if (Equals(now, change.Applied)) property.boxedValue = change.Original;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Changes.Clear();
            CurrentKey = null;
        }

        private static void Refresh()
        {
            Canvas.ForceUpdateCanvases();
            InternalEditorUtility.RepaintAllViews();
            Changed?.Invoke();
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (CurrentKey == null) return;
            _keyBeforeSave = CurrentKey;
            Revert();
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (_keyBeforeSave == null) return;
            string key = _keyBeforeSave;
            _keyBeforeSave = null;
            EditorApplication.delayCall += () => Show(key);
        }

        // ── 화면 채우기(게임 코드 그대로) ─────────────────────────────

        private static void Apply(Views v, string key)
        {
            string[] parts = key.Split('/');
            int stage = parts.Length > 1 ? Mathf.Clamp(int.Parse(parts[1]), 1, v.Stages.Count) : 1;

            HideAll(v);
            switch (parts[0])
            {
                case "main":
                    v.MainMenu.Show();
                    break;
                case "tablet":
                    v.MainMenu.Show();
                    v.TabletPopup.SetActive(true);
                    break;
                case "calib":
                    ShowGameplay(v);
                    v.Calibration.ShowReady();
                    break;
                case "calibDone":
                    ShowGameplay(v);
                    v.Calibration.ShowConfirmed(1, Mathf.Max(1, Mathf.CeilToInt(v.Config.TutorialConfirmSeconds)));
                    break;
                case "stage":
                    ShowStage(v, stage);
                    break;
                case "pause":
                    ShowStage(v, stage);
                    v.PauseButton.SetActive(false);
                    v.PausePopup.SetActive(true);
                    break;
                case "clear":
                case "fail":
                    bool hasNextStage = stage < v.Stages.Count;
                    v.Result.Show(SampleResult(stage, parts[0] == "clear"), v.Stages.Get(stage - 1),
                        hasNextStage ? GameFlow.NextStageLabel : GameFlow.BackToMenuLabel, v.Config.ResultCountdownSeconds);
                    break;
            }
        }

        private static void HideAll(Views v)
        {
            v.MainMenu.Hide();
            v.TabletPopup.SetActive(false);
            v.GameplayHud.SetActive(false);
            v.Calibration.Hide();
            v.StageHud.Hide();
            v.Result.Hide();
            v.PauseButton.SetActive(false);
            v.PausePopup.SetActive(false);
        }

        private static void ShowGameplay(Views v)
        {
            v.GameplayHud.SetActive(true);
            v.PauseButton.SetActive(true);
        }

        private static void ShowStage(Views v, int stage)
        {
            ShowGameplay(v);
            v.StageHud.Show(new StageRun(stage, v.Config.ArrowsPerStage, v.Config.StageTimeLimitSeconds));
        }

        /// <summary>결과 화면에 띄울 예시 숫자(게임 시작부터의 누적). 스테이지마다 3발씩 쏜 것으로 한다.</summary>
        private static StageResult SampleResult(int stage, bool cleared)
        {
            int shots = stage * 3;
            int hits = cleared ? stage * 2 : stage * 2 - 1;
            return new StageResult(stage, cleared, hits, hits, shots, (float)hits / shots, 0.72f, 0.65f);
        }

        private static Views FindViews()
        {
            var game = Object.FindAnyObjectByType<GameFlow>(FindObjectsInactive.Include);
            var pairing = Object.FindAnyObjectByType<TabletPairingView>(FindObjectsInactive.Include);
            var pause = Object.FindAnyObjectByType<PauseView>(FindObjectsInactive.Include);
            if (game == null || pairing == null || pause == null) return null;

            var gameSo = new SerializedObject(game);
            var mainMenu = Ref<MainMenuView>(gameSo, "_mainMenu");
            var pauseSo = new SerializedObject(pause);
            return new Views
            {
                Config = Ref<GameConfig>(gameSo, "_config"),
                Stages = Ref<StageList>(gameSo, "_stages"),
                MainMenu = mainMenu,
                TabletPopup = Ref<GameObject>(new SerializedObject(pairing), "_popup"),
                GameplayHud = Ref<GameObject>(gameSo, "_gameplayHud"),
                StageHud = Ref<StageHud>(gameSo, "_stageHud"),
                Calibration = Ref<CalibrationView>(gameSo, "_calibrationView"),
                Result = Ref<ResultView>(gameSo, "_resultView"),
                PauseButton = Ref<UnityEngine.UI.Button>(pauseSo, "_pauseButton").gameObject,
                PausePopup = Ref<GameObject>(pauseSo, "_popup"),
            };
        }

        private static T Ref<T>(SerializedObject so, string field) where T : Object =>
            (T)so.FindProperty(field).objectReferenceValue;

        // ── 값 기억하기 ─────────────────────────────

        private static List<Object> Targets(Scene scene)
        {
            var targets = new List<Object>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (Array.IndexOf(CanvasRoots, root.name) < 0) continue;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    targets.Add(t.gameObject);
                    // 스크립트가 빠진 컴포넌트는 null로 나온다.
                    targets.AddRange(t.GetComponents<Component>().Where(c => c != null));
                }
            }
            return targets;
        }

        private static Dictionary<(Object Target, string Path), object> Capture(List<Object> targets)
        {
            var values = new Dictionary<(Object, string), object>();
            foreach (Object target in targets)
            {
                if (target == null) continue;
                var so = new SerializedObject(target);
                SerializedProperty property = so.GetIterator();
                while (property.Next(true))
                {
                    string path = property.propertyPath;
                    if (property.hasChildren || SkippedPaths.Any(skipped => path.StartsWith(skipped, StringComparison.Ordinal))) continue;
                    if (TryGetValue(property, out object value)) values[(target, property.propertyPath)] = value;
                }
            }
            return values;
        }

        private static bool TryGetValue(SerializedProperty property, out object value)
        {
            try
            {
                value = property.boxedValue;
                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }
    }
}
