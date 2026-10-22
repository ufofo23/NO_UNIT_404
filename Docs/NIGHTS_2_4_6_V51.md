# 2·4·6일차 v5.1 — 퀘스트 풀

기준: `NO_UNIT_404_GDD_v5.1.md` 4, 11, 13, 15.
1·3·5일차는 `Docs/NIGHTS_1_3_5_V51.md`, 메인 퀘스트(N2-M01 / N4-M01 / N6-M01)는 각 PR과
`Docs/GDD/CHANGE_PROPOSALS.md` P-01~P-03 참고.

## 1. 퀘스트 풀

1·3·5일차와 같은 구조다. 각 일자 15개: 메인 1 + 고정 스토리 서브퀘스트 1 + 랜덤 후보 13.
매 근무는 메인과 스토리 서브퀘스트에 랜덤 3개(조건 충족 시 4번째 60%)를 더해 5~6개가 된다.
이제 여섯 밤 모두 풀이 완성됐다.

| 일자 | 고정 스토리 (엔딩 영향 O) | 엔딩 플래그 | 결정되는 곳 |
|---|---|---|---|
| 2 | N2-R12 2009년 주민의 전화 | `END_2009_CALL_PRESERVED` | 전화 (`CALL_N2R12_2009`) |
| 4 | N4-R10 선자의 증언 | `END_SUNJA_TESTIMONY_FULL` | 303호 대화 (`D_N4R10_SUNJA`) |
| 6 | N6-R05 도면에 없는 방문 | `END_HARIN_DOOR_OPENED` | 404호 안의 문 |

- 데이터: `Scripts/Content/SeedContent.V51PoolsEven.cs` (42개 퀘스트, 증거 70종, 대화 2, 전화 1).
  `BuildV51Subquests()`가 1·3·5일차와 함께 묶는다.
- 규칙: `Scripts/Cases/SubquestRules.Even.cs` — `SubquestRules`의 partial. 감전, 연기, 변형,
  대화 결과, 다음 날 가중치.
- 현장 소품: `Scripts/Gameplay/WorldBuilder.EvenSubquests.cs` — 퀘스트가 열려 있을 때만 반응.
- 선택지 수치는 GDD 표 그대로다. 정답 선택지는 항상 증거를 요구한다(19.2).
- 랜덤 퀘스트는 엔딩 플래그를 쓰지 않는다(4.1.1). `NightPoolContentTests`가 여섯 밤 모두 강제한다.
- 대화로 결정되는 퀘스트(N2-R12, N4-R03, N4-R10)는 대화 선택이 플래그를 남기고, 보고서는
  그 선택에 맞는 한 줄과 "아무 말도 하지 않았다"는 한 줄을 보여준다. 전화를 놓쳐도 통화
  기록이 남아 보고할 수 있다.

### 추첨 쿼터 (4.3)

| 일자 | Normal | Mixed | Heavy | 후보 구성 |
|---|---|---|---|---|
| 2 | 1 | 1 | 1 | Normal 4, Mixed 2, Anomaly 7 |
| 4 | 0 | 1 | 2 | Normal 1, Mixed 1, Anomaly 11 |
| 6 | 0 | 0 | 3 | Normal 1, Final Pressure 12 |

Heavy는 Anomaly·Consequence·Final Pressure를 함께 센다(`TakeHeavyQuota`).

### 일자를 넘는 연결

| 원인 | 결과 |
|---|---|
| N1 `SUNJA_CARE` | N4-R01 가중치(+30, GOOD 아니면), 반응 변형 / N4-R10 증언 분량 |
| N4-R01 직접 방문 | N4-R10 전체 증언 가능 |
| CommunityTrust ≥ 60 | N4-R10 전체 증언 가능 |
| N4-R10 증언 | `SunjaTestimony` → N5-R13 무전 교차검증의 증인 |
| N2-R05 상자 처리 | `OLD_PACKAGE_STATUS` → N5-R05 가중치·변형 |
| N2-R08 검증 실험 | `FUTURE_RULE_CONFIRMED` → N6-R04 방송을 따라가도 안전 |
| N3-R14 녹음 보존 | `FUTURE_WARNING_404` → N6-R04 방송을 따라가도 안전 |
| N2-R14 반송 | N4-R12 가중치 +40 |
| N3-R10 기준표식 | N6-R03 통로의 기준표식 소품이 나타남 |
| N5-R01 엘리베이터 전력 차단 | N6-R14 엘리베이터 탈출이 멈춤 |
| N5-M01 무전 확인 | N6-R13 가중치 +30 |
| BuildingSafety < 60 또는 방화문 ≠ SAFE | N6-R08 연기 유입, 가중치 +40 |
| ChairmanAlert | N6-R02 / N6-R09 가중치(÷2), 50 이상이면 삭제·잠금이 실제로 진행 |
| DistortionDebt ≥ 2 | N6-R03 가중치 +30 |

### 위험 규칙

- **N2-R03** 급전선을 내리지 않고 분전반을 열면 HP -8(한 번). 내린 뒤에는 수리된다.
- **N4-R11** 벽을 긁으면 HP -2, SAN -5. 두 번째 아이의 윤곽과 "동생" 일부만 나온다.
- **N6-R06** 잔해를 직접 치우면 HP -8, 보강하면 게임 시간 10분.
- **N6-R08** 연기 속에서 5초마다 HP -2, 최대 -20. 방화문 걸쇠를 걸거나 환기가 맞으면 멈춘다.
  변형에 따라 환기는 역류(HP -6)한다.
- **N6-R09** 강제 개방 HP -5, B1 회로 차단은 게임 시간 5분.

## 2. 4일차 404 전화

v5.0에서 N4-M01에 걸려 있던 `CALL_N4_404`는 v5.1에서 N4-R03의 전화다. 이제 N4-R03이
열릴 때 울린다. 대화(`D_N4_CALL_404`)는 v5.1 선택지 넷(위치 / 이름 / 침묵 / 끊기)으로 바꿨다.
N4-R03이 뽑히지 않은 밤에는 404 전화가 오지 않는다. 메인은 시작할 때 스스로 `Knows404`를
켜므로 주민 DB의 404 행은 영향을 받지 않는다.

## 3. 검증

- EditMode `NightPoolContentTests` — 여섯 밤 모두 15개 구성, 쿼터, 엔딩 플래그는 메인과
  스토리만, 모든 퀘스트가 어떤 상태에서도 보고 가능. 모든 선택지가 조건부인 퀘스트는
  `EveryComplementarySetCoversEveryState`가 플래그 조합을 전부 돌려 증명한다.
- EditMode `EvenNightSubquestTests` — 차단기 감전, 2009 전화(받음/놓침), 404 전화가 N4-R03에
  울림, 선자 증언 분량, 방문 → 증언, 하린의 문, 연기 피해와 상한, 붕괴 지점, N2-R14 → N4-R12.
- PlayMode `SubquestWorldTests` — 146개 소품 모두 레이캐스트로 닿고 퀘스트 밖에서는 침묵.
- PlayMode `EvenNightSubquestWorldTests` — 짝수 날 소품 73개가 다른 상호작용 물체와 부피를
  공유하지 않음, 303호 노크 → 대화, 404 안의 문.
- PlayMode `CampaignCompletionTests.EveryWholeNightCanBeFinished` — 여섯 밤 모두 전체 추첨으로
  5~6개를 뽑고 닫는다.

## 4. 남은 것

- 6일차는 메인 보고서를 내는 순간 근무가 끝난다. 근무 순서상 메인 뒤에 놓인 랜덤 1~2개는
  나오지 않는다. 스토리 서브퀘스트는 메인보다 앞이라 항상 나온다.
- N6-R09 원격 잠금은 문을 실제로 잠그지 않는다. 잠긴 상태를 알리고 우회 방법을 고르게 한다.
  실제로 잠그면 탈출로가 막힐 수 있어 소프트락 위험이 크다.
- N6-R06 / N6-R07 / N6-R13은 메인의 시간과 단서에만 관여한다고 GDD가 말하지만, 지금은
  플래그만 남기고 메인의 유해·장부 회수 난이도는 바꾸지 않는다.
- 2·4·6일차도 v2.1 야간 대응 이벤트(M01~M18)가 돌지 않는다. 풀이 완성된 밤은 두 체계를
  겹치지 않는다(1·3·5일차와 같은 규칙).
- 대화와 이상현상 연출은 텍스트와 알림뿐이다. 거울 속 아이들, 화면 속 미래 같은 시각 효과는 없다.
