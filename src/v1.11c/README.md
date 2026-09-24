# HIKI Notifier / 히키 알리미

Eltax의 팬메이드 Windows 알림 유틸리티입니다. NAVER, CHZZK, RPLAY, YouTube 또는 Hikimori Neko의 공식 프로그램이 아닙니다.

HIKI Notifier is a fan-made Windows notification utility by Eltax. It is not an official app of NAVER, CHZZK, RPLAY, YouTube, or Hikimori Neko.

## 환경 / Requirements

- Windows 10/11, x64, .NET Framework 4.8
- 로그인 또는 API 키 불필요 / No login or API key required
- CHZZK·RPLAY LIVE 상태: 30초 간격 / CHZZK and RPLAY live status: 30 seconds
- YouTube 공개 Atom 피드의 새 콘텐츠: 180초 간격 / New content from the public YouTube Atom feed: 180 seconds

YouTube RSS가 일시적으로 실패하면 공개 채널의 동영상·Shorts 페이지를 보조 정보원으로 확인합니다. HTML 구조 변경 시 감지가 지연될 수 있습니다.

If YouTube RSS is temporarily unavailable, the app checks the public channel's Videos and Shorts pages as a fallback. Changes to YouTube's page structure may delay detection.

## 사용 / Use

Hikimori Neko 스트리머에는 CHZZK와 YouTube 채널이 내장되어 있습니다. 이름·URL은 고정이며 메모와 채널별 알림은 수정할 수 있습니다. 사용자 스트리머를 추가할 때 이름, 메모, 첫 CHZZK/RPLAY/YouTube 채널 URL을 입력하고, 편집창에서 다른 플랫폼 채널을 연결할 수 있습니다. YouTube LIVE 감지는 아직 지원하지 않습니다.

Hikimori Neko is built in with CHZZK and YouTube channels. Its name and URLs are fixed, while its memo and per-channel alerts can be changed. Add a streamer with a name, memo, and first CHZZK/RPLAY/YouTube channel URL; connect other platform channels in the edit window. YouTube live detection is not supported yet.

YouTube 채널 ID를 확인했지만 RSS 피드가 일시적으로 실패하면 채널은 확인 대기 상태로 등록됩니다. 첫 정상 피드는 기존 콘텐츠를 기준값으로 저장하며 알림을 보내지 않습니다.

If a YouTube channel ID resolves but its RSS feed is temporarily unavailable, the channel is registered as pending. The first successful feed establishes a baseline without an alert.

알림창에서는 보기 버튼을 눌렀을 때만 브라우저가 열립니다. 창을 닫거나 최소화해도 트레이에서 계속 실행되며, 트레이 메뉴의 **프로그램 종료**로 종료합니다. 기본 WAV, 사용자 WAV, 무음 중 알림음을 선택할 수 있습니다.

Only the alert's watch button opens a browser. Closing or minimizing the window leaves the app in the tray; choose **Exit** in the tray menu to quit. Notification sound can use the embedded WAV, a custom WAV, or silence.

전역 설정과 내장 HIKI 상태는 `%LocalAppData%\HIKI Notifier\settings.json`, 사용자 스트리머는 같은 폴더의 `profiles\{ID}.json`에 각각 저장됩니다. 이전 EXE 옆 또는 `%AppData%`의 단일 설정 JSON은 첫 실행 시 백업 후 자동 변환합니다. UI 언어는 `lang\*.ini` 언어팩을 자동 검색하며, 누락된 번역은 영어로 표시합니다.

Global settings and built-in HIKI state are saved to `%LocalAppData%\HIKI Notifier\settings.json`. Each user streamer has one `profiles\{ID}.json`. An older single JSON beside the EXE or under `%AppData%` is backed up and migrated on first run. UI language packs are discovered from `lang\*.ini`; missing translations fall back to English.

## 빌드 / Build

Visual Studio의 .NET Framework 4.8 개발 도구가 필요합니다. `./build.ps1 -Test`는 x64 Release 빌드와 로직 검사를 수행합니다. 실행 파일은 `bin\Release\HIKI Notifier.exe`입니다.

Visual Studio with .NET Framework 4.8 targeting tools is required. `./build.ps1 -Test` builds x64 Release and runs logic checks. The app is `bin\Release\HIKI Notifier.exe`.
