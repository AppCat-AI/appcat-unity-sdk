// Host-side JVM unit tests for the pure half of the Android Unity bridge
// (AppCatUnityJson.kt). Compiles the shipped source directly from
// Runtime/Plugins/Android; the Activity/AppCatCore-dependent bridge is
// exercised by the Unity Android smoke app.
//
//   cd Tests~/android && ./gradlew test
plugins {
  kotlin("jvm") version "1.9.22"
}

repositories {
  mavenCentral()
}

kotlin {
  jvmToolchain(17)
}

sourceSets {
  main {
    kotlin.setSrcDirs(listOf("../../Runtime/Plugins/Android"))
    kotlin.include("**/AppCatUnityJson.kt")
  }
  test {
    kotlin.setSrcDirs(listOf("src/test/kotlin"))
  }
}

dependencies {
  // Android ships org.json; on the JVM we use the reference implementation.
  implementation("org.json:json:20240303")
  testImplementation(kotlin("test"))
  testImplementation("junit:junit:4.13.2")
}

tasks.test {
  useJUnit()
  testLogging { events("passed", "failed", "skipped") }
}
