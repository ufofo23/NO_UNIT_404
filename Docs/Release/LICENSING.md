# 라이선스 현황

출시 전에 답이 확정되어 있어야 하는 항목만 모았다. 각 항목은 **확인한 출처**와
**현재 프로젝트 상태**를 함께 적는다.

## 1. Unity 에디터 라이선스

현재 이 프로젝트가 빌드되는 라이선스는 **Unity Personal** 이다.
(`Id: 14569699475687-UnityPersXXXX`, Product: Unity Personal, 만료: 무제한)

### 1.1 스플래시 스크린 — 제거 가능, 확인 완료

Unity는 Runtime Fee 취소 발표에서 다음을 명시했다.

> The Made with Unity splash screen will become **optional for Unity Personal** games
> made with Unity 6.

- 본 프로젝트는 Unity **6000.3.2f1** 이므로 해당된다.
- `PlayerSettings.SplashScreen.show = false`를 적용했고
  (`Editor/ReleaseSetup.cs`), `ProjectSettings.asset`에
  `m_ShowUnitySplashScreen: 0` / `m_ShowUnitySplashLogo: 0`으로 기록되어 있다.
- **릴리스 빌드를 두 번 만든 뒤에도 값이 0으로 유지된다.** 라이선스가 스플래시를
  강제하는 버전에서는 빌드 파이프라인이 이 값을 되돌리므로, 되돌아오지 않았다는
  사실 자체가 강제되지 않는다는 증거다.
- **육안 확인은 아직 남아 있다.** 빌드를 실행해 첫 5초를 보면 된다.
  자동 캡처를 다시 시도했고 다시 실패했다. 원인이 이제 분명하다: 이 빌드는 D3D12
  전체화면이라 GDI 화면 캡처(`CopyFromScreen`)가 검은 프레임만 돌려주고, 창 모드로
  띄워도 비대화형 세션에서는 창 핸들을 잡지 못한다. **화면 캡처로는 이 항목을 자동화할
  수 없다.**
- 간접 증거는 전부 "스플래시 없음"을 가리킨다:
  `m_ShowUnitySplashScreen: 0`이 릴리스 빌드 **3회** 후에도 유지되고, 릴리스 점검이
  `SplashScreen.show`를 경고하지 않으며, 빌드 산출물 `globalgamemanagers`에 splash
  문자열이 없고, 한 번 잡힌 프레임에서 창 등장부터 메인 메뉴까지가 0.6초 미만으로
  Unity 스플래시 최소 표시 시간(약 2초)보다 짧다.
- **그래도 직접 본 기록은 아니다.** 30초면 끝나는 일이므로 사람이 한 번 확인할 것.
- **주의**: Unity 2022 이하에서는 Personal 라이선스일 때 이 값이 빌드 시점에 조용히
  되돌려진다. 에디터 버전을 내리면 이 항목이 되살아난다.

### 1.2 매출·펀딩 한도

Unity Personal 사용 자격은 **직전 12개월 총 매출 및 조달 자금 20만 USD 미만**이다.
(Unity 6 출시일인 2024년 10월 17일부로 기존 10만 USD에서 상향.)

- GDD 1.6 권장가 USD 8.99 기준, 한도까지 약 **22,000장**.
- 이를 넘기면 Unity Pro로 전환해야 한다 (20만 ~ 2,500만 USD 구간, 2025년 1월 1일 발효).
- **Runtime Fee는 취소되었다.** Unity 6로 만든 게임에 런타임 수수료는 없다.

### 1.3 남은 확인 사항

- [ ] 상업 판매 주체를 개인으로 할지 사업자로 할지 (Personal 자격은 매출 기준이지
      법인격 기준이 아니다)
- [ ] 판매 시작 후 매출이 한도에 근접하면 Pro 전환 시점을 놓치지 않도록 추적

## 2. 배포하는 서드파티

| 대상 | 라이선스 | 재배포 | 프로젝트 내 위치 |
|---|---|---|---|
| Pretendard (폰트) | SIL OFL 1.1 | 가능, **고지 동봉 필수** | `Resources/NO404/Fonts/` |
| Steamworks.NET | MIT | 가능, 고지 동봉 | `Assets/com.rlabrecque.steamworks.net/` |
| Steamworks SDK (`steam_api64.dll`) | Valve Steamworks SDK 계약 | Steam 배포 목적 한정 | Steamworks.NET 패키지 내 |
| Unity 런타임 | Unity Editor Software Terms | 빌드 산출물로 배포 | — |

### 2.1 고지가 실제로 배포되는 경로

두 곳 모두에 들어간다. 하나만으로는 부족하다.

1. **게임 내** — 메인 메뉴 > 만든 사람들 (`UI/CreditsView.cs`).
   `Resources`에서 OFL 원문을 읽어 그대로 표시한다.
2. **빌드 폴더** — `Licenses/` 하위에 평문 복사
   (`BuildScript.CopyNotices()`가 자동 수행).

Pretendard는 **Reserved Font Name**이 걸려 있다 — 폰트 파일명이나 내부 이름을
`Pretendard`를 포함한 채로 **수정·개명해서는 안 된다.** 현재는 원본을 그대로 쓰고
있으므로 문제없다.

### 2.2 아직 없는 것

- 음원: 현재 `Tools/GenerateAudio.py`로 자체 합성한 것이라 **전량 자체 저작물**이다.
  외부 음원을 도입하면 이 표에 추가하고, GDD 19.4가 요구하는
  "스트리머 모드에서 모두 사용 가능한" 조건(저작권 안전)을 반드시 확인할 것.
- 폰트 추가분: GDD 16.4는 로그·타임스탬프에 IBM Plex Mono(OFL)를 지정한다.
  현재 UI가 단일 폰트라 미도입.

## 3. 출처

- Unity, "Unity is Canceling the Runtime Fee" — https://unity.com/blog/unity-is-canceling-the-runtime-fee
- Unity, "Unity Pricing Changes" — https://unity.com/products/pricing-updates
- Unity Editor Software Terms — https://unity.com/legal/editor-terms-of-service/software
- SIL Open Font License 1.1 — `Resources/NO404/Fonts/Pretendard-OFL.txt`
