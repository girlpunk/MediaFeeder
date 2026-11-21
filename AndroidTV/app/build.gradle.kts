plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.plugin.serialization")
    id("org.jetbrains.kotlin.plugin.compose")
    id("com.google.dagger.hilt.android")
    id("com.google.devtools.ksp")
    id("androidx.baselineprofile")
}

android {
    namespace = "xyz.foxocube.mediafeeder.androidtv"
    compileSdk = 37
    compileSdkMinor = 2
    buildToolsVersion = "37.0.0"

    defaultConfig {
        applicationId = "xyz.foxocube.mediafeeder.androidtv"
        minSdk = 24
        targetSdk = 37
        versionCode = 1
        versionName = "0.1.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        vectorDrawables {
            useSupportLibrary = true
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
        }
    }
    buildFeatures {
        compose = true
        buildConfig = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    testOptions {
        unitTests {
            isReturnDefaultValues = true
            // Jupiter (JUnit 5) tests; without this AGP discovers with the JUnit 4 framework.
            all { it.useJUnitPlatform() }
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17)
    }
}

dependencies {
    implementation(platform("androidx.compose:compose-bom:${rootProject.extra["composeBomVersion"]}"))

    // General compose dependencies.
    implementation("androidx.activity:activity-compose:1.13.0")
    implementation("androidx.compose.animation:animation:1.13.0")

    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.leanback:leanback:1.2.0")
    implementation("com.github.bumptech.glide:glide:5.0.10")

    // Compose Previews
    debugImplementation("androidx.compose.ui:ui-tooling")

    // Material components optimized for TV apps
    implementation("androidx.tv:tv-material:1.1.0")

    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.ui:ui")
    implementation("androidx.lifecycle:lifecycle-viewmodel")
    implementation("androidx.lifecycle:lifecycle-viewmodel-ktx")

    // ViewModel in Compose
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose")

    implementation("androidx.tv:tv-foundation:1.0.0")

    // Compose Navigation
    implementation("androidx.navigation:navigation-compose:2.10.2")

    // JSON parser
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.13.0")

    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.11.0")
    implementation("com.squareup.okhttp3:okhttp:5.5.1")

    // Media3
    implementation("androidx.media3:media3-exoplayer:1.11.1")
    implementation("androidx.media3:media3-exoplayer-hls:1.11.1")
    implementation("androidx.media3:media3-ui:1.11.1")
    implementation("androidx.media3:media3-ui-compose-material3:1.11.1")

    testImplementation("org.junit.jupiter:junit-jupiter:6.1.3")
    // Gradle 9 no longer injects the launcher automatically.
    testRuntimeOnly("org.junit.platform:junit-platform-launcher:6.1.3")



    implementation("androidx.core:core-ktx:1.19.1")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.11.1")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.11.1")

    // extra material icons
    implementation("androidx.compose.material:material-icons-extended")

    // Coil
    implementation("io.coil-kt:coil-compose:2.7.0")

    // SplashScreen
    implementation("androidx.core:core-splashscreen:1.2.0")

    // Hilt
    implementation("com.google.dagger:hilt-android:2.60.1")
    implementation("androidx.hilt:hilt-navigation-compose:1.4.0")
    ksp("com.google.dagger:hilt-compiler:2.60.1")


    // Baseline profile installer
    implementation("androidx.profileinstaller:profileinstaller:1.4.1")

    // For baseline profile generation
    // baselineProfile(project(":benchmark"))
}
