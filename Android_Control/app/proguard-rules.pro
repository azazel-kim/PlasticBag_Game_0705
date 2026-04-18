# Compose + Hilt 기본 유지
-keepclassmembers class * { @dagger.hilt.android.HiltAndroidApp <init>(...); }
-keep class kotlinx.coroutines.** { *; }
