using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeOdysseus.EditorTools
{
    /// <summary>
    /// Build Settings에 등록된 씬으로 Android 개발 빌드를 만들고, USB로 연결된 폰에 설치·실행한다.
    /// "ver2" 메뉴는 앱 이름과 패키지 이름을 빌드하는 동안만 바꿔서, 기존 앱을 덮어쓰지 않고 따로 설치한다.
    /// "APK 파일만 만들기"는 폰 없이 파일만 만든다.
    /// </summary>
    public static class AndroidBuild
    {
        private const string OutputDirectory = "Builds";
        private const string OutputPath = OutputDirectory + "/BeODYSSEUS.apk";
        private const string Ver2OutputPath = OutputDirectory + "/BeODYSSEUS_ver2.apk";
        private const string Ver2NameSuffix = " ver2";
        private const string Ver2IdentifierSuffix = ".ver2";

        [MenuItem("Be ODYSSEUS/Android 빌드 후 폰에서 실행")]
        public static void BuildAndRun() => Build(OutputPath, run: true);

        /// <summary>기존 앱은 그대로 두고 "앱 이름 ver2"로 따로 설치한다. 빌드가 끝나면 이름과 패키지를 원래대로 돌린다.</summary>
        [MenuItem("Be ODYSSEUS/Android 빌드 후 폰에서 실행 (ver2로 따로 설치)")]
        public static void BuildAndRunVer2() => BuildVer2(run: true);

        /// <summary>폰 없이 ver2 APK 파일만 만든다(Builds/BeODYSSEUS_ver2.apk). 파일을 폰에 옮겨 설치하면 된다.</summary>
        [MenuItem("Be ODYSSEUS/APK 파일만 만들기 (ver2)")]
        public static void BuildApkVer2() => BuildVer2(run: false);

        private static void BuildVer2(bool run)
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            string productName = PlayerSettings.productName;
            string identifier = PlayerSettings.GetApplicationIdentifier(android);
            try
            {
                PlayerSettings.productName = productName + Ver2NameSuffix;
                PlayerSettings.SetApplicationIdentifier(android, identifier + Ver2IdentifierSuffix);
                Build(Ver2OutputPath, run);
            }
            finally
            {
                PlayerSettings.productName = productName;
                PlayerSettings.SetApplicationIdentifier(android, identifier);
            }
        }

        /// <param name="run">true면 USB로 연결된 폰에 설치하고 실행한다. false면 APK 파일만 만든다.</param>
        private static void Build(string outputPath, bool run)
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
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | (run ? BuildOptions.AutoRunPlayer : BuildOptions.None),
            };

            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            string message = $"[AndroidBuild] {summary.result} | {summary.totalTime.TotalSeconds:F0}s | errors={summary.totalErrors} | {summary.outputPath}";
            if (summary.result == BuildResult.Succeeded) Debug.Log(message);
            else Debug.LogError(message);
        }
    }
}
