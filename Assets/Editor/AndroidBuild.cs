using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeOdysseus.EditorTools
{
    /// <summary>
    /// Build Settings에 등록된 씬으로 Android 개발 빌드를 만들고, USB로 연결된 폰에 설치·실행한다.
    /// </summary>
    public static class AndroidBuild
    {
        private const string OutputDirectory = "Builds";
        private const string OutputPath = OutputDirectory + "/BeODYSSEUS.apk";

        [MenuItem("Be ODYSSEUS/Android 빌드 후 폰에서 실행")]
        public static void BuildAndRun()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[AndroidBuild] Build Settings에 등록된 씬이 없습니다.");
                return;
            }

            Directory.CreateDirectory(OutputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.AutoRunPlayer | BuildOptions.Development,
            };

            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            string message = $"[AndroidBuild] {summary.result} | {summary.totalTime.TotalSeconds:F0}s | errors={summary.totalErrors} | {summary.outputPath}";
            if (summary.result == BuildResult.Succeeded) Debug.Log(message);
            else Debug.LogError(message);
        }
    }
}
