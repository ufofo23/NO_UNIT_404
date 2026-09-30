# 첫 방문자 3D 캐릭터

## 적용 대상

- 현재 코드에서 가장 먼저 호출되는 `vis_n0_guest_jiwoo` — **602호 방문객**.
- 최지우 본인이 아니라 최지우를 찾아온, 이름이 공개되지 않은 성인 남성 손님이다.
- `NO_UNIT_404_GDD_v5.0.md` 9.2에는 프롤로그의 정상 약 배송원이 먼저 등장한다. 현재 게임은 프롤로그 없이 Night 1에서 시작하며, `GameLoop.SeedScriptedCallers`는 `N1-M01` 시작에 602호 손님을 먼저 배치한다. 이번 작업은 현재 등장 순서에 맞춘 아트 적용이다.
- 외형은 사용자가 지정한 작업자 레퍼런스로 교체했다: 검은 모자, 갈색 작업복, 형광 안전조끼와 반사띠, 검은 카고 바지, 붉은 장갑, 검은 작업화. 방문자 ID와 사건 진행은 그대로 유지한다.

## 게임에서 확인

1. Unity 6000.3.2f1에서 Play → 새 게임.
2. 시작 무전 대화를 진행한다. 첫 메인 사건이 시작되면 602호 방문객이 호출된다.
3. 공동현관에서 직접 보거나 관리 PC의 인터폰 / CAM-01을 확인한다.
4. 출입을 승인하면 동일한 캐릭터가 로비에 나타나 기존 방문자 경로를 따라간다.

`dev.mainonly`를 켠 개발 세션에서는 기존 규칙에 따라 이 손님이 생략된다. 일반 새 게임의 기본값은 꺼짐이다. 테스트용으로 바꿨다면 `dev.mainonly off` 후 새 게임을 시작한다.

## 자산과 재생성

- 현재 원본: `Docs/Art/ReferenceGuest/ReferenceGuest_Working.blend`.
- 생성 스크립트: `Tools/PrepareReferenceGuest.py`.
- 이미지 기반 생성 원본: `Docs/Art/ReferenceGuest/base_basic_pbr.glb`.
- 게임 모델: `Assets/_Project/Resources/NO404/Characters/FirstGuest/FirstGuest.fbx`.
- 게임 프리팹: 같은 폴더의 `FirstGuestCharacter.prefab` 및 URP 머티리얼.
- Unity 메뉴: `Tools > NO404 > Art > Rebuild First Visitor`.

Blender를 백그라운드에서 `-b -P Tools/PrepareReferenceGuest.py`로 실행한 뒤 Unity 재생성 메뉴를 실행한다. 게임 실행에는 Blender가 필요하지 않다. Blender 원본은 Unity 자동 변환을 피하기 위해 Assets 밖에 둔다.

현재 모델은 23,332개 삼각형, 16개 뼈대, 정점당 최대 4개 뼈대 가중치의 스킨 메시다. 4K 색상 텍스처는 레퍼런스의 정면·후면 세부를 UV에 베이크하고, 가려진 옆면은 생성 모델의 텍스처를 유지한다. `FirstGuestMotion`은 게임 시간 배율에 맞춰 대기·팔·다리·무릎·발목을 구동한다. 방문자 판정·출입권한·경로 시뮬레이션은 기존 서비스를 그대로 읽는다.

이 폴더의 `FirstGuest.blend`와 기존 `Tools/GenerateFirstGuest.py`는 이전 도형 프로토타입 기록이다. 현재 모델의 재생성에 사용하지 않는다.

## 검증

`FirstGuestCharacterTests` PlayMode 테스트가 새 게임의 실제 첫 호출 순서, 캐릭터 프리팹 적용, 현관 카메라 시야, 입장 후 동일 모델, 일시정지와 걷기 관절 변화, 퇴장 후 제거를 확인한다. 테스트가 저장하는 `FirstGuest_InGame.png`, `FirstGuest_Interphone.png`는 Unity 게임 월드의 실제 렌더링이다. 현재 모델의 스튜디오 렌더는 `../ReferenceGuest/ReferenceGuest_Final.png`이며, `FirstGuest_Studio.png`는 이전 도형 프로토타입 이미지다.
