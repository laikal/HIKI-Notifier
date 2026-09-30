# Twitch LIVE 알림 Provider

검증일: 2026-09-25 KST. v1.12c working source, 버전 변경 없음.

## 현재 상태 모델

- Live: 요청 성공, 채널 identity 검증, 채널에 바인딩된 유효한 BroadcastEvent/isLiveBroadcast=true.
- NotLive: 정상 조회·identity·알려진 JSON-LD 구조 검증 후 현재 방송 객체가 없음. 기존 LiveState.Offline으로 표현한다. 프로필/VOD ItemList만 있는 페이지도 해당한다.
- Unknown: HTTP/네트워크/구조 파싱/identity 검증 실패, 만료·미래 시각·충돌·불완전한 방송 객체 등 판별 불가.
- NotFound: 요청한 채널이 존재하지 않음을 신뢰할 수 있게 확인한 결과. 현재 HTTP 404를 이 결과로 분리한다.

NotLive는 Twitch가 OFFLINE 문구를 명시했다는 주장이 아니라, 검증된 채널 페이지에서 현재 LIVE가 관측되지 않았다는 앱의 상태다.
알 수 없는 최상위 타입, 잘못된 graph/list 구조, 중복 프로필/방송 객체를 NotLive로 처리하지 않는다.
추천 영상/VOD ItemList는 방송 감지 대상으로 순회하지 않는다. LIVE 문자열 검색은 사용하지 않는다.

## 전이 및 알림

- NotLive → Live: 새 방송이면 알림과 AutoOpen 대상.
- Live → NotLive: 다음 방송을 위해 재무장. 이미 처리한 session 기록은 유지.
- Live → Unknown: 방송 종료로 간주하지 않으며 재무장하지 않음.
- Unknown → Live: 이전 확정 LIVE session과 다른 새 session임이 확인될 때 알림/AutoOpen 대상. 같은 session 또는 식별 불가는 억제.
- NotLive → Unknown: 모델의 기존 Offline을 유지하지만 해당 poll은 실패로 기록해 backoff 적용.
- 최초 실행 Unknown → 첫 Live: 기존대로 자동 알림/자동 열기 없음.
- 알림 OFF → ON: 현재 Live이면 기존 NotificationService를 통해 1회 replay. AutoOpen replay 없음.

마지막 확정 상태와 최근 session 20개를 TwitchProvider 내부에서 기억한다. 공통 알림 정책에 맞춰 전이 인자를 전달하므로 다른 Provider 규칙은 바뀌지 않는다.

## 실제 HTTP 검증

수정 후 구현체를 직접 실행한 결과:

| 채널 | 존재 확인 | 결과 |
| --- | --- | --- |
| eslcs | True | Live, 제목/시작 시각 확인 |
| gaules | True | Live, 제목/시작 시각 확인 |
| twitch | True | Offline (NotLive), 정상 프로필 구조에 현재 방송 없음 |
| 임의 비존재 후보 | False | Unknown |

실제 방송 시작 시각을 기다려 알림창을 띄우는 전 과정은 이번 검증에 포함하지 않았다. HTML 응답과 상태 전이 fixture로 연결을 검사했다.

## 데이터 source 및 제한

이 Provider는 공개 채널 HTML의 구조화된 LIVE 정보를 이용해 현재 알림 대상 방송이 있는지 확인한다. OAuth/Client Secret/로그인 쿠키/타 앱 Client-ID/playback token/M3U8/HLS/영상 다운로드는 사용하지 않는다.
기존 조사에서 Client-ID 없는 GQL은 HTTP 400(Client-ID 누락), player HTML은 일반 셸만 반환했다. 검증되지 않은 fallback은 추가하지 않았다.

- 취득: 채널 표시명, 현재 LIVE/NotLive, 제목, 시작 시각.
- session key: login + UTC startDate. 실제 Twitch stream ID는 미취득.
- 미취득: 구조화된 동시 시청자 수, 게임/카테고리. UI 시청자는 `-`.
- 기본 60초 polling, 연속 실패 60 → 120 → 300초, 정상 판정 시 60초 복귀.
- HTML 배포 구조가 바뀌면 Unknown이 될 수 있다. JSON-LD의 갱신 지연까지 완전히 배제할 수는 없다.
- 짧은 실제 검사에서 rate limit/integrity challenge는 관찰되지 않았다. 장시간 안정성 검증은 아니다.
- 앱 간 Client-ID 공유에 대한 공식 안내: https://dev.twitch.tv/docs/authentication/register-app/
- embed 문서: https://dev.twitch.tv/docs/embed/video-and-clips/

## 이번 변경 파일

- Providers/Twitch/TwitchClient.cs: 정상 채널의 방송 객체 부재를 NotLive로 판정; 구조 변경·충돌 guard 강화.
- Providers/Twitch/TwitchProvider.cs: 마지막 확정 상태 보존; 오류 복구 시 새 session 처리.
- Tests/TwitchChecks.cs: NotLive/Unknown/parser/전이/HTTP 연결 검사 갱신.
- Tests/Fixtures/twitch-eslcs-notlive.html: 실제 eslcs identity에서 방송 객체만 제거한 합성 fixture.
- Tests/Fixtures/TWITCH_NOTES.md, 이 보고서.

CHZZK/YouTube/RPLAY 구현, 버전, src/v1.12c, release/v1.12c는 이번 변경에서 수정하지 않았다.
Debug/Release 빌드 성공. 전체 fixture/로직 검사 결과는 아래 검증 기록에 기재한다.

검증 기록: 전체 256개 PASS. 기존 플랫폼 회귀 검사와 멈춘 Twitch 요청 중 CHZZK polling 유지 검사 포함.
