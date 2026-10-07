#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;

namespace AppCat.Editor
{
    /// <summary>
    /// Ensures the exported Gradle project can compile the Kotlin bridge and
    /// run the vendored appcat-core.aar:
    ///   - kotlin-stdlib on the runtime classpath (the AAR is written in Kotlin
    ///     and does not bundle the stdlib; Unity projects without other Kotlin
    ///     plugins would otherwise crash with NoClassDefFoundError).
    ///   - INTERNET permission, required for the attribution API.
    ///
    /// Idempotent: skips lines that are already present.
    /// </summary>
    internal class AppCatAndroidBuildProcessor : IPostGenerateGradleAndroidProject
    {
        private const string KotlinStdlib = "org.jetbrains.kotlin:kotlin-stdlib:1.9.22";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            EnsureDependency(Path.Combine(path, "build.gradle"));
            EnsureInternetPermission(Path.Combine(path, "src", "main", "AndroidManifest.xml"));
        }

        private static void EnsureDependency(string gradlePath)
        {
            if (!File.Exists(gradlePath)) return;
            var text = File.ReadAllText(gradlePath);
            if (text.Contains("kotlin-stdlib")) return;

            const string marker = "dependencies {";
            var idx = text.IndexOf(marker, System.StringComparison.Ordinal);
            if (idx < 0)
            {
                text += "\ndependencies {\n    implementation '" + KotlinStdlib + "'\n}\n";
            }
            else
            {
                var insertAt = idx + marker.Length;
                text = text.Insert(insertAt, "\n    implementation '" + KotlinStdlib + "' // AppCat");
            }
            File.WriteAllText(gradlePath, text);
        }

        private static void EnsureInternetPermission(string manifestPath)
        {
            if (!File.Exists(manifestPath)) return;
            var text = File.ReadAllText(manifestPath);
            if (text.Contains("android.permission.INTERNET")) return;
            text = text.Replace("</manifest>",
                "    <uses-permission android:name=\"android.permission.INTERNET\" />\n</manifest>");
            File.WriteAllText(manifestPath, text);
        }
    }
}
#endif
