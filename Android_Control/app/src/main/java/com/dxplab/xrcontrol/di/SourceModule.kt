package com.dxplab.xrcontrol.di

import com.dxplab.xrcontrol.data.mock.MockLinkBandClient
import com.dxplab.xrcontrol.domain.source.SensorSource
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

/**
 * SensorSource 주입 — 현재 Mock을 기본 사용.
 *
 * 실SDK 교체 시:
 *   1. LinkBandClient를 실구현으로 채움
 *   2. @Provides에서 MockLinkBandClient() → LinkBandClient(...)로 교체
 *   3. 또는 SettingsRepository.useMock 값을 기반으로 런타임 선택
 *      (하지만 @Singleton이라 앱 재시작 필요 — 추후 Factory 패턴으로 개선)
 */
@Module
@InstallIn(SingletonComponent::class)
object SourceModule {

    @Provides
    @Singleton
    fun provideSensorSource(): SensorSource {
        // TODO: SettingsRepository.useMock 읽어 실SDK와 런타임 스왑 지원
        return MockLinkBandClient()
    }
}
