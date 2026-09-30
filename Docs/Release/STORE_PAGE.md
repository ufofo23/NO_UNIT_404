# 스토어 페이지 원고

Steamworks 파트너 사이트 > Store Presence 에 그대로 붙여 넣을 수 있게 쓴 원고다.
**게임에 실제로 있는 것만** 적는다 — 스토어 문구와 구현이 다르면 환불과 리뷰로 돌아온다.

작성 시점 기준: 빌드 0.9.0, 아트/오디오 패스 이전. 문구 자체는 아트 상태와 무관하다.

---

## 1. 기본 정보

| 항목 | 값 |
|---|---|
| 이름 (국문) | 404호는 없습니다 |
| 이름 (영문) | NO UNIT 404 |
| 개발/배급 | ProjectCaretaker |
| 플랫폼 | Windows 10/11 64-bit |
| 언어 | 한국어, 영어 (인터페이스·자막 모두) |
| 가격 | USD 8.99 / KRW 11,000 (GDD 1.6) |
| 첫 플레이 | 4~6시간 |
| 온라인 요소 | 없음 (1인용 오프라인) |
| 성인 콘텐츠 자기신고 | 해당 없음 |

---

## 2. 짧은 설명 (Short Description)

Steam 상한 **300자**. 검색 결과와 위시리스트에 이것만 보인다.

문 앞 판별이 첫 문장에 와야 한다. 검색 결과에서 이 게임이 무슨 게임인지 3초 안에
전달되는 것은 그것 하나뿐이고, 실제로 밤당 6~9번 일어난다(GDD 13.4).

### 한국어

> 인터폰이 울린다. 들여보낼 것인가. CCTV와 주민 등록부와 출입 기록으로 문 앞의 사람이
> 누구인지 판별하는 야간 관리원의 6일. 잘못 들이면 그 사람은 건물 안에 남는다.
> 그리고 이 건물에는 도면에 없는 404호가 있다.

### English

> The intercom rings. Do you let them in? Six nights as a caretaker, checking who is at the
> door against the cameras, the resident register and the access log. Let the wrong one in and
> they stay in the building. And this building has a unit 404 its plans do not.

---

## 3. 긴 설명 (About This Game)

### 한국어

**전임 관리원이 사라진 자리에 당신이 들어간다.**

해솔아파트 103동. 임시 야간 관리원 윤서우의 근무는 22:00에 시작해서 06:00에 끝난다.
관리실 PC에는 CCTV 12대, 주민 데이터베이스, 출입 기록, 계량기 그래프가 있다.
인터폰이 울리면 문 앞의 사람이 누구인지 정해야 한다.

**들여보낼 것인가.**

방문자는 신분을 말한다. 그 말이 사실인지는 당신이 확인한다. 주민 등록부에 그 이름이
있는가. 출입 기록의 시간이 그 사람의 동선과 맞는가. CCTV에 찍힌 것과 지금 하는 말이
같은가. **근거 두 개가 서로를 받쳐주기 전에는 어떤 판정도 확정되지 않는다.**

**404호는 없습니다.**

건물 도면에 404호는 없다. 4층 복도를 직접 걸어보면 403과 405 사이의 벽이 다른 층보다
넓다. 2009년에 이 건물에서 불이 났고, 그때 죽은 아이의 기록이 어디에도 남아 있지 않다.

**당신이 남기는 것이 기록이 된다.**

6일차에 보고서를 제출한다. 그때까지 모은 증거를 어떻게 연결했고 무엇을 적었는지에 따라
다섯 개의 결말 중 하나에 도달한다. 침묵하는 쪽을 고를 수도 있다. 그것도 기록이다.

---

**이 게임에 있는 것**

- **문 앞 판별** — 7일 근무 동안 **54명**이 인터폰을 누른다. 밤당 6~9명이고, 그중
  한둘은 들여보내면 안 되는 사람이다. 들여보내면 그 사람은 건물 안에 남는다
- **시설 관리 시뮬레이션** — 15개 사건과 18개 일반 업무. 순찰, 전력 배분, 민원 응대,
  방문자 판별이 전부 같은 8시간 안에서 경쟁한다
- **조사** — 증거 보드에서 단서를 직접 연결한다. 연결은 색이 아니라 관계로 구분된다
- **CCTV** — 이상현상 36종. 화면에 최소 4초 머무르고 60초까지 되감아 다시 볼 수 있다
- **다섯 개의 결말** — 엔딩 갤러리에 기록되며, 회차를 지워도 남는다

**이 게임에 없는 것**

- **전투가 없다.** 무기도, 적 체력도, 공격 수단도 설계 단계에서 제외했다
- **쫓기는 구간은 밤 전체에 두 번뿐이다.** 평범한 순찰 중에 무언가가 당신을 쫓아오는
  일은 일어나지 않는다. 이 건물이 무섭게 구는 방식은 그것이 아니다
- **갑자기 최대 음량으로 터지는 연출이 없다.** 가장 큰 소리도 환경음 대비 +8dB를
  넘지 못하도록 코드가 강제한다. 놀라게 하는 대신 **알아차리게 한다**
- **추가 결제가 없다.** 확률형 아이템, 인게임 재화, DLC 결제 요소 전부 없다

**붙잡혀도 죽지 않는다.** 증거 일부를 빼앗기고 근무 평가가 깎이고 시간을 잃는다.
한 번의 잘못된 판정이 진실에 이르는 길을 영구히 막는 일은 없다 — 대체 증거가 항상 있다.

---

**접근성**

- 자막 크기 90~160%, 화자 이름 표시, **환경음 자막** (`[엘리베이터가 위층에서 멈춘다]`)
- 색맹 모드 — 그래프에 기호, 증거 연결선에 굵기와 관계 문구
- 모션 감소 / 카메라 흔들림 0~100% / 밝기 보정 75~150%
- 시간 제한 선택지 해제, 추격 난이도 완화
- **밤을 느리게** — 압박 상승 0.5배. 증거와 업무 평가는 그대로 간다
- 퍼즐 힌트: 없음 / 지연 / 상시
- 스트리머 모드

---

### English

**You take the job the last caretaker left.**

Haesol Apartments, Block 103. Seo-woo Yoon's temporary night shift starts at 22:00 and ends
at 06:00. The office PC has twelve cameras, a resident database, an access log and the utility
meters. When the intercom rings, you decide who is at the door.

**Do you let them in?**

Visitors tell you who they are. Whether that is true is your job. Is the name in the resident
register? Does the access log put them where they say they were? Does the footage agree with
what they are telling you now? **No judgement is final until two independent facts back each
other up.**

**There is no unit 404.**

The plans do not have one. Walk the fourth-floor corridor and the wall between 403 and 405 is
wider than the same wall on every other floor. There was a fire here in 2009, and the child who
died in it is not in any record.

**What you write down becomes the record.**

On the sixth night you file your report. How you connected the evidence, and what you chose to
put in writing, decides which of five endings you reach. You can choose to stay silent. That is
also a record.

---

**What's in it**

- **The door** — **fifty-four callers** across the seven shifts, six to nine a night, one or
  two of whom should not be let in. Let one in and they stay in the building
- **Facility management sim** — fifteen cases and eighteen routine duties, all competing for
  the same eight hours: patrols, the power budget, complaints, and the door
- **Investigation** — link clues yourself on the evidence board; links read as relationships,
  not colours
- **CCTV** — thirty-six anomaly types. Each holds on screen for at least four seconds, and you
  can rewind sixty seconds to check again
- **Five endings** — recorded in a gallery that survives deleting a playthrough

**What's not**

- **No combat.** No weapons, no enemy health, no way to attack — excluded at the design stage
- **You are chased exactly twice in the whole game.** Nothing stalks you on an ordinary patrol.
  That is not how this building frightens you
- **No sudden full-volume scares.** The loudest sound in the game cannot exceed ambient by more
  than 8dB, and the code enforces it. It does not startle you; it lets you notice
- **No further purchases.** No loot boxes, no in-game currency, no paid unlocks

**Being caught is not death.** You lose a piece of evidence, some of your performance rating,
and ten minutes. A single wrong call can never permanently close off the truth — there is
always another route to it.

---

**Accessibility**

- Subtitles at 90–160%, speaker names, and **ambient captions** (`[the lift stops on a floor above]`)
- Colour-blind mode — glyphs on the graphs, thickness and written relations on evidence links
- Reduce motion / camera shake 0–100% / brightness 75–150%
- Remove choice timers, easier pursuit
- **Slower nights** — pressure builds at half rate. Evidence and performance are untouched
- Puzzle hints: off / delayed / always
- Streamer mode

---

## 4. 태그 (우선순위 — GDD 30.2)

순서가 중요하다. Steam은 앞쪽 태그를 더 강하게 쓴다.

1. Psychological Horror
2. Investigation
3. Mystery
4. Management
5. Singleplayer
6. Atmospheric
7. Story Rich
8. First-Person
9. Detective
10. Choices Matter
11. Multiple Endings
12. Horror
13. Simulation
14. Dark
15. Korean (한국어 지원 노출)

**넣지 말 것**: Survival Horror, Action, Combat 계열. 전투가 없으므로 기대를 어긋나게
만들고 그대로 부정 리뷰가 된다.

---

## 5. 시스템 요구사항

GDD 20.2의 최소 사양을 따른다. **실측 전에는 게시하지 않는다** —
`Tools ▸ NO404 ▸ Release ▸ Measure Performance`를 실제 최소 사양 기기에서 돌린 값으로
채운다. 추정치를 올리면 환불 사유가 된다.

| | 최소 | 권장 |
|---|---|---|
| OS | Windows 10 64-bit | Windows 11 64-bit |
| 저장공간 | (빌드 실측) | (빌드 실측) |
| 기타 | (측정 후 기입) | (측정 후 기입) |

품질 프리셋은 낮음/보통/높음 3단계이며, **낮음이 최소 사양에서 목표 프레임을 내는
기준선**이다.

---

## 6. 게시 전 확인

- [ ] 짧은 설명이 300자 이내인가 (한/영 각각)
- [ ] 스크린샷이 **그레이박스가 아닌가** — 5장 이상 1920×1080
- [ ] 캡슐 6종이 전부 올라갔는가 (`ART_BRIEF.md` 3.1)
- [ ] Small capsule 231×87에서 한글 제목이 읽히는가
- [ ] 트레일러가 오디오 패스 **이후**에 만들어졌는가
- [ ] 등급 표시가 확정되어 반영되었는가 (`RATING_GRAC.md`)
- [ ] 태그에 Action/Combat 계열이 없는가
- [ ] 시스템 요구사항이 추정이 아니라 실측인가
