# Steam 통합 운영 문서

GDD 20.19 / 3.1 P0. 코드 쪽 통합은 완료되어 있고, 이 문서는 **Steamworks 파트너
사이트에서 사람이 해야 하는 설정**을 다룬다. 코드와 파트너 사이트 설정이 어긋나면
게임에서는 아무 증상도 나타나지 않고 도전과제만 조용히 해제되지 않으므로, 두 쪽을
같이 두고 대조할 수 있게 한 파일로 정리한다.

## 1. 현재 상태

| 항목 | 상태 |
|---|---|
| Steamworks.NET | `Assets/com.rlabrecque.steamworks.net/` (2025.164.1, MIT) |
| 초기화·콜백·종료 | `Assets/_Project/Scripts/Steam/SteamworksBackend.cs` |
| 게임 쪽 인터페이스 | `NO404.Core.ISteamBackend` / `SteamService` |
| 앱 ID | **480 (Valve Spacewar 테스트 앱)** — 반드시 교체. `Core/SteamAppInfo.cs` |
| 재실행 핸드오프 | **동작 확인됨** — 릴리스 빌드를 직접 실행하면 `Player.log`에 `[Steam] handing off to a copy launched by Steam`이 남고 Steam을 통해 다시 뜬다 |
| 도전과제 | 코드에 24종 정의, 파트너 사이트에 **미등록** |
| 클라우드 저장 | Auto-Cloud 방식, 파트너 사이트에 **미설정** |

### 왜 별도 어셈블리인가

Steamworks.NET은 Valve가 네이티브 라이브러리를 제공하는 플랫폼으로 어셈블리를
제한한다. `NO404.Runtime`은 플랫폼 제한이 없으므로 직접 참조하면 다른 플랫폼 빌드가
전부 깨진다. 그래서 의존 방향을 뒤집어 `NO404.Steam`이 `ISteamBackend`를 구현하고
`[RuntimeInitializeOnLoadMethod]`로 자신을 등록한다.

**`Assets/_Project/Scripts/Steam/` 폴더를 통째로 지워도 게임은 빌드되고 실행된다.**
도전과제는 세이브 파일에 로컬 기록으로 남는다.

## 2. 출시 전 반드시 바꿔야 하는 것

### 2.1 앱 ID

1. `Assets/_Project/Scripts/Core/SteamAppInfo.cs`의 `AppId` 상수
2. 프로젝트 루트 `steam_appid.txt`

이 두 값은 **릴리스 점검이 서로 대조한다.** 480이면 BLOCKING이고, 둘이 다른 값이어도
BLOCKING이다 — 어긋나면 에디터와 배포된 플레이어가 서로 다른 앱과 통신한다.

App ID가 `NO404.Steam`이 아니라 `NO404.Runtime`에 있는 이유: `NO404.Steam`은 플랫폼
제한 어셈블리라 **에디터 툴이 그 안의 상수를 읽을 수 없었다.** 이 실수를 잡으라고 만든
점검이 정작 그 값을 못 보는 상태였다.

`steam_appid.txt`는 **에디터·개발 실행 전용**이다. `BuildScript.Sanitise()`가 릴리스
빌드 결과물에서 자동으로 삭제한다 — 이 파일이 함께 배포되면 플레이어가 Steam을 거치지
않고 그 앱 ID로 실행할 수 있다.

480을 그대로 두면 `SetAchievement` 호출이 성공한 것처럼 보이면서 아무 데도 기록되지
않는다. 즉 **실패가 성공처럼 보인다.**

### 2.2 도전과제 24종

파트너 사이트 > Achievements에 아래 **API Name을 문자 그대로** 등록한다. 하나라도
철자가 다르면 `SteamworksBackend.Unlock()`이 에러 로그를 남기고 해당 과제는 영원히
해제되지 않는다.

| # | API Name | 한국어 | English | 조건 | 숨김 |
|---:|---|---|---|---|:---:|
| 1 | `ACH_FIRST_SHIFT` | 첫 근무 | First Shift | 프롤로그 종료 | |
| 2 | `ACH_GOOD_CARETAKER` | 성실한 관리원 | Diligent Caretaker | 한 밤의 일반 업무 전부 정답 | |
| 3 | `ACH_NO_UNIT_404` | 404호는 없습니다 | No Unit 404 | 404호 진입 | ● |
| 4 | `ACH_TWO_COURIERS` | 두 명의 배달원 | Two Couriers | 2일차 사건 해결 | |
| 5 | `ACH_FLOOR_16` | 16층 | The Sixteenth Floor | 3일차 사건 해결 | |
| 6 | `ACH_WITNESS` | 목격자 | Witness | 이선자 증언 확보 | |
| 7 | `ACH_BACKUP` | 대체 인력 | The Replacement | 강태호 관련 판정 완료 | |
| 8 | `ACH_POWER_MANAGER` | 전력 관리 | Power Management | 예비 전력을 끊기지 않게 유지 (15.5) | |
| 9 | `ACH_FOUND_DONGSIK` | 박동식을 찾다 | Finding Dongsik | 박동식의 행방 확인 | ● |
| 10 | `ACH_TRUE_RECORD` | 기록된 사람들 | The Recorded | 엔딩 A | ● |
| 11 | `ACH_SILENCE` | 안전한 침묵 | Safe Silence | 엔딩 B | ● |
| 12 | `ACH_ERASED` | 사라진 404 | The Erased 404 | 엔딩 C | ● |
| 13 | `ACH_COLLAPSE` | 공동체 붕괴 | Collapse | 엔딩 D | ● |
| 14 | `ACH_LOOP` | 반복 근무 | The Loop | 엔딩 E (비밀) | ● |
| 15 | `ACH_FLAWLESS_NIGHT` | 무결점 근무 | Flawless Night | 한 밤의 모든 사건 정답 | |
| 16 | `ACH_UNSEEN` | 들키지 않고 | Unseen | 5일차를 한 번도 발각되지 않고 종료 | |
| 17 | `ACH_ALL_TYPES` | 여덟 가지 증거 | Eight Kinds of Proof | 증거 8종을 모두 1개 이상 확보 | |
| 18 | `ACH_EVERY_EYE` | 모든 눈 | Every Eye | 미확인 CCTV 움직임 0으로 밤 종료 | |
| 19 | `ACH_LINKED_TRUTH` | 연결된 진실 | Linked Truth | 증거 보드에서 첫 연결 생성 | |
| 20 | `ACH_PERFECT_LINE` | 놓치지 않은 전화 | Never Missed a Call | 전화를 한 번도 놓치지 않고 엔딩 도달 | |
| 21 | `ACH_TRUSTED_CARETAKER` | 신뢰받는 관리원 | Trusted Caretaker | 주민 신뢰 100 | |
| 22 | `ACH_SAFE_HOUSE` | 안전한 건물 | Safe Building | 건물 안전 100 | |
| 23 | `ACH_COMPLETIONIST` | 모든 기록 | Every Record | 엔딩 기록 전부 해제 | ● |
| 24 | `ACH_NIGHT_CHIEF` | 야간 책임자 | Night Chief | 야간 책임자 난이도로 엔딩 도달 | |

숨김(●)은 스토리를 누설하는 항목이다. 파트너 사이트에서 Hidden 체크.

**엔딩 도전과제 4종(10~13)의 이름은 `strings.csv`의 `ending.*.title`과 글자 그대로
일치해야 한다.** 도전과제 이름은 파트너 사이트에서, 엔딩 제목은 게임에서 나오므로 둘이
다르면 영어권 플레이어는 같은 엔딩을 두 개의 이름으로 보게 된다. 엔딩 E(`ACH_LOOP`,
반복 근무)만 예외로, 엔딩 제목(다음 근무자 / The Next Shift)이 비밀이라 일부러 다르다.

**아이콘**: 과제당 해제/미해제 2종, 64×64 PNG. 아직 없다 — 아트 패스 항목.

### 2.3 클라우드 저장 (Auto-Cloud)

파트너 사이트 > Cloud > **Auto-Cloud** 에 아래를 등록한다.

| 항목 | 값 |
|---|---|
| Root | `WinAppDataLocalLow` |
| Subdirectory | `ProjectCaretaker/NO UNIT 404/saves` |
| Pattern | `*.json` |
| Root (설정) | `WinAppDataLocalLow` |
| Subdirectory (설정) | `ProjectCaretaker/NO UNIT 404` |
| Pattern (설정) | `settings.json` |

경로는 `Application.persistentDataPath` = `%USERPROFILE%\AppData\LocalLow\{companyName}\{productName}` 규칙에서 나온다. `companyName`/`productName`을 바꾸면 이 경로도 같이 바꿔야 한다.

Auto-Cloud를 쓰는 이유: 세이브는 이미 `SaveService`가 체크섬과 함께 원자적으로 쓰고
있고(CLAUDE.md), 그 파일을 그대로 동기화하면 된다. ISteamRemoteStorage API로 다시
구현하면 같은 일을 두 번 하면서 실패 경로만 늘어난다.

게임 쪽에서는 `SteamService.CloudEnabled`로 동기화가 꺼져 있는지 알 수 있다 — 이것이
Auto-Cloud가 스스로 할 수 없는 유일한 부분이다.

**할당량**: 세이브 슬롯 5개 × 약 20KB + 설정. 기본 할당량으로 충분하다.

### 2.4 Rich Presence

`SteamFriends.SetRichPresence("steam_display", key)`로 **토큰 키**를 보낸다. 문장을
직접 보내면 아무것도 표시되지 않는다. 파트너 사이트 > Rich Presence Localization에
아래 토큰을 등록한다.

| 토큰 | 한국어 | English |
|---|---|---|
| `#Status_Menu` | 메인 메뉴 | In the main menu |
| `#Status_Night` | {#night}일차 근무 중 | Night {#night} |
| `#Status_Endless` | 무한 야간 근무 | Endless night shift |

## 3. 빌드 업로드

1. `Tools > NO404 > Release > Windows Release Build`
2. `Tools > NO404 > Release > Release Readiness Report` — BLOCKING 0 확인
3. Depot: Windows 64-bit 단일 depot
4. 업로드 제외 확인: `steam_appid.txt`, `*_BurstDebugInformation_DoNotShip`
   (`BuildScript.Sanitise()`가 이미 지우지만 업로드 전에 눈으로 확인)
5. `Licenses/` 폴더는 **포함**한다 (SIL OFL 1.1 요구사항)

## 4. 스토어 페이지 (미착수)

- 캡슐 아트 6종 (Header 460×215, Small 231×87, Main 616×353, Vertical 374×448,
  Library 600×900, Library Hero 3840×1240)
- 스크린샷 5장 이상 1920×1080
- 트레일러 — GDD 2.3의 8개 훅 영상
- 태그 — GDD 30.2 우선순위 참조
- 가격 USD 8.99 / KRW 11,000 (GDD 1.6)

## 5. 검증 방법

Steam 클라이언트를 켠 상태로 에디터에서 실행하면 로그에
`[Steam] initialised as <닉네임> (app 480)`이 남는다. 이 줄이 없으면 통합이 동작하지
않는 것이다. Steam이 꺼져 있으면 `[Steam] Steam is not running` — 이것은 정상이며
게임은 그대로 진행된다.
