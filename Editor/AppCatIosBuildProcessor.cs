#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace AppCat.Editor
{
    /// <summary>
    /// Post-processes the exported Xcode project for the Swift bridge.
    ///
    /// Embedding of AppCatCoreKit.xcframework is handled by Unity itself via
    /// the plugin's .meta (<c>AddToEmbeddedBinaries: true</c>). This step only
    /// guarantees the build settings Unity does not set on its own:
    ///   - Swift 5 on UnityFramework (where Plugins/iOS sources compile)
    ///   - Swift stdlib embedding and @executable_path/Frameworks rpath so the
    ///     dynamic xcframework resolves at launch
    /// Also warns early if the framework is missing from the export.
    /// </summary>
    internal static class AppCatIosBuildProcessor
    {
        private const string FrameworkName = "AppCatCoreKit.xcframework";

        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            if (Directory.GetDirectories(pathToBuiltProject, FrameworkName, SearchOption.AllDirectories).Length == 0)
            {
                UnityEngine.Debug.LogWarning("[AppCat] " + FrameworkName + " not found in exported Xcode project; the native iOS backend will be unavailable at runtime.");
            }

            var projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            var mainTarget = proj.GetUnityMainTargetGuid();
            var frameworkTarget = proj.GetUnityFrameworkTargetGuid();

            foreach (var t in new[] { mainTarget, frameworkTarget })
            {
                proj.SetBuildProperty(t, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks");
                proj.SetBuildProperty(t, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            }
            proj.SetBuildProperty(frameworkTarget, "SWIFT_VERSION", "5.0");

            proj.WriteToFile(projPath);
        }
    }
}
#endif
