# Camera & Motion Analyst - Agent Memory

## Xsens MVN Awinda 설정
- 제품: MVN Awinda (무선, 재활 권장)
- 센서: 17개 무선 IMU, 23 세그먼트, 22 관절
- 업데이트: 60Hz, 지연 ~30ms
- UDP 포트: 9763, Unity3D 모드(Y-Up, Left-Handed)
- MVN Analyze 소프트웨어 경유 스트리밍 (내부 1000Hz 처리)

## UDP 데이터그램 구조
- Header: 24B ("MXTP##" + counter + timecode + charID)
- Body: 23 × 28B (Position xyz + Quaternion wxyz)
- Unity3D 모드 선택 시 좌표계 자동 변환

## Samsung XR 네트워크 요구사항
- 헤드셋과 MVN PC 동일 WiFi 필요
- 포트 9763 UDP 인바운드 허용
- WiFi 추가 지연: 2-10ms

## 참조 문서
- `Xsens_Rehabilitation_Unity_Integration_Report.md` — 상세 통합 보고서
- 섹션 5: UDP 프로토콜 + XSensUDPReceiver.cs 코드
- 섹션 6: SensorFusionManager 연동 구조
