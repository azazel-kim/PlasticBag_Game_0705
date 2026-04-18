// XRControl — Galaxy Fold LinkBand2 Bridge 앱
// 루트 build script — 플러그인 버전 선언만 수행, 공통 설정은 각 모듈에서.

plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.compose) apply false
    alias(libs.plugins.hilt) apply false
    alias(libs.plugins.ksp) apply false
}
