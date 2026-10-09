# Scenario — 서사 정본

이 폴더 바로 아래에 있는 파일이 **현재 기준**이고, 이전 버전은 `archive/` 에 둔다.
(아직 보관본이 없어서 `archive/` 폴더는 첫 교체 때 만든다.)

| 버전 | 파일 | 상태 |
|---|---|---|
| **v2.0** | [NO_UNIT_404_SCENARIO_BOOK_v2_0.md](NO_UNIT_404_SCENARIO_BOOK_v2_0.md) | **현재 파일** |

GDD v5.1 은 1.1절에 시나리오 **v2.1** 을 통째로 싣고 그것을 정본으로 삼는다. v2.1 은
별도 파일로 들어온 적이 없으므로, 이 파일(v2.0)과 GDD v5.1 의 1.1절이 다르면 GDD 쪽이 맞다.
알려진 차이는 4F 서비스 통로 벽의 키 표시(이름 → 호칭)다.

## 새 버전을 올릴 때

1. 지금 파일을 `git mv` 로 `archive/` 에 옮긴다.
2. 새 파일을 이 폴더에 `NO_UNIT_404_SCENARIO_BOOK_v<버전>.md` 이름으로 넣는다.
3. 위 표를 고치고, `Docs/ENDING_PATH_v1_1.md` 와 `Docs/ONBOARDING.md` 가 가리키는 파일명을 같은 커밋에서 고친다.

## 관련 문서

- 설계 문서: [../GDD/](../GDD/)
- 엔딩 경로: [../ENDING_PATH_v1_1.md](../ENDING_PATH_v1_1.md)
