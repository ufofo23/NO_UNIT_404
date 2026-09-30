# 참고 이미지 기반 방문자

사용자가 제공한 `ChatGPT Image 2026년 9월 8일 오후 06_52_15.png`의 네 방향을 입력으로 [BlenderMCP](https://github.com/ahujasid/blender-mcp)의 공개 Hyper3D Rodin 체험 연동을 사용했다. 개인 유료 API 키는 사용하지 않았다.

## 제작 결과

- 원본 메시: `base_basic_pbr.glb`, 23,332 triangles.
- 편집 가능한 리깅 원본: `ReferenceGuest_Working.blend`.
- 형상 확인용 렌더: `ReferenceGuest_Final.png`.
- 게임용 FBX: `Assets/_Project/Resources/NO404/Characters/FirstGuest/FirstGuest.fbx`.
- 게임용 4K 텍스처: 같은 폴더의 `Worker_BaseColor.png`.
- 게임 프리팹: 같은 폴더의 `FirstGuestCharacter.prefab`.

3D 생성 결과에 레퍼런스 세부를 투영해 UV에 베이크했다. 측면 사진에서 손이 허벅지를 가리는 부분을 그대로 투영하면 바지에 장갑 무늬가 중복되므로, 옆면은 생성 모델의 가림 처리된 텍스처를 사용한다. 원본 얼굴과 옷에 맞춘 재구성이며, 원본 이미지와 모든 각도에서 픽셀 단위로 동일한 모델은 아니다.

16개 뼈대와 정점 가중치로 연속된 표면이 변형된다. 캡슐이나 분리된 원통 관절로 구성한 모델이 아니다. 걷기는 현재 게임의 이동 속도와 시간 배율을 사용하는 절차적 동작이다.

원본 glTF의 양면 재질을 Unity에서도 유지한다. 얼굴과 옷의 검은 삼각형 자국은 정면 Blender 렌더에서도 재현됐다. 투영 텍스처의 혼합 가중치를 부드럽게 조정하고, Unity 임포트 시 면적·각도 가중 법선을 재계산해 삼각형 경계를 해소했다. 현재 PC 렌더러의 SSAO를 끄면 CCTV 텍스처에 발생하던 검은 상단 띠와 이중 윤곽이 사라지는 것을 렌더 비교로 확인해 해당 효과를 비활성화했다.

## 재생성

2026-09-09 보행 수정: 이동 속도는 1.1 → 1.65m/s이며 발걸음 주기도 이동 속도에 비례한다. 신발 높이 0.16m 이하 정점은 발 뼈대 하나에 100% 연결하고, 0.16–0.24m의 바지 밑단에서만 정강이 가중치로 전환한다. `Blender -b -P Tools/FixReferenceGuestFeet.py`는 저장된 blend의 외형·텍스처를 유지하며 발 리깅만 수정한다. PlayMode 검증은 여러 보행 주기에서 양쪽 신발의 정점 간 거리 변화가 0.2mm 이내인지 확인한다.

`Blender -b -P Tools/PrepareReferenceGuest.py` → Unity `Tools > NO404 > Art > Rebuild First Visitor`.

이미 저장된 GLB로 작업하므로 재생성에 외부 API 요청이나 계정이 필요하지 않다. `ReferenceGuestRodin.py generate`는 원본 메시를 새로 생성할 때만 쓰며, 기존 작업이 기록돼 있으면 중복 요청을 차단한다.

Unity를 사용 중이면 `Tools/PrepareReferenceValidation.ps1`이 `Builds/ReferenceGuestValidationProject`에 검증용 사본을 만든다. 열려 있는 에디터의 씬이나 재생 상태를 조작하지 않고 별도 사본에서 테스트와 빌드를 수행할 수 있다.
