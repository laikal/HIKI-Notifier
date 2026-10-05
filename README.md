# HIKI Notifier

**English | [한국어](#한국어) | [日本語](#日本語)**

**Current release: v1.280**

A lightweight Windows notification utility for monitoring multiple streaming platforms and new content.

**Supported platforms:** CHZZK · YouTube · RPLAY · Twitch · SOOP · CIME · Kick · TwitCasting · Bilibili · Niconico Live · Mirrativ · SHOWROOM · Picarto.TV

- **CHZZK** — LIVE status, stream title, viewer count, thumbnails, and live notifications
- **YouTube** — New video and Shorts notifications
- **RPLAY** — LIVE status and live notifications
- **Twitch** — LIVE status and live notifications
- **SOOP** — LIVE status and live notifications
- **CIME** — LIVE status, stream title, viewer information, and live notifications
- **Kick** — LIVE status, stream title, viewer count, and live notifications
- **TwitCasting** — LIVE status, stream title, and live notifications
- **Bilibili** — LIVE status and live notifications
- **Niconico Live** — User LIVE status and live notifications
- **Mirrativ** — Public LIVE status and live notifications
- **SHOWROOM** — LIVE status and live notifications
- **Picarto.TV** — LIVE status and live notifications

HIKI Notifier includes a built-in **Hikimori Neko** profile with one CHZZK channel and two YouTube channels.

Additional streamer profiles can be added manually, and one profile can contain multiple different channels from the same platform.

No account login is required for supported platform monitoring.

> **Unofficial fan-made utility**  
> HIKI Notifier is not affiliated with or endorsed by any supported platform or its operator.

---

## Screenshots

### Main Window

![HIKI Notifier Main Window](img/1.27_main_en.jpg)

### Notification Appearance

![Customize Notification](img/1.27_Cutomize_en.jpg)

### Notification Examples

![YouTube notification example](img/1.27_alret_1.jpg)

![YouTube notification example](img/1.27_alret_2.jpg)

![CHZZK notification example](img/1.27_alret_3.jpg)

> The screenshots above are from the 1.27 UI. The overall layout remains representative of v1.280.

---

## Features

- LIVE / OFFLINE / UNKNOWN status monitoring for supported live-stream platforms
- Desktop notifications when registered live channels go LIVE
- Stream title, category, viewer information, and thumbnails when available
- YouTube new video and Shorts notifications
- Built-in Hikimori Neko profile with **1 CHZZK + 2 YouTube channels**
- Multiple streamer profiles
- Multiple channels per profile, including multiple channels from the same platform
- Per-channel notification ON / OFF
- Optional per-channel automatic stream-page opening
- Open the relevant stream or content page from the notification
- Per-profile notification appearance settings
- Per-profile notification backgrounds and text colors
- Per-profile background-image master switch
- Per-profile stream/video thumbnail backgrounds
- Built-in HIKI fallback background when no custom media or usable thumbnail is available
- Custom notification backgrounds using **JPG / JPEG / PNG / GIF / WebP / WebM**
- Static and animated WebP support
- WebM background support using VP8 / VP9 / AV1 video
- Optional notification-media audio, independently controlled from the normal notification WAV sound
- Adjustable header and content font sizes
- Adjustable text outline color and **outline thickness (0–6 px)**
- Adjustable notification opacity and display duration
- Notification preview before saving
- Test notifications can preview branding using a randomly selected loaded Provider
- Per-profile notification sound: built-in WAV / custom WAV / silent
- Resizable main window with DPI-aware layout behavior
- Standard and Dark main-window themes
- Built-in categorized Help window
- Single-instance protection to prevent duplicate polling and duplicate notifications
- System tray operation
- Optional startup with Windows
- English, Korean, and Japanese language packs

---

## Download and Run

Download the latest ZIP from:

https://github.com/laikal/HIKI-Notifier/releases

Extract the full contents of the ZIP and run:

`HIKI Notifier.exe`

No installer is required.

When updating an existing installation, replace the distributed program files together rather than replacing only the EXE. Your existing `settings.json` and `Profiles` data can be kept.

---

## Built-in Hikimori Neko Profile

The **Hikimori Neko** profile is included automatically.

It contains:

```text
Hikimori Neko
├─ CHZZK
├─ YouTube — Main
└─ YouTube — Replay
```

The built-in profile and its predefined channel addresses are protected from deletion or modification.

Channel notification settings, notification appearance, and notification sound can still be customized.

---

## Add a Streamer Profile

Press **Add Profile** in the main window.

Enter a streamer name, optional memo, and one or more supported channel URLs.

Example:

```text
https://chzzk.naver.com/live/CHANNEL_ID
https://www.youtube.com/@HANDLE
https://www.twitch.tv/CHANNEL
https://kick.com/CHANNEL
https://twitcasting.tv/CHANNEL
```

Supported platform URLs are recognized automatically by the installed Providers.

A single streamer profile can contain multiple linked channels, including multiple different channels from the same platform.

---

## Notifications

Notifications can be enabled or disabled separately for each channel.

For supported live-stream platforms, a notification appears when a newly detected broadcast starts.

Depending on the platform, a notification can include:

- Streamer name
- Stream title
- Category
- Viewer count
- Stream/video thumbnail

For YouTube, HIKI Notifier can notify you when a new video or Shorts content is detected.

Use the notification's available open-stream/content action to open the relevant page in your default browser.

---

## Automatic Stream Page Opening

Each supported live channel can optionally open its stream page automatically when a new broadcast starts.

This option is configured per channel and is disabled by default.

It is intended for newly detected broadcast starts and does not simply open a page every time the application sees a channel in LIVE state.

---

## Notification Appearance

Each streamer profile can control its own notification appearance and sound behavior.

### Per-profile settings

- Use notification background image/media
- Use stream/video thumbnails as notification backgrounds
- Custom background image/media
- Text color
- Text outline color
- Text outline thickness: **0–6 px**
- Notification sound mode
- Custom WAV file when selected

When notification background images are enabled, the normal background priority is:

1. **Custom profile background/media**
2. **Stream/video thumbnail**, when enabled and available
3. **Built-in HIKI background**

If **Use notification background image** is turned off, HIKI Notifier uses the image-less notification style.

Live-stream thumbnails are used on supported live platforms when available, while YouTube new-video and Shorts notifications can use their video thumbnails.

### Notification text / timing controls

- Header font size: **12–20 pt**
- Content font size: **10–16 pt**
- Text outline thickness: **0–6 px**
- Notification opacity: **50–100%**
- Notification duration: **3–30 seconds**

### Supported custom background formats

- JPG
- JPEG
- PNG
- GIF
- WebP
- WebM

Animated GIF and animated WebP backgrounds repeat while the notification is visible.

WebM backgrounds support the application's validated VP8 / VP9 / AV1 playback path. Notification-media audio can be enabled or disabled independently from the normal notification WAV sound.

Unsupported, missing, or corrupt media falls back safely instead of preventing the notification from appearing.

You can test current appearance and sound changes before saving them. Test notifications may use a randomly selected loaded Provider logo so that different platform branding can be previewed without changing the channels registered to the profile.

The notification title can use up to two lines, and the layout is designed to keep text readable over image and video backgrounds.

---

## Main Window Background Media

The main window can use custom background media using the existing skin/background system.

Supported media includes image formats as well as WebP and WebM. Animated media pauses while the main window is hidden or minimized and resumes when it becomes visible again.

Main background-media audio is optional and is controlled separately from notification sound and notification-media audio.

---

## Main Window Themes

Two main-window themes are available:

- **Standard** — original HIKI Notifier appearance
- **Dark** — dark color theme

The theme can be changed from **Settings**.

---

## Help

The built-in **Help** window provides categorized guides for:

- Quick start
- Streamer profiles
- Adding channels
- Supported platforms
- Notifications
- Automatic stream opening
- Notification appearance
- Backup
- Troubleshooting
- Other common usage topics

---

## Notification Sound

Notification sound is configured **per streamer profile** in **Notification Appearance**.

Available modes:

- **Built-in notification WAV**
- **Custom WAV file**
- **Silent**

Different streamer profiles can use different custom WAV files.

The normal notification WAV sound is independent from audio embedded in notification background media such as WebM.

---

## YouTube Monitoring

HIKI Notifier can notify you about newly published YouTube videos and Shorts without requiring a Google or YouTube login.

Previously detected content is not repeatedly notified during normal use.

Because YouTube monitoring depends on publicly available YouTube data, future changes to YouTube may require an update to HIKI Notifier.

---

## System Tray

Closing or minimizing the main window does **not** exit HIKI Notifier.

The application continues running in the Windows system tray.

From the tray menu you can:

- Open HIKI Notifier
- Access available channel actions
- Enable or disable channel notifications
- Open available stream/content pages
- Open program information
- Exit the application

To completely close HIKI Notifier, right-click the tray icon and select **Exit**.

---

## Start with Windows

HIKI Notifier can optionally start automatically with Windows.

This option can be changed from **Settings**.

---

## Profile Data and Backup

Streamer profiles are stored under:

`Profiles\`

Global application settings are stored in:

`settings.json`

For a simple manual backup, close HIKI Notifier and back up both:

```text
Profiles\
settings.json
```

Profile appearance files, custom media references, and per-profile notification settings are stored with the profile data according to the application's profile storage structure.

---

## Languages

Included language packs:

- English
- 한국어
- 日本語

Additional INI language packs can be added under:

```text
lang\*.ini
```

If a translation key is missing, English is used as the fallback.

---

## System Requirements

- 64-bit Windows 10 or Windows 11
- x64 processor
- .NET Framework 4.8

---

## Developer

**Eltax**

---

## Special Thanks

### Hikimori Neko

The person who inspired this project.

The built-in HIKI notification background is included with permission from Hikimori Neko.

---

## Disclaimer

HIKI Notifier is an unofficial fan-made utility.

It is not affiliated with or endorsed by any supported platform or its operator.

All service names and trademarks belong to their respective owners.

Changes to service APIs, public feeds, or web structures may cause some features to stop working correctly.

---

## License

No open-source license is currently provided for the HIKI Notifier project source code.

**Copyright © Eltax. All rights reserved.**

Third-party trademarks, service names, and externally sourced assets remain subject to the rights and license terms of their respective owners.

Bundled third-party runtime components retain their own licenses. See the distributed `Providers/Module/ThirdPartyNotices.md` and `Providers/Module/Licenses/` files for applicable notices and license texts.

---

# 한국어

**[English](#hiki-notifier) | 한국어 | [日本語](#日本語)**

**현재 버전: v1.280**

**HIKI Notifier**는 여러 스트리밍 플랫폼의 방송 상태와 새 콘텐츠를 확인하고 Windows 알림으로 알려주는 가벼운 상주형 알림 유틸리티입니다.

**지원 플랫폼:** CHZZK · YouTube · RPLAY · Twitch · SOOP · CIME · Kick · TwitCasting · Bilibili · Niconico Live · Mirrativ · SHOWROOM · Picarto.TV

- **CHZZK** — 방송 상태, 방송 제목, 시청자 수, 썸네일 및 방송 시작 알림
- **YouTube** — 새 영상 및 Shorts 알림
- **RPLAY** — 방송 상태 및 방송 시작 알림
- **Twitch** — 방송 상태 및 방송 시작 알림
- **SOOP** — 방송 상태 및 방송 시작 알림
- **CIME(씨미)** — 방송 상태, 방송 제목, 시청자 정보 및 방송 시작 알림
- **Kick** — 방송 상태, 방송 제목, 시청자 수 및 방송 시작 알림
- **TwitCasting** — 방송 상태, 방송 제목 및 방송 시작 알림
- **Bilibili** — 방송 상태 및 방송 시작 알림
- **Niconico Live** — 일반 사용자 방송 상태 및 방송 시작 알림
- **Mirrativ** — 공개 방송 상태 및 방송 시작 알림
- **SHOWROOM** — 방송 상태 및 방송 시작 알림
- **Picarto.TV** — 방송 상태 및 방송 시작 알림

기본 **Hikimori Neko** 프로파일에는 CHZZK 1개와 YouTube 2개 채널이 포함되어 있습니다.

다른 스트리머 프로파일을 직접 추가할 수 있으며, 하나의 프로파일에 같은 플랫폼의 서로 다른 채널을 여러 개 등록할 수도 있습니다.

지원 플랫폼의 방송 상태 확인에는 계정 로그인이 필요하지 않습니다.

> **비공식 팬메이드 유틸리티**  
> HIKI Notifier는 지원 플랫폼 또는 관련 운영사의 공식 프로그램이 아닙니다.

---

## 스크린샷

### 메인 화면

![HIKI Notifier 메인 화면](img/1.27_main_kr.jpg)

### 알림창 꾸미기

![알림창 꾸미기](img/1.27_Cutomize_kr.jpg)

### 알림창 예시

![YouTube 알림창 예시](img/1.27_alret_1.jpg)

![YouTube 알림창 예시](img/1.27_alret_2.jpg)

![CHZZK 알림창 예시](img/1.27_alret_3.jpg)

> 위 스크린샷은 1.27 UI 기준이며, 전체적인 화면 구성은 v1.280에서도 동일한 흐름을 유지합니다.

---

## 주요 기능

- 지원 라이브 플랫폼의 LIVE / OFFLINE / UNKNOWN 상태 확인
- 등록한 라이브 채널의 방송 시작 알림
- 플랫폼에 따라 방송 제목, 카테고리, 시청자 정보 및 썸네일 표시
- YouTube 새 영상 및 Shorts 알림
- Hikimori Neko **CHZZK 1개 + YouTube 2개** 기본 프로파일
- 여러 스트리머 프로파일 등록
- 하나의 프로파일에 여러 채널 등록 및 동일 플랫폼 다중 채널 지원
- 채널별 알림 ON / OFF
- 채널별 방송 시작 시 페이지 자동 열기
- 알림창에서 해당 방송 또는 콘텐츠 페이지 열기
- 프로파일별 알림창 꾸미기
- 프로파일별 알림 배경 및 글자 색 설정
- 프로파일별 알림 배경 이미지/미디어 사용 ON / OFF
- 프로파일별 방송·영상 썸네일 배경
- 사용자 배경이나 사용할 수 있는 썸네일이 없을 때 표시되는 HIKI 기본 배경
- **JPG / JPEG / PNG / GIF / WebP / WebM** 사용자 지정 알림 배경
- 정적 및 움직이는 WebP 지원
- VP8 / VP9 / AV1 WebM 배경 지원
- 일반 WAV 알림음과 별도로 켜고 끌 수 있는 알림 배경 미디어 오디오
- 헤더 / 본문 글자 크기 조절
- 글자 외곽선 색 및 **외곽선 두께 0~6 px** 조절
- 알림창 투명도 및 표시 시간 조절
- 저장 전 알림창 테스트
- 테스트 알림에서 현재 로드된 Provider 로고를 랜덤으로 미리보기
- 프로파일별 알림음: 기본 내장 WAV / 사용자 WAV / 무음
- DPI 대응 및 크기 조절이 가능한 메인 화면
- 기본 / 다크 메인 화면 테마
- 항목별 내장 도움말
- 중복 실행 방지로 이중 polling 및 중복 알림 방지
- 시스템 트레이 상주
- Windows 시작 시 자동 실행
- English / 한국어 / 日本語 언어팩

---

## 다운로드 및 실행

최신 ZIP 파일은 다음 페이지에서 받을 수 있습니다.

https://github.com/laikal/HIKI-Notifier/releases

ZIP 파일의 전체 내용을 압축 해제한 뒤:

`HIKI Notifier.exe`

를 실행하면 됩니다.

별도의 설치 프로그램은 필요하지 않습니다.

기존 버전에서 업데이트할 때는 EXE 하나만 교체하지 말고 배포 ZIP의 프로그램 파일 전체를 함께 교체하세요. 기존 `settings.json`과 `Profiles` 데이터는 그대로 유지할 수 있습니다.

---

## Hikimori Neko 기본 프로파일

**Hikimori Neko** 프로파일은 프로그램에 기본으로 포함되어 있습니다.

구성:

```text
Hikimori Neko
├─ CHZZK
├─ YouTube — 메인
└─ YouTube — 다시보기
```

기본 프로파일과 미리 등록된 채널 주소는 삭제하거나 변경할 수 없습니다.

각 채널의 알림 설정, 알림창 꾸미기, 알림음은 변경할 수 있습니다.

---

## 다른 스트리머 프로파일 추가

메인 화면에서 **프로파일 추가**를 누릅니다.

스트리머 이름, 선택 사항인 메모, 하나 이상의 지원 채널 주소를 입력할 수 있습니다.

예:

```text
https://chzzk.naver.com/live/CHANNEL_ID
https://www.youtube.com/@HANDLE
https://www.twitch.tv/CHANNEL
https://kick.com/CHANNEL
https://twitcasting.tv/CHANNEL
```

지원되는 플랫폼 주소는 설치된 Provider를 통해 자동으로 인식됩니다.

하나의 스트리머 프로파일에 여러 채널을 연결할 수 있으며, 같은 플랫폼의 서로 다른 채널도 여러 개 등록할 수 있습니다.

---

## 알림

각 채널의 알림을 개별적으로 켜거나 끌 수 있습니다.

지원되는 라이브 플랫폼에서 새 방송 시작이 확인되면 데스크톱 알림창이 표시됩니다.

플랫폼에 따라 다음 정보가 표시될 수 있습니다.

- 스트리머 이름
- 방송 제목
- 카테고리
- 현재 시청자 수
- 방송·영상 썸네일

YouTube에서는 새 영상 또는 Shorts가 확인되면 새 콘텐츠 알림을 받을 수 있습니다.

알림창에서 제공되는 방송/콘텐츠 열기 기능을 사용하면 해당 페이지를 기본 브라우저로 열 수 있습니다.

---

## 방송 시작 시 페이지 자동 열기

지원되는 라이브 채널은 새 방송이 시작될 때 해당 방송 페이지를 자동으로 열도록 설정할 수 있습니다.

이 옵션은 채널별로 설정하며 기본값은 OFF입니다.

단순히 프로그램이 LIVE 상태를 확인할 때마다 페이지를 여는 기능은 아니며 새 방송 시작을 기준으로 동작합니다.

---

## 알림창 꾸미기

각 스트리머 프로파일마다 알림 배경, 글자 스타일, 알림음 등을 따로 설정할 수 있습니다.

### 프로파일별 설정

- 알림 배경 이미지/미디어 사용
- 영상 썸네일을 알림 배경으로 사용
- 사용자 지정 배경 이미지/미디어
- 글자 색
- 글자 외곽선 색
- 글자 외곽선 두께: **0~6 px**
- 알림음 모드
- 사용자 WAV 선택

알림 배경 이미지 사용이 켜져 있을 때 일반적인 배경 우선순위는 다음과 같습니다.

1. **사용자가 지정한 프로파일 배경/미디어**
2. **방송·영상 썸네일** — 옵션이 켜져 있고 썸네일을 사용할 수 있을 때
3. **HIKI 기본 배경 이미지**

**알림 배경 이미지 사용**을 끄면 이미지 없는 알림 방식으로 표시됩니다.

지원 라이브 플랫폼의 방송 알림에서는 라이브 썸네일을 사용할 수 있으며, YouTube 새 영상·Shorts 알림에서는 영상 썸네일을 사용할 수 있습니다.

### 글자 / 표시 설정

- 헤더 글자 크기: **12~20 pt**
- 본문 글자 크기: **10~16 pt**
- 글자 외곽선 두께: **0~6 px**
- 알림창 투명도: **50~100%**
- 알림 표시 시간: **3~30초**

### 지원하는 사용자 지정 배경 형식

- JPG
- JPEG
- PNG
- GIF
- WebP
- WebM

움직이는 GIF와 Animated WebP는 알림창이 표시되는 동안 반복 재생됩니다.

WebM은 프로그램에서 검증된 VP8 / VP9 / AV1 재생 경로를 사용합니다. WebM 등 배경 미디어의 오디오는 일반 WAV 알림음과 별도로 켜거나 끌 수 있습니다.

파일이 없거나 손상됐거나 지원하지 않는 미디어인 경우에도 알림 자체가 막히지 않도록 안전하게 기본 배경으로 폴백합니다.

설정을 저장하기 전에 현재 변경값으로 알림창과 알림음을 테스트할 수 있습니다. 테스트 알림에서는 현재 프로파일에 등록된 채널 종류와 관계없이 로드된 Provider 중 하나의 로고를 랜덤으로 표시할 수 있습니다.

방송 및 콘텐츠 제목은 최대 2줄까지 표시되며, 이미지·영상 배경에서도 글자를 읽기 쉽도록 알림창이 구성되어 있습니다.

---

## 메인 화면 배경 미디어

메인 화면도 기존 스킨/배경 시스템을 통해 사용자 지정 배경 미디어를 사용할 수 있습니다.

이미지 형식과 함께 WebP 및 WebM을 사용할 수 있으며, 움직이는 미디어는 메인 창이 숨겨지거나 최소화된 동안 일시정지되고 다시 표시되면 재생을 이어갑니다.

메인 배경 미디어 오디오는 선택 사항이며, 일반 알림 WAV 및 알림창 배경 미디어 오디오와 별도로 관리됩니다.

---

## 메인 화면 테마

두 가지 메인 화면 테마를 사용할 수 있습니다.

- **기본** — 기존 HIKI Notifier 화면
- **다크** — 어두운 색상 테마

테마는 **설정**에서 변경할 수 있습니다.

---

## 사용 방법 / 도움말

프로그램 안의 **사용 방법** 화면에서 다음 내용을 항목별로 확인할 수 있습니다.

- 빠른 시작
- 스트리머 프로파일
- 채널 추가
- 지원 플랫폼
- 알림 설정
- 방송 페이지 자동 열기
- 알림창 꾸미기
- 백업
- 문제 해결
- 기타 주요 사용법

---

## 알림음

알림음은 **스트리머 프로파일별로 알림창 꾸미기에서 설정**합니다.

다음 방식을 사용할 수 있습니다.

- **프로그램 기본 내장 WAV**
- **사용자 지정 WAV**
- **무음**

서로 다른 스트리머 프로파일에 서로 다른 사용자 WAV를 지정할 수 있습니다.

일반 WAV 알림음은 WebM 같은 알림 배경 미디어에 포함된 오디오와 별개의 기능입니다.

---

## YouTube 새 콘텐츠 확인

HIKI Notifier는 Google 또는 YouTube 로그인 없이 새 YouTube 영상과 Shorts를 확인할 수 있습니다.

이미 확인된 콘텐츠는 정상적인 사용 중 반복해서 알리지 않습니다.

YouTube에서 공개하는 정보에 의존하는 기능이므로 향후 YouTube 측 변경에 따라 프로그램 업데이트가 필요할 수 있습니다.

---

## 시스템 트레이

메인 창을 닫거나 최소화해도 HIKI Notifier는 종료되지 않습니다.

프로그램은 Windows 시스템 트레이에서 계속 동작합니다.

트레이 메뉴에서는 다음 기능을 사용할 수 있습니다.

- HIKI Notifier 열기
- 사용 가능한 채널 기능
- 채널별 알림 ON / OFF
- 방송 / 콘텐츠 페이지 열기
- 프로그램 정보
- 프로그램 종료

완전히 종료하려면 트레이 아이콘을 우클릭한 뒤 **프로그램 종료**를 선택하세요.

---

## Windows 시작 시 자동 실행

HIKI Notifier를 Windows 시작 시 자동으로 실행하도록 설정할 수 있습니다.

이 옵션은 **설정**에서 변경할 수 있습니다.

---

## 프로파일 데이터와 백업

스트리머 프로파일은 다음 폴더에 저장됩니다.

`Profiles\`

프로그램 전체 설정은 다음 파일에 저장됩니다.

`settings.json`

간단히 백업하려면 HIKI Notifier를 종료한 뒤 다음 두 항목을 보관하세요.

```text
Profiles\
settings.json
```

프로파일별 알림 배경, 미디어 참조, 알림음 등의 설정도 프로그램의 프로파일 저장 구조에 따라 함께 보관됩니다.

---

## 언어

기본 제공 언어팩:

- English
- 한국어
- 日本語

추가 INI 언어팩은 다음 위치에 넣을 수 있습니다.

```text
lang\*.ini
```

번역 항목이 없는 경우 English 언어팩을 기본값으로 사용합니다.

---

## 시스템 요구 사항

- 64비트 Windows 10 또는 Windows 11
- x64 프로세서
- .NET Framework 4.8

---

## 개발자

**Eltax**

---

## Special Thanks

### Hikimori Neko

이 프로그램을 만들게 된 계기가 되어준 사람.

프로그램에 포함된 HIKI 기본 알림 배경 이미지는 Hikimori Neko의 허가를 받아 사용합니다.

---

## 면책 고지

HIKI Notifier는 비공식 팬메이드 유틸리티입니다.

지원 플랫폼 또는 관련 운영사와 공식적으로 제휴하거나 승인받은 프로그램이 아닙니다.

각 서비스명과 상표는 해당 권리자에게 귀속됩니다.

서비스 측 API, 공개 피드 또는 웹 구조가 변경될 경우 일부 기능이 정상적으로 동작하지 않을 수 있습니다.

---

## 라이선스

HIKI Notifier 프로젝트 소스 코드에는 현재 별도의 오픈소스 라이선스가 제공되지 않습니다.

**Copyright © Eltax. All rights reserved.**

제3자 상표, 서비스명 및 외부 출처 자산은 각 권리자와 해당 라이선스 조건에 따릅니다.

배포본에 포함된 제3자 런타임 구성 요소에는 각각의 라이선스가 적용됩니다. 관련 고지와 라이선스 원문은 배포본의 `Providers/Module/ThirdPartyNotices.md` 및 `Providers/Module/Licenses/`를 확인하세요.

---

# 日本語

**[English](#hiki-notifier) | [한국어](#한국어) | 日本語**

**現在のバージョン: v1.280**

**HIKI Notifier** は、複数の配信プラットフォームの配信状況や新着コンテンツを確認し、Windows通知で知らせる軽量な常駐型通知ユーティリティです。

**対応プラットフォーム:** CHZZK · YouTube · RPLAY · Twitch · SOOP · CIME · Kick · TwitCasting · Bilibili · Niconico Live · Mirrativ · SHOWROOM · Picarto.TV

- **CHZZK** — 配信状態、配信タイトル、視聴者数、サムネイル、配信開始通知
- **YouTube** — 新着動画・Shorts通知
- **RPLAY** — 配信状態、配信開始通知
- **Twitch** — 配信状態、配信開始通知
- **SOOP** — 配信状態、配信開始通知
- **CIME** — 配信状態、配信タイトル、視聴者情報、配信開始通知
- **Kick** — 配信状態、配信タイトル、視聴者数、配信開始通知
- **TwitCasting** — 配信状態、配信タイトル、配信開始通知
- **Bilibili** — 配信状態、配信開始通知
- **Niconico Live** — 一般ユーザー配信の状態確認・配信開始通知
- **Mirrativ** — 公開LIVEの状態確認・配信開始通知
- **SHOWROOM** — 配信状態、配信開始通知
- **Picarto.TV** — 配信状態、配信開始通知

標準の **Hikimori Neko** プロフィールには、CHZZK 1チャンネルとYouTube 2チャンネルが含まれています。

ほかの配信者プロフィールを追加でき、1つのプロフィールに同じプラットフォームの別チャンネルを複数登録することもできます。

対応プラットフォームの確認にアカウントログインは必要ありません。

> **非公式ファンメイドユーティリティ**  
> HIKI Notifierは、各対応プラットフォームまたはその運営会社が公式に提供・承認するソフトウェアではありません。

---

## スクリーンショット

### メイン画面

![HIKI Notifier メイン画面](img/1.27_main_jp.jpg)

### 通知カスタマイズ

![通知カスタマイズ](img/1.27_Cutomize_jp.jpg)

### 通知例

![YouTube 通知例](img/1.27_alret_1.jpg)

![YouTube 通知例](img/1.27_alret_2.jpg)

![CHZZK 通知例](img/1.27_alret_3.jpg)

> 上記のスクリーンショットは1.27 UIのものですが、v1.280でも全体的なレイアウトは同じ流れを維持しています。

---

## 主な機能

- 対応ライブ配信プラットフォームのLIVE / OFFLINE / UNKNOWN状態を確認
- 登録したライブチャンネルの配信開始通知
- プラットフォームに応じて配信タイトル、カテゴリ、視聴者情報、サムネイルを表示
- YouTubeの新着動画・Shorts通知
- Hikimori Nekoの **CHZZK 1チャンネル + YouTube 2チャンネル** 標準プロフィール
- 複数の配信者プロフィール
- 1つのプロフィールに複数チャンネルを登録可能
- 同一プラットフォームの別チャンネルを複数登録可能
- チャンネルごとの通知ON / OFF
- チャンネルごとの配信ページ自動オープン
- 通知から配信・コンテンツページを開く
- プロフィールごとの通知カスタマイズ
- プロフィールごとの通知背景・文字色設定
- プロフィールごとの通知背景画像/メディア ON / OFF
- プロフィールごとの配信・動画サムネイル背景
- カスタム背景や利用可能なサムネイルがない場合のHIKI標準背景
- **JPG / JPEG / PNG / GIF / WebP / WebM** カスタム通知背景
- 静止WebP・アニメーションWebP対応
- VP8 / VP9 / AV1 WebM背景対応
- 通常のWAV通知音とは独立した通知背景メディア音声のON / OFF
- ヘッダー / 本文フォントサイズ調整
- 文字アウトライン色と **アウトライン太さ 0～6 px** の調整
- 通知の透明度・表示時間調整
- 保存前の通知テスト
- テスト通知で読み込まれているProviderロゴをランダムにプレビュー
- プロフィールごとの通知音: 標準内蔵WAV / カスタムWAV / 無音
- DPI対応・サイズ変更可能なメイン画面
- 標準 / ダークテーマ
- カテゴリ別の内蔵ヘルプ
- 二重起動防止による重複ポーリング・重複通知の防止
- システムトレイ常駐
- Windows起動時の自動実行
- English / 한국어 / 日本語 言語パック

---

## ダウンロードと起動

最新版のZIPファイル:

https://github.com/laikal/HIKI-Notifier/releases

ZIPファイルの内容をすべて展開し、

`HIKI Notifier.exe`

を実行してください。

インストーラーは必要ありません。

既存バージョンから更新する場合は、EXEだけではなく配布ZIPのプログラムファイル一式を置き換えてください。既存の `settings.json` と `Profiles` データはそのまま利用できます。

---

## Hikimori Neko 標準プロフィール

**Hikimori Neko** プロフィールは最初から登録されています。

構成:

```text
Hikimori Neko
├─ CHZZK
├─ YouTube — Main
└─ YouTube — Replay
```

標準プロフィールと登録済みチャンネルURLは削除・変更できません。

各チャンネルの通知設定、通知外観、通知音は変更できます。

---

## ほかの配信者プロフィールを追加

メイン画面で **プロフィール追加** を選択します。

配信者名、任意のメモ、1つ以上の対応チャンネルURLを入力できます。

例:

```text
https://chzzk.naver.com/live/CHANNEL_ID
https://www.youtube.com/@HANDLE
https://www.twitch.tv/CHANNEL
https://kick.com/CHANNEL
https://twitcasting.tv/CHANNEL
```

対応しているプラットフォームURLは、インストールされているProviderによって自動的に認識されます。

1つのプロフィールに複数のチャンネルを登録でき、同じプラットフォームの別チャンネルも複数登録できます。

---

## 通知

各チャンネルの通知は個別にON / OFFできます。

対応ライブ配信プラットフォームで新しい配信開始が確認されると、デスクトップ通知が表示されます。

プラットフォームによって次の情報が表示されます。

- 配信者名
- 配信タイトル
- カテゴリ
- 現在の視聴者数
- 配信・動画サムネイル

YouTubeでは、新しい動画またはShortsが確認されると新着コンテンツ通知を受け取れます。

通知に用意された配信/コンテンツを開く操作から、対象ページを既定のブラウザーで開けます。

---

## 配信開始時にページを自動で開く

対応するライブチャンネルでは、新しい配信開始時に配信ページを自動的に開くよう設定できます。

この設定はチャンネルごとに管理され、初期状態ではOFFです。

単にLIVE状態を確認するたびにページを開く機能ではなく、新しい配信開始を基準に動作します。

---

## 通知カスタマイズ

配信者プロフィールごとに、通知背景、文字スタイル、通知音などを個別に設定できます。

### プロフィールごとの設定

- 通知の背景画像/メディアを使用
- 動画のサムネイルを通知背景に使用
- カスタム背景画像/メディア
- 文字色
- 文字アウトライン色
- 文字アウトライン太さ: **0～6 px**
- 通知音モード
- カスタムWAV選択

通知背景を有効にしている場合、通常の背景優先順位は次のとおりです。

1. **プロフィールに設定したカスタム背景/メディア**
2. **配信・動画サムネイル** — オプションが有効で、利用できる場合
3. **HIKI標準背景画像**

**通知の背景画像を使用**を無効にすると、画像を使わない通知スタイルで表示されます。

対応ライブ配信プラットフォームの通知では配信サムネイルを利用でき、YouTubeの新着動画・Shorts通知では動画サムネイルを利用できます。

### 文字 / 表示設定

- ヘッダーフォントサイズ: **12～20 pt**
- 本文フォントサイズ: **10～16 pt**
- 文字アウトライン太さ: **0～6 px**
- 通知透明度: **50～100%**
- 通知表示時間: **3～30秒**

### 対応カスタム背景形式

- JPG
- JPEG
- PNG
- GIF
- WebP
- WebM

アニメーションGIFとアニメーションWebPは通知表示中に繰り返し再生されます。

WebMはアプリで検証されたVP8 / VP9 / AV1再生経路を使用します。WebMなどの背景メディア音声は、通常のWAV通知音とは独立してON / OFFできます。

ファイルが存在しない、破損している、または未対応のメディアの場合でも、通知そのものが停止しないよう安全に標準背景へフォールバックします。

保存前に現在の設定と通知音で通知をテストできます。テスト通知では、そのプロフィールに登録されているチャンネル種類とは関係なく、読み込まれているProviderのいずれかのロゴをランダムに表示できます。

配信・コンテンツタイトルは最大2行まで表示され、画像・動画背景上でも文字を読みやすいレイアウトになっています。

---

## メイン画面の背景メディア

メイン画面も既存のスキン/背景システムを利用してカスタム背景メディアを使用できます。

画像形式に加えてWebPとWebMを利用でき、アニメーションメディアはメインウィンドウが非表示または最小化されている間は一時停止し、再表示時に再生を再開します。

メイン背景メディアの音声は任意で、通常の通知WAVおよび通知背景メディア音声とは別に管理されます。

---

## メイン画面テーマ

2種類のテーマを利用できます。

- **標準** — 従来のHIKI Notifier画面
- **ダーク** — ダークカラーテーマ

テーマは **設定** から変更できます。

---

## ヘルプ

アプリ内の **ヘルプ** 画面では、次の内容をカテゴリ別に確認できます。

- クイックスタート
- 配信者プロフィール
- チャンネル追加
- 対応プラットフォーム
- 通知設定
- 配信ページ自動オープン
- 通知カスタマイズ
- バックアップ
- トラブルシューティング
- その他の主な使い方

---

## 通知音

通知音は **配信者プロフィールごとに通知カスタマイズ画面で設定**します。

利用できるモード:

- **標準の内蔵WAV通知音**
- **カスタムWAV**
- **無音**

プロフィールごとに別々のカスタムWAVを設定できます。

通常のWAV通知音は、WebMなどの通知背景メディアに含まれる音声とは別の機能です。

---

## YouTube新着コンテンツ

HIKI NotifierはGoogleまたはYouTubeへのログインなしで、新しいYouTube動画とShortsを確認できます。

通常の利用中、すでに確認済みのコンテンツを繰り返し通知することはありません。

YouTubeが公開している情報を利用する機能のため、YouTube側の変更によってはHIKI Notifierの更新が必要になる場合があります。

---

## システムトレイ

メインウィンドウを閉じたり最小化したりしてもHIKI Notifierは終了しません。

アプリはWindowsのシステムトレイで動作を続けます。

トレイメニューでは次の操作ができます。

- HIKI Notifierを開く
- 利用可能なチャンネル操作
- チャンネルごとの通知ON / OFF
- 配信 / コンテンツページを開く
- プログラム情報
- アプリを終了

完全に終了するには、トレイアイコンを右クリックして **終了** を選択してください。

---

## Windows起動時に自動実行

HIKI NotifierをWindows起動時に自動実行するよう設定できます。

この設定は **設定** から変更できます。

---

## プロフィールデータとバックアップ

配信者プロフィール:

`Profiles\`

アプリ全体の設定:

`settings.json`

バックアップする場合はHIKI Notifierを終了して次の2項目を保存してください。

```text
Profiles\
settings.json
```

プロフィールごとの通知背景、メディア参照、通知音などもアプリのプロフィール保存構造に従って一緒に保存されます。

---

## 言語

標準言語パック:

- English
- 한국어
- 日本語

追加のINI言語パックは次の場所に追加できます。

```text
lang\*.ini
```

翻訳項目がない場合はEnglishをフォールバックとして使用します。

---

## システム要件

- 64-bit Windows 10 または Windows 11
- x64プロセッサ
- .NET Framework 4.8

---

## 開発者

**Eltax**

---

## Special Thanks

### Hikimori Neko

このプロジェクトを作るきっかけをくれた人。

内蔵のHIKI標準通知背景画像は、Hikimori Nekoの許可を得て使用しています。

---

## 免責事項

HIKI Notifierは非公式のファンメイドユーティリティです。

各対応プラットフォームまたはその運営会社とは公式な提携関係になく、承認を受けたソフトウェアでもありません。

各サービス名および商標は、それぞれの権利者に帰属します。

サービス側のAPI、公開フィード、Web構造などが変更された場合、一部の機能が正常に動作しなくなる可能性があります。

---

## ライセンス

HIKI Notifierプロジェクトのソースコードには現在、オープンソースライセンスは提供されていません。

**Copyright © Eltax. All rights reserved.**

第三者の商標、サービス名、および外部由来のアセットには、それぞれの権利者およびライセンス条件が適用されます。

配布物に含まれる第三者ランタイムコンポーネントには、それぞれのライセンスが適用されます。関連する通知とライセンス本文は、配布物の `Providers/Module/ThirdPartyNotices.md` および `Providers/Module/Licenses/` を参照してください。
