pluginManagement {
    repositories {
        google {
            content {
                includeGroupByRegex("com\\.android.*")
                includeGroupByRegex("com\\.google.*")
                includeGroupByRegex("androidx.*")
            }
        }
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
        // LooxidLabs SDK — 실기기 연동 시 사용 (현재 Mock 모드 우선)
        // maven { url = uri("https://jitpack.io") }
    }
}

rootProject.name = "XRControl"
include(":app")
