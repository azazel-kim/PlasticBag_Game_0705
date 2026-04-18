package com.dxplab.xrcontrol

import android.app.Application
import dagger.hilt.android.HiltAndroidApp

/**
 * XR Control Android 앱의 루트 Application.
 *
 * Hilt DI 초기화를 위해 @HiltAndroidApp 어노테이션 필수.
 */
@HiltAndroidApp
class XRApplication : Application()
