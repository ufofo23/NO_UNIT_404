# 날짜 간 연결 가이드 — 1 → 2 → 3 → 4 → 5 → 6일차

1·3·5일차와 2·4·6일차를 다른 사람이 나눠 만들 때, 한 날의 결과를 뒤의 날이 읽게 하는
방법과 날짜별로 넘겨야 할 값을 정리했다.

- 근거: GDD v5.1 5장(상태 모델), 16장(선택 → 다음날 영향), 각 퀘스트의 "후속 영향" 항목.
- 코드 기준: 2026-10-10.

"보내기"는 앞날 퀘스트가 값을 쓰는 것, "받기"는 뒤 날 퀘스트가 그 값을 읽는 것이다.

---

## 1. 날이 넘어갈 때 엔진이 하는 일

`GameLoop.AdvanceToNextNight` → `StartNight(n)` → `CaseService.BeginNight(n)` 순서로 진행된다.

1. 전날 `nextNight = true`로 미뤄 둔 결과를 적용한다.
2. 그날 15개 풀을 추첨한다(`NightPoolService`). 이때 가중치는 `SubquestRules.WeightBonus`다.
3. 근무 체인을 배치한다(랜덤 A → 스토리 → 랜덤 B → 메인 → 나머지).

1이 2보다 먼저라서, 전날 결과로 생기는 결과 퀘스트(CONSEQUENCE)가 그날 추첨에 바로 들어갈 수 있다.

| 날이 바뀌어도 남는 것 | 매일 새로 시작하는 것 |
|---|---|
| HP·SAN (`VitalService`, 매일 회복 없음) | 시계 22:00 |
| 증거 (`EvidenceService`, 새 게임에서만 초기화) | 활성 퀘스트 목록 |
| 상태값 전부 (`GameStateService`: flag·stat·choice·debt) | 방문자·전화·전력·압력 |
| 출입 권한, 층 리스크 | 그날의 추첨 결과 |

상태값은 저장 파일에 그대로 들어간다. 새 키를 만들어도 저장 마이그레이션이 필요 없고,
코옵 미러(`ShiftMirror`)로 자동 동기화된다.

---

## 2. 보내는 쪽 — 값을 어떤 그릇에 담을까

| 넘길 것 | 그릇 | 쓰는 법 (SeedContent) | 읽는 법 | 값이 없을 때 |
|---|---|---|---|---|
| 여러 결과 중 하나 | Choice | `Pick(ChoiceIds.X, "VALUE")`, `ConsequenceDefinition.Choice` | `ChoiceIs`, `GetChoice` | `GetChoice` = `null`, `ChoiceIs` = false |
| 예/아니오 | Flag | `Set(...)`, `ConsequenceDefinition.Flag` | `GetFlag` | false |
| 누적 수치 | Stat | `Trust(n)`, `Safety(n)`, `Archive(n)`, `Alert(n)`, `Harin(n)` | `GetStat` | 0. 단 새 게임 시작값이 Trust 50, Safety 70, Archive 50이다 |
| 잘못 처리한 일의 누적 | Debt | `Owe(DebtIds.X, n)` | `GetDebt` | 0 (범위 0~5, 넘치면 잘린다) |
| 물건·기록 | Evidence | `Give("EV_...")` | `Evidence.Has`, 선택지의 `Needs("EV_...")` | 없음 |
| 다음 날 시작에 반영할 결과 | `nextNight` | `ConsequenceDefinition`의 `nextNight = true` | 다음 날 추첨 직전에 자동 적용 | — |

결과의 모양에 따라 그릇을 고른다.

- 결과가 셋 이상으로 갈리면 Choice를 쓴다. Flag 여러 개로 나누면 "돌봤고 동시에 거부했다" 같은 모순된 저장이 생길 수 있다.
- 다음 날의 "분위기"만 바꾸면 되는 값은 Stat이나 Debt로 충분하다.
- 다음 날이 특정 상황을 재현해야 하면 Choice나 Flag를 쓴다.

---

## 3. 받는 쪽 — 코드에서 읽는 세 군데

1. **추첨 가중치 — `SubquestRules.WeightBonus`.** GDD의 "Weight +N"이 여기 들어간다.
   랜덤 후보에만 효과가 있다. 메인과 고정 스토리는 항상 나온다.
2. **변형 결정 — `SubquestRules.Started`.** 퀘스트가 열리는 순간 앞날 값을 읽고, 그날 쓸
   플래그로 고정한다(예: `N5-R03` → `N5_PUMP_OK` / `N5_PUMP_OVERLOAD` / `N5_PUMP_LEAK`).
   보고서 선택지, 현장 소품, 재로드가 모두 이 플래그 하나를 읽는다. **보고 시점에 앞날 값을
   다시 계산하지 말 것.**
3. **선택지·목표 개방.** 선택지는 `When(ConditionDefinition.Flag(...))`,
   필요한 증거는 `Needs("EV_...")`로 연다.

현장 소품의 모습이 앞날 결과에 따라 달라야 하면(예: 수리기사가 녹음기를 떼어 간 흔적),
월드 빌드 시점에 플래그를 읽고 `FlagChangedEvent`를 구독한다. 구역은 다시 로드될 때마다
처음부터 지어진다.

---

## 4. 지켜야 할 규칙

1. **키는 `GameIds.cs`의 상수로만 쓴다.** 오타 하나가 조용한 연결 끊김이 된다.
   현재 문자열 리터럴로 쓰인 곳이 있어 상수로 옮겨야 한다.
   - `"PLAYER_LOST_COUNT"`: `SubquestRules`, `SelectedMainQuestRules`
   - `"DONGSIK_LAST_BROADCAST_FOUND"`, `"DONGSIK_REMAINS_LOCATION_HINT"`, `"CHOI_2009_RESPONSIBILITY_CONFIRMED"`, `"N3_HEIGHT_MATCH_NOTED"`: `SelectedMainQuestRules`
   - `"STAIR_DARK"`, `"N5_CCTV_DOWN"`: `SeedContent.V51Pools`
2. **"값 없음"은 "그 일이 없었다"로 처리한다.** 랜덤 퀘스트는 추첨되지 않을 수 있으므로
   값이 없는 것도 정상 경로다. 받는 쪽은 `null`·false일 때의 변형을 따로 정해야 한다.
   - ⚠ 현재 5일차 두 곳이 이 규칙을 어긴다.
     - `N5-R03`: `PUMP_STATUS`가 없으면 과부하 변형이 되고 가중치 +40이 붙는다.
     - `N5-R07`: `FIRE_SENSOR_STATUS`가 없으면 "신뢰 불가" 변형이 되고 가중치 +40이 붙는다.
   - 즉 1일차에 펌프·감지기 사건이 없었는데 5일차에 고장으로 나온다. GDD 기준으로는
     정상 변형이어야 한다.
3. **덮어쓰기에 주의한다.** `SetChoice`는 덮어쓴다. 뒤 날이 같은 키를 다시 쓰면 앞날 결과가
   사라진다. 예를 들어 `N5-R05`는 회수하면 `OLD_PACKAGE_STATUS`를 `PRESERVED`로 쓴다.
   6일차가 "2일차에 잃어버렸었는지"를 알아야 하면 별도 키를 둔다.
4. **하루짜리 값과 넘기는 값을 구분한다.**
   - 그날만 쓰는 진행 상태는 `NxRyy_` 접두어를 붙이고(예: `N3R08_LOOPING`) 그날 안에 정리한다.
   - 다음 날로 넘기는 값은 GDD 5.4의 이름(`PUMP_STATUS` 등)을 쓴다.
5. **엔딩 연출 플래그(`END_*`)는 메인과 고정 스토리만 쓴다.** EditMode 테스트가 강제한다.
   N6-R06·R07처럼 랜덤 퀘스트는 압박 수치만 바꾼다.
6. **페일세이프를 같이 만든다.** 앞날을 놓쳐도 진실 엔딩이 막히면 안 된다(CLAUDE.md, GDD 11.2).
   받는 쪽은 "값이 없을 때의 대체 증거" 경로를 함께 둔다.
   예: N1 고지서를 놓쳐도 N2~N4에서 대체 증거가 나온다.
7. **결과는 한 번만 적용한다.** 결과는 보고(Resolved) 시점에 한 번, `nextNight`는 다음 날 한 번
   적용된다. 같은 값을 `Started`와 `Resolved`에서 두 번 더하지 않는다.
8. **저장과 코옵 규칙을 지킨다.**
   - `GameStateService`에 넣은 값은 자동으로 저장되고 미러링된다.
   - 새 서비스에 상태를 두면 `SaveService`에 넣고, 예전 저장 파일을 마이그레이션해야 한다.
   - 상태를 바꾸는 동사는 `NetShift.Request`를 거친다. 현재 `SubquestInteractable`은
     이 경로를 거치지 않는다. 솔로 우선이라 지금은 동작하지만, 복사하기 전에 합의할 것.
9. **연결을 테스트하는 방법을 갖춘다.**
   - `night.start N`으로 건너뛰면 앞날 값이 전부 비어 있다.
   - 콘솔에 `flag.set`과 `stat.set`(debt 포함)은 있지만 **Choice를 넣는 명령이 없다.**
     `choice.set ID VALUE`를 추가해야 연결을 손으로 시험할 수 있다.
   - EditMode 계약 테스트를 둔다. 받는 쪽이 읽는 키마다 그 값을 쓰는 퀘스트가 존재하는지,
     값의 철자가 GDD 5.4의 범위 안인지 검사한다.

---

## 5. 날짜별 연결표

"상태" 칸의 뜻은 다음과 같다.

- **구현됨:** 코드에 있다.
- **N일차 담당:** 그 날을 만드는 사람이 할 일이다.
- **미구현(홀수):** 1·3·5일차 쪽에서 아직 안 한 일이다.
- **키 합의:** 결과를 담을 키가 아직 없어서, 이름부터 정해야 한다.

### 5.1 1일차 → 2일차

| 넘길 값 | 보내는 퀘스트 | 받는 곳 | 상태 |
|---|---|---|---|
| `N1_404_BILL_PRESERVED` | N1-M01 보존 | N2-M01: 2009 송장 연도 비교 힌트 자동 기록 | 보내기 구현됨 / 받기 2일차 담당 |
| `RECORD_DEBT` +1 | N1-M01 폐기 | N2 기록 복구 계열 가중치 +50 | 보내기 구현됨 / 받기 2일차 담당 |
| `ACCESS_DEBT` +1 | N1-R03 방치 | N2 방문자 일탈 추가 | 보내기 구현됨 / 받기 2일차 담당 |
| `N1R06_SLIPPERY` | N1-R06 방치 | N2 추가 민원 가중치 | 보내기 구현됨 / 받기 2일차 담당 |
| `N1R12_CLIP_KEPT`, 증거 `EV_N1R12_LOOP_CLIP` | N1-R12 클립 보존 | N2-M01 ECHO 판정 보조 | 보내기 구현됨 / 받기 2일차 담당 |
| `HarinResonance` +3, 증거 `EV_N1R13_AFTERIMAGE` | N1-R13 | N2 Identity·Echo 사건 가중치 | 전용 플래그 없음. 증거 보유로 판정할지 키를 둘지 **키 합의** |

### 5.2 1일차 → 3·4·5일차 (하루 이상 건너뛰는 연결)

| 넘길 값 | 보내는 퀘스트 | 받는 곳 | 상태 |
|---|---|---|---|
| `N1R11_IGNORED`, `DISTORTION_DEBT` +1 | N1-R11 무시 | N3-R09 가중치 +50 | 구현됨 |
| `PUMP_STATUS` (BYPASS) | N1-R02 | N3 전력 사건 가중치 | **미구현(홀수)** |
| `ACCESS_DEBT` | N1-R03 | N3 방문자 일탈 사건 | **미구현(홀수)** |
| N1-R14 스티커 | N1-R14 | N3 서비스 통로 생활 흔적과 연결 | 연출 연결만, 상태값 없음 |
| `SUNJA_CARE` | N1-R01 | N4-R01 안부, N4-R10 증언량 | 보내기 구현됨 / 받기 4일차 담당 |
| `N1_404_BILL_PRESERVED` | N1-M01 | N4-M01 윤미정 이름 교차 확인 | 보내기 구현됨 / 받기 4일차 담당 |
| 증거 `EV_N1R09_ENVELOPE` | N1-R09 | N4 DB 침투 전조 | 보내기 구현됨 / 받기 4일차 담당 |
| `SUNJA_CARE` | N1-R01 | N5-R02 선자 대피 | 구현됨 |
| `PUMP_STATUS` | N1-R02 | N5-R01 부하, N5-R03 변형 | 구현됨. 단 4장 규칙 2의 "값 없음" 문제 |
| `FIRE_SENSOR_STATUS` | N1-R07 | N5-R07 경보 신뢰도 | 구현됨. 단 같은 "값 없음" 문제 |
| `BuildingSafety`, `CommunityTrust` | N1-R04, N1-R08 등 | N5 긴급 동선, 대피 협조 | 수치 누적 |

### 5.3 2일차 → 3일차 (2일차가 보내고 3일차가 받음)

| 넘길 값 | 보내는 퀘스트 | 받는 곳 | 상태 |
|---|---|---|---|
| `ACCESS_DEBT` | N2-M01 잘못된 Full Pass, N2-R02, N2-R07, N2-R10 | N3 방문자 일탈·출입 사건 가중치 | 보내기 2일차 담당 / **받기 미구현(홀수)** |
| N2-R10 복제 카드 | N2-R10 | N3-R02 수리기사·4F 사건과 결합 | **키 합의** 후 3일차에서 받기 |
| `ECHO_RULE_CONFIRMED` | N2-M01 (메인, 구현됨) | 이후 ECHO 사건에서 자동 메모 힌트 1줄 | **받기 미구현(홀수)** |
| N2-R11 | N2-R11 | N3 4F 서비스 구역 복선 | 연출 연결만 |

### 5.4 2일차 → 4·5·6일차

| 넘길 값 | 보내는 퀘스트 | 받는 곳 | 상태 |
|---|---|---|---|
| `JUNHO_STATUS` | N2-M01 | N4 이전, 반품 송장 변형 | 짝수 날끼리 |
| `HARIN_PACKAGE` | N2-R14 | N4-M01 교차 증거 | 짝수 날끼리 |
| N2-R09 DB 삭제 | N2-R09 | N4 기록 오염 복선 | 짝수 날끼리 |
| `OLD_PACKAGE_STATUS` | N2-R05 | N5-R05 (LOST면 진짜 패키지) | 받기 구현됨. N5-R05가 PRESERVED로 덮어씀(규칙 3) |
| N2-R05 상자 | N2-R05 | N4·N5 화재 테이프 대체 증거 | **미구현(홀수)**: N5-M01 쪽 대체 증거 경로 |
| `END_2009_CALL_PRESERVED` | N2-R12 | N5 화재 재현 음성 단서(N5-M01) | **받기 미구현(홀수)** |
| `SAFETY_DEBT` | N2-R03 임시 팬 | N5 기록서버 전력 부담(N5-R01) | **받기 미구현(홀수)** |
| `FUTURE_RULE_CONFIRMED` | N2-R08 | N6 Future 해석 | 짝수 날끼리 |

### 5.5 3일차 → 4일차 (3일차가 보내고 4일차가 받음)

| 넘길 값 | 보내는 퀘스트 | 받는 곳 | 상태 |
|---|---|---|---|
| `CONTRACTOR_ACCESS`, `SERVICE_RECORDER_STATUS` | N3-R02 | N4 기록 복구 사건 | 보내기 구현됨 / 받기 4일차 담당 |
| `RECORD_DEBT`, `ChairmanAlert` +3 | N3-R06 | N4 Record 사건 가중치 | 보내기 구현됨 / 받기 4일차 담당 |
| `PLAYER_LOST_COUNT` | N3-M01, N3-R08 | N4·N5 왜곡 사건 가중치(2 이상) | 보내기 구현됨(문자열 리터럴) / 받기 4일차 담당 |
| `N3_HEIGHT_MATCH_NOTED`, 증거 `EV_HEIGHT_MARKS` | N3-M01 | N4-M01 "언니 123 / 동생 117"과 DB의 두 자매 연결 | 보내기 구현됨 / 받기 4일차 담당 |
| `END_CHILD_ITEMS_PRESERVED`, 증거 `EV_N3R12_CHILD_ITEMS` | N3-R12 | N4 가족기록 연결 | 보내기 구현됨 / 받기 4일차 담당 |

### 5.6 3일차 → 5·6일차

| 넘길 값 | 보내는 퀘스트 | 받는 곳 | 상태 |
|---|---|---|---|
| `404_WALL_RESPONSE` | N3-R07 | N5-R13 노크 증거 자동 확보, N6-R13 | N5 구현됨 / N6 담당 |
| `SERVICE_RECORDER_STATUS` | N3-R02 | N5-R06 | 구현됨 |
| `SPACE_RULE_CONFIRMED` | N3-M01 | N5-R10 메모, N6 공간 왜곡 힌트 | N5 구현됨 / N6 담당 |
| `FIRE_DOOR_PREP` | N3-R03 수리 | N5·N6 방화문 난이도 | 보내기 구현됨 / **N5 받기 미구현(홀수)** / N6 담당 |
| `STAIR_LIGHT_FIXED`, `STAIR_DARK` | N3-R05 | Lost 탈출 가독성 | 보내기 구현됨 / 받는 곳 없음 |
| `F4_REFERENCE_MARKER` | N3-R10 | N6-R03 실제 길 확인 | 보내기 구현됨 / N6 담당 |
| `FUTURE_WARNING_404` | N3-R14 녹음 저장 | N6 404 문 개방 순서 힌트 | 보내기 구현됨 / N6 담당 |

### 5.7 4일차 → 5일차 (4일차가 보내고 5일차가 받음 — 의존이 가장 큼)

| 넘길 값 | 보내는 퀘스트 | 받는 곳 | 상태 |
|---|---|---|---|
| `ChairmanAlert` | N4-M01(+5~25), N4-R02, N4-R06 | N5-R04 침입 시점. 50 이상이면 원격 잠금이 통하지 않음 | 받기 구현됨 |
| `DB404_ACTION`, `RECORD_DEBT` +2 (DELETE) | N4-M01 | N5 기록 복구 부담 | **받기 미구현(홀수)**. 어느 5일차 퀘스트가 받을지 정할 것 |
| `SUNJA_TESTIMONY` | N4-R10 | N5 대피, 박동식 위치 | **받기 미구현(홀수)**. N5-R02는 지금 `SUNJA_CARE`만 읽음 |
| N4-R03 이름 질문 | N4-R03 | N5 박동식·기록실 위치 힌트 | **키 합의** |
| N4-R06 위장 로그 | N4-R06 | N5-R04 침입 위장 로그 | **키 합의** |
| N4-R07 폐기실 이동 | N4-R07 | N5 기록실 ECHO 강도 | **키 합의** |
| 증거(N4-R08 화재 신고 테이프) | N4-R08 | N5-M01 방화문 증거 | 증거로 연결. N5-M01 쪽 대체 경로 **미구현(홀수)** |
| `DISTORTION_DEBT`, `PLAYER_LOST_COUNT` | 4일차 왜곡 퀘스트 | N5-R10 | 받기 구현됨 |
| `HARIN_RECORD_PRESERVED` | N4-M01, N4-R05 | 엔딩 에필로그 | 엔딩 쪽 미구현 |

### 5.8 4일차 → 6일차

| 넘길 값 | 보내는 퀘스트 | 받는 곳 | 상태 |
|---|---|---|---|
| `MANUAL_SOURCE_TRUST` | N4-R04 | N5·N6 매뉴얼 오염 판정 | **N5 받기 미구현(홀수)** / N6 담당 |
| `ArchiveIntegrity` | N4-M01(±15) | N6 최종 보고서의 주장 선택 폭 | 짝수 날끼리 |
| N4-R09, N4-R11, N4-R12, N4-R13 | — | N6 근거, 기억 장면, 공간 변형 | 짝수 날끼리 |

### 5.9 5일차 → 6일차 (5일차가 보내고 6일차가 받음)

| 넘길 값 | 보내는 퀘스트 | 받는 곳 | 상태 |
|---|---|---|---|
| `FIRE_DOOR_STATUS` (SAFE / JAMMED / OPEN) | N5-M01 | N6 이동 난이도, N6-R08 연기 유입 | 보내기 구현됨 / 받기 6일차 담당 |
| `DONGSIK_LAST_BROADCAST_FOUND`, `DONGSIK_REMAINS_LOCATION_HINT` | N5-M01 (N5-R13 보조) | N6-M01 유해 발견 난이도·연출 | 보내기 구현됨(문자열 리터럴) / 받기 6일차 담당 |
| `CHOI_2009_RESPONSIBILITY_CONFIRMED` | N5-M01 | N6 최종 주장 | 보내기 구현됨(문자열 리터럴) / 받기 6일차 담당 |
| `END_DONGSIK_LAST_BROADCAST_VERIFIED` | N5-R13 | 엔딩 | 보내기 구현됨 / 엔딩 쪽 미구현 |
| 전력 배분 결과 | N5-R01 | N6 시작 시스템 상태 | **키 합의**. 지금은 수치만 바뀌고 어느 셋을 살렸는지 저장되지 않음 |
| 대피 결과 | N5-R02 | N6 주민 동선, 에필로그 | **키 합의** |
| `OLD_PACKAGE_STATUS`, 증거 `EV_N5R05_CONTENTS` | N5-R05 | N6 404 내부 물품 연결 | 보내기 구현됨 / 받기 6일차 담당 |
| `N5_CCTV_DOWN`, `DISTORTION_DEBT` | N5-R08 | N6 Final Pressure 가중치 | 보내기 구현됨(문자열 리터럴) / 받기 6일차 담당 |
| `DISTORTION_DEBT` | N5-R10 | N6 공간 안정성 | 수치 |
| 장비 상태 | N5-R12 | N6 장비 상태 | **키 합의** |
| `BuildingSafety`, `CommunityTrust`, `ChairmanAlert`, `ArchiveIntegrity` | 5일차 전체 | N6 HP 위험·대피, N6-R10, N6-R02·R09, 최종 보고서 | 수치 누적 |

---

## 6. 1·3·5일차 쪽에서 먼저 정리할 것

1. `N5-R03`·`N5-R07`의 "값 없음" 처리를 정상 변형으로 고친다(4장 규칙 2).
2. 문자열 리터럴 키를 `GameIds` 상수로 옮긴다(4장 규칙 1).
3. 콘솔에 `choice.set ID VALUE`를 추가한다.
4. 계약 EditMode 테스트를 만든다. 받는 키마다 그 값을 쓰는 퀘스트가 있는지, 값이 GDD 5.4 범위 안인지 검사한다.
5. 5일차가 6일차에 넘길 키를 만든다(`N5-R01` 전력 배분, `N5-R02` 대피 결과, `N5-R12` 장비 상태). 키 이름은 6일차 담당과 정한다.
6. 짝수 날과 키를 정한 뒤, 홀수 쪽 받기 미구현분을 채운다.
   - 3일차: `ACCESS_DEBT`, `PUMP_STATUS=BYPASS`, `ECHO_RULE_CONFIRMED`
   - 5일차: `DB404_ACTION`, `SUNJA_TESTIMONY`, `END_2009_CALL_PRESERVED`, `SAFETY_DEBT`, `FIRE_DOOR_PREP`, `MANUAL_SOURCE_TRUST`
