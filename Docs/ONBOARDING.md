# 합류 안내 — 개발 환경과 버전

새로 합류하는 사람이 **코드를 받아서 Play 버튼을 누르기까지** 필요한 것 전부.
기준 커밋 `1cf0654` · 작성 2026-09-14.

---

## 0. 먼저 읽을 것 — 지금 그대로는 넘길 수 없다

레포에 리모트가 없고(`git remote -v`가 비어 있음), **아직 커밋되지 않은 파일 78개
(약 24MB)** 가 작업 트리에만 있다. 그중 `Scripts/Core/MainOnlyMode.cs` 는
`GameLoop` · `CctvService` · `PhoneService` · `ThreatService` · `NightPoolService` ·
`ManualEventService` · `DevConsole` 일곱 개가 **이미 참조하고 있는 타입**이다.
`VisitorAppearance.cs`, `FirstGuestMotion.cs`, `Resources/NO404/Characters/` 도 같은 상태다.

그래서 지금 이 폴더를 그대로 zip으로 묶어 보내면 **컴파일이 안 된다.**
넘기기 전에 작성자가 먼저 해야 할 일:

```bash
git add -A
git commit -m "..."          # 미커밋 소스 78개 + GDD v5.0 반영
git remote add origin <URL>  # GitHub private 등
git push -u origin master
```

같이 정리되어야 하는 것:

- 루트의 GDD 5종이 **작업 트리에서 삭제**된 상태이고(삭제가 커밋되지 않음),
  현재 기준 문서인 `NO_UNIT_404_GDD_v5.0.md` 는 **untracked** 다.
  지금 clone 하면 받는 사람은 **옛날 v1.0/v2.1/v3.0만 받고 v5.0은 못 받는다.**
- `CLAUDE.md` 의 "Source of truth" 절은 아직 v3.0 스택(1–4인 협동 기준)을 가리킨다.
  실제 기준은 v5.0(솔로 우선)이고, 현황은 `Docs/V5_STATUS.md` 에 있다.

레포 크기는 `.git` 19MB · `Assets` 29MB이고 가장 큰 추적 파일이 1.5MB 폰트라
**Git LFS는 필요 없다.** 그냥 push하면 된다.

반대로 **폴더째 zip으로 보내면 안 된다** — 이 폴더의 `Library/` 는 현재 **6.4GB**다.
git이 추적하는 실제 소스는 다 합쳐 50MB 미만이고, `Library/`는 받는 쪽에서 첫 임포트 때
다시 만들어진다.

---

## 1. 설치 목록

| 항목 | 버전 | 비고 |
|---|---|---|
| **Unity Editor** | **6000.3.2f1** (revision `a9779f353c9b`) | 정확히 이 버전. Unity Hub로 설치 |
| Unity 모듈 | **Windows Build Support (IL2CPP 불필요)** | 빌드는 Mono 백엔드 |
| Unity 계정 | 각자 개인 계정 + Personal 라이선스 | `Docs/Release/LICENSING.md` |
| Git | 2.x | LFS 불필요 |
| IDE | Rider 2024.3+ 또는 Visual Studio 2022 (**Unity를 사용한 게임 개발** 워크로드) | 패키지에 양쪽 IDE 브리지 모두 들어있음 |
| OS | Windows 10/11 x64 | 빌드 타깃이 Windows 전용 |
| Blender (선택) | 4.x | `Tools/*.py` 캐릭터 스크립트용. 게임 실행에는 불필요 |
| Python (선택) | 3.10+ / `numpy`, `scipy` | `Tools/GenerateAudio.py` 용. 오디오는 이미 리포에 있음 |

.NET SDK를 따로 깔 필요는 없다. Unity가 자체 Roslyn으로 컴파일한다.

## 2. 프로젝트 설정 (건드리면 안 되는 값)

| 설정 | 값 |
|---|---|
| 렌더 파이프라인 | **URP 17.3.0** |
| 스크립팅 백엔드 (Standalone) | **Mono** |
| API 호환성 레벨 | **.NET Standard 2.1** |
| 입력 | **New Input System 전용** (`activeInputHandler: 1`) — 구 `Input.GetKey`는 런타임 예외 |
| 언어 버전 | **C# 9** — `record struct`·file-scoped namespace 금지 |
| 에셋 직렬화 | Force Text · Visible Meta Files (병합을 위해 유지) |
| Scripting Define | `STEAMWORKS_NET` (Standalone) |
| **Burst AOT** | **꺼져 있음.** 켜면 Windows 빌드가 "Postprocess built player"에서 `Pipe is broken`으로 죽는다. 되살리지 말 것 (`ProjectSettings/BurstAotSettings_StandaloneWindows64.json`) |
| bundleVersion | 0.9.0 |
| 회사 / 제품명 | ProjectCaretaker / NO UNIT 404 |

## 3. 패키지 (`Packages/manifest.json`)

핵심만:

```
com.unity.render-pipelines.universal   17.3.0
com.unity.inputsystem                  1.17.0
com.unity.netcode.gameobjects          2.13.2
com.unity.services.multiplayer         2.3.1
com.unity.ai.navigation                2.0.9
com.unity.test-framework                1.6.0
com.unity.ide.rider / ide.visualstudio  3.0.38 / 2.0.25
com.unity.timeline / ugui / visualscripting
```

**서드파티 패키지는 추가하지 않는다.** 예외는 딱 둘이고 둘 다 이미 들어있다 —
`com.unity.netcode.gameobjects`(Unity 퍼스트파티, 협동용)와
`Assets/com.rlabrecque.steamworks.net`(레포에 직접 포함된 Steamworks.NET).
DI 컨테이너·런타임 코드 생성·노드 그래프 플러그인도 금지다. 자세한 이유는 `CLAUDE.md`.

## 4. 처음 열 때

1. Unity Hub → Add → 이 폴더. **6000.3.2f1** 로 연다.
2. 첫 임포트는 몇 분 걸린다 (`Library/`는 git에 없으므로 각자 로컬에서 생성된다).
3. **Tools ▸ NO404 ▸ Setup ▸ Create Entry Scene And Build Settings** — 한 번만.
4. Play. 메인 메뉴에서 *새 게임*.

`Bootstrap`이 `[RuntimeInitializeOnLoadMethod]`로 스스로 설치되므로 아무 씬에서
Play를 눌러도 돌아간다. 3번은 빌드에만 필요하다.

검증 두 가지를 습관으로:

- **Tools ▸ NO404 ▸ Data ▸ Validate Content** — 에러 0이어야 한다
- **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**

빌드: **Tools ▸ NO404 ▸ Build ▸ Windows Development Build**
→ `Builds/Development_<stamp>/NO_UNIT_404.exe`

## 5. 협동 플레이를 돌리려면 (여기서 막힌다)

Netcode + **Unity Relay**를 쓴다. IP도 포트포워딩도 필요 없지만, 대신
**Unity Gaming Services 프로젝트에 묶여 있다**:

```
ProjectSettings/ProjectSettings.asset → cloudProjectId: 7c73dc22-9a0f-4041-85cf-65c6b1542081
```

합류하는 사람이 **이 UGS 조직/프로젝트의 멤버로 초대되어야** Relay 할당을 받을 수 있다.
Unity Dashboard에서 초대하고, 에디터에서 같은 계정으로 로그인해야 한다.
초대 전에는 싱글 플레이는 되고 방 열기만 실패한다.

절차는 `Docs/COOP_TEST.md`. 단, **v5.0 기준으로 게임은 솔로 우선**이고 협동은
night 1까지만 손을 댄 상태다 — 호스트 마이그레이션·재접속 없음.

## 6. Steam

`Assets/com.rlabrecque.steamworks.net` 로 이미 연결되어 있고 **앱 아이디는 480**
(Valve 테스트 앱, `steam_appid.txt`). 에디터에서 Steam 기능을 보려면 **Steam 클라이언트가
켜져 있어야** 한다. 꺼져 있으면 초기화만 실패하고 게임은 정상 진행된다.
실제 앱 아이디는 `Core/SteamAppInfo.cs`. 파트너 사이트 설정은 `Docs/Release/STEAM.md`.

## 7. 세이브 위치

```
%USERPROFILE%\AppData\LocalLow\ProjectCaretaker\NO UNIT 404\saves\
```

슬롯 0–2 자동, 3 수동, 4 야간 시작 백업. 전부 지우려면
**Tools ▸ NO404 ▸ Delete All Saves**.

## 8. 코드를 건드리기 전에 읽을 문서

순서대로:

1. `CLAUDE.md` — 타협 불가 규칙 (`GameObject.Find` 금지, 하드코딩 문자열 금지,
   `Loc.T(key)` + `Resources/NO404/strings.csv` ko/en 동시 등록, 정수 게임 초 등)
2. `Docs/GDD/NO_UNIT_404_GDD_v5.1.md` — **현재 기준 설계 문서** (이전 버전은 `Docs/GDD/archive/`)
3. `Docs/V5_STATUS.md` — 계획이 아니라 **지금 되는 것과 안 되는 것**
4. `README.md` — 구조·조작·메뉴·치수. 단 협동 중심 서술은 v3.0 시절 잔재가 섞여 있다
5. `Docs/Scenario/NO_UNIT_404_SCENARIO_BOOK_v2_0.md`, `Docs/ENDING_PATH_v1_1.md` — 내러티브

## 9. git에 들어가지 않는 것

`.gitignore`가 `Library/` · `Temp/` · `obj/` · `Builds/` · `Logs/` · `UserSettings/` ·
`.vs/` · `.idea/` · `*.csproj` · `*.sln` 을 제외한다. 이것들은 각자 로컬에서 재생성되므로
**절대 커밋하지 말 것.** 특히 `Library/`는 이 폴더에서 6.4GB까지 자라 있다 —
임포트 캐시라 없어도 되고, 있으면 오히려 방해가 된다. 디스크는 여유 15GB 이상 잡아둘 것. 반대로 `.meta` 파일은 전부 커밋해야 한다 — 빠지면 상대방
프로젝트에서 참조가 끊긴다.
