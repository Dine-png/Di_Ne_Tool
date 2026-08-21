# 🎭 Di Ne Tool — VRChat Avatar Editor Suite

VRChat 아바타의 편집, 메뉴 제작, 라이팅, 최적화, 아이콘·스크린샷 제작, VRM 변환 준비를 한곳에서 처리하는 **Unity Editor 도구 모음**입니다.

![Unity 2022.3](https://img.shields.io/badge/Unity-2022.3_LTS%2B-black?logo=unity&style=flat)
![Version 1.6.3](https://img.shields.io/badge/Version-1.6.3-30D1C2?style=flat)
![Languages](https://img.shields.io/badge/UI-EN%20%2F%20KO%20%2F%20JP-4C9BD6?style=flat)

> 대부분의 도구는 English / 한국어 / 日本語 UI를 지원합니다. 기능에 따라 lilToon, Poiyomi, NDMF, Modular Avatar, UniVRM을 함께 사용할 수 있습니다.

## 📥 설치

[![Add to VCC](https://img.shields.io/badge/Add_to_VCC-005AF0?style=for-the-badge&logo=vrchat&logoColor=white)](https://dine-png.github.io/Di-Ne-Tool-Page/)

1. 위 버튼을 눌러 Di Ne Tool 저장소를 VCC에 추가합니다.
2. VCC에서 사용할 프로젝트의 **Manage Project**를 엽니다.
3. 목록에서 **Di Ne Tool**을 찾아 추가합니다.
4. Unity를 열고 상단의 `DiNe` 메뉴가 나타나는지 확인합니다.

저장소를 직접 등록하려면 VCC의 **Settings → Packages → Add Repository**에 아래 주소를 입력하세요.

```text
https://dine-png.github.io/Di_Ne_Tool/index.json
```

### 요구 사항

- Unity 2022.3 LTS 이상
- VRChat Avatars SDK 3.5.0 이상
- NDMF 1.8 이상
- Modular Avatar 1.15.1 이상
- lilToon / Poiyomi: 해당 셰이더용 기능을 사용할 때 필요
- UniVRM: Extra Modifier의 SpringBone 변환, MToon 변환, VRM 내보내기 연동에 필요

VCC / VPM으로 설치하면 package dependency에 등록된 VRChat SDK, NDMF, Modular Avatar가 함께 해결됩니다.

## 🚀 어디서 여나요?

| 도구 | 여는 위치 | 용도 |
|---|---|---|
| Avi Editor | `DiNe → Avi Editor` | 뼈, 애니메이션, 표정, 쉐이프키, PhysBone 일괄 편집 |
| Material Tool | `DiNe → Material Tool` | 프리셋, 미사용 텍스처 정리, VRAM 최적화 |
| Screen Saver | `DiNe → Screen Saver` | 스크린샷 및 메뉴 아이콘 제작 |
| Multi Dresser | Hierarchy 우클릭 → `Di Ne → Multi Dresser` | 옷장·헤어·액세서리 메뉴 생성 |
| Lighting Designer | Hierarchy 우클릭 → `Di Ne → Lighting Designer` | 인게임 라이팅 메뉴 생성 |
| Smart Toggle | 대상 오브젝트 우클릭 → `Di Ne → Smart Toggle` | 오브젝트 하나를 빠르게 토글 메뉴로 등록 |
| Smart Icon | 오브젝트 선택 후 우클릭 → `Di Ne → Smart Icon` | 선택한 오브젝트의 아이콘을 즉시 생성 |
| Opticore | 아바타 루트에 `Add Component → Di Ne → Opticore` | 빌드 시 비파괴 아바타 최적화 |
| Remove Mesh In Box | Renderer 오브젝트에 `Add Component → DiNe → Remove Mesh In Box` | 박스 안/밖 폴리곤을 빌드 시 제거 |
| Package Patcher | `DiNe → EX → Package Patcher` | 여러 Unity 패키지 일괄 설치·정리 |
| Asset Cleaner | `DiNe → EX → Asset Cleaner` | 선택한 씬에서 쓰지 않는 에셋 찾기 |
| Extra Modifier | `DiNe → EX → Extra Modifier` | VRChat 아바타를 VRM용으로 정리 |

---

## 🧍 Avi Editor

아바타 루트를 한 번 지정한 뒤 다섯 개의 탭에서 작업합니다. 탭을 바꾸거나 Undo/Redo를 사용해도 대상과 슬라이더 상태를 최대한 유지합니다.

### 1. 아마추어

- **직접 뼈 조정**: 바디 맵에서 부위를 고르고 균일/축별 Scale, Position, Rotation을 조절합니다.
- 머리, 목, 상체, 척추, 골반, 어깨, 팔·다리뿐 아니라 가슴·엉덩이 보조 본도 다룰 수 있습니다.
- Scale / Rotation / Position을 함께 또는 따로 프리셋으로 저장하고 불러올 수 있습니다.
- **MA 비율 조정**: Modular Avatar Scale Adjuster 방식으로 비율을 만들고 전용 프리셋을 저장·설치합니다.
- 대상 구조가 바뀌었다면 아바타 입력칸 옆의 `↺` 버튼으로 본 매핑을 다시 읽습니다.

### 2. 애니메이션

1. 아바타 루트와 Animation Clip을 지정합니다.
2. 시간 슬라이더를 움직여 포즈와 쉐이프키를 실시간 미리보기 합니다.
3. **쉐이프키만 적용**, **포즈만 적용**, **전체 적용** 중 원하는 버튼을 누릅니다.
4. 되돌리려면 **원본으로 초기화 (T-Pose/0)** 를 누릅니다.

슬라이더 이동은 미리보기이며, 적용 버튼을 눌러야 씬 오브젝트에 기록됩니다.

### 3. 표정

- 얼굴 프리뷰를 보면서 Body의 쉐이프키 값을 조절합니다.
- 기존 표정 Animation Clip을 불러와 수정하거나 새 클립으로 저장할 수 있습니다.
- **Gesture ShapeKey를 0으로 포함**하면 다른 제스처 클립에서 쓰는 쉐이프키가 섞여 표정이 깨지는 문제를 줄일 수 있습니다.
- FX Animator Controller의 레이어와 State를 확인하고 표정 클립을 바로 교체할 수 있습니다.

### 4. 쉐이프키

- 여러 쉐이프키를 0~200% 비율로 섞어 **새 쉐이프키**를 만듭니다.
- 기존 쉐이프키의 최대 변형량을 0~200%로 다시 조절합니다.
- 기존 쉐이프키의 내용을 다른 쉐이프키 조합으로 교체합니다.
- 검색, 얼굴 미리보기, 원본 복원을 지원합니다.

### 5. 엑스트라

아바타 아래의 비활성 오브젝트까지 포함해 모든 PhysBone을 찾고 상호작용 설정을 한 번에 바꿉니다.

- **잡기**: 플레이어가 PhysBone을 잡아 움직일 수 있는지 일괄 설정
- **포즈 고정**: 잡은 PhysBone을 원하는 자세에 고정할 수 있는지 일괄 설정
- **플레이어 콜라이더 반응**: 손을 포함한 글로벌 플레이어 콜라이더에 반응할지 일괄 설정
- 각 항목에서 켜짐 / 꺼짐 / 개별 설정 / 미지원 개수를 바로 확인
- **모두 켜기 / 모두 끄기**와 Undo 지원

플레이어 콜라이더 반응을 꺼도 각 PhysBone의 **Colliders** 목록에 직접 넣은 콜라이더는 제거하거나 비활성화하지 않습니다.

---

## 🎨 Material Tool

먼저 **대상 오브젝트**, **자식 포함**, **비활성 오브젝트 포함** 여부를 정합니다. 실제 변경 전에는 상단의 **미리보기 모드**로 대상을 확인하는 것을 권장합니다.

### 프리셋 적용

1. 프로젝트를 스캔해 lilToon 프리셋 라이브러리를 불러옵니다.
2. Skin / Hair / Cloth / Nature / Inorganic / Effect / Other 카테고리에서 프리셋을 고릅니다.
3. 대상의 lilToon 머티리얼을 스캔하고 적용할 항목만 체크합니다.
4. **프리셋 적용**을 누릅니다. 머티리얼 변경은 Undo를 지원합니다.

### 다이어트

lilToon과 Poiyomi 머티리얼에서 선택한 기능의 텍스처 슬롯을 찾아 정리합니다.

- Shadow / AO
- Shadow Mask
- Outline
- Normal Map
- MatCap
- Rim Light
- Emission
- Glitter
- Backlight
- Parallax
- Dissolve

**텍스처만 제거**는 슬롯만 비우고, **제거 + 기능 끄기**는 지원되는 셰이더 토글까지 함께 끕니다. 각 머티리얼 카드에서 실제 제거될 텍스처와 현재 기능 상태를 확인할 수 있습니다.

### VRAM 최적화

- 대상이 사용하는 텍스처의 포맷, 해상도, mipmap을 기준으로 예상 VRAM을 계산합니다.
- 텍스처별 권장 압축 포맷과 최대 해상도, 예상 절감량을 표시합니다.
- 여러 텍스처를 체크해 압축 방식과 최대 해상도를 한 번에 바꿀 수 있습니다.
- **전체 최적화**로 권장 변경을 일괄 적용할 수 있습니다.
- 텍스처 썸네일을 더블 클릭하면 확대, 1:1, 화면 맞춤을 지원하는 프리뷰가 열립니다.

> Texture Import Settings 변경은 Undo로 되돌릴 수 없습니다. 적용 전 버전 관리 상태를 확인하세요.

---

## 📸 Screen Saver

이름은 Screen Saver이지만 현재 중심 기능은 **스크린샷**과 **VRChat 메뉴 아이콘 제작**입니다.

### 스크린샷

1. **Screenshot** 탭에서 Game View 또는 Scene View를 선택합니다.
2. FHD / QHD / UHD / Custom 해상도와 16:9 / 9:16 / 3:4 / 1:1 비율을 고릅니다.
3. 배경을 Skybox / 단색 / 투명 중에서 선택합니다.
4. Game View는 내장 프리뷰에서 드래그로 회전·이동하고 스크롤로 전후 이동합니다.
5. `F` 또는 **선택 오브젝트 포커스**로 구도를 맞추고, 필요하면 **카메라에 적용**합니다.
6. **캡처**를 누르면 `Assets/Di Ne/ScreenShot`에 저장됩니다.

프리뷰 카메라는 가상이므로 구도를 움직여도 원본 카메라는 바로 바뀌지 않습니다. 실제 카메라를 옮길 때만 **카메라에 적용**을 누르세요.

### 아이콘

- 대상 오브젝트를 지정하고 프리뷰에서 좌클릭 드래그로 회전, 우클릭 드래그로 이동, 스크롤로 확대/축소합니다.
- 앞/뒤/좌/우/위/아래 방향 프리셋과 줌 프리셋을 사용할 수 있습니다.
- 휴머노이드 아바타에는 VRChat `proxy_idle` 포즈를 적용할 수 있습니다.
- 외곽선 색·두께와 금지 표시 오버레이의 크기·투명도·앞뒤 배치를 조절합니다.
- 기존 아이콘 덮어쓰기 또는 복사본 생성을 선택할 수 있습니다.
- 결과는 공용 폴더 `Assets/Di Ne/Icons`에 저장됩니다.

Multi Dresser와 Smart Toggle의 **아이콘 편집** 버튼도 같은 편집기를 사용합니다.

---

## 👗 Multi Dresser

옷, 헤어, 액세서리를 카테고리별 메뉴로 묶고 각 버튼 상태를 미리 확인하는 도구입니다.

1. 아바타 루트를 우클릭하고 `Di Ne → Multi Dresser`를 선택합니다.
2. 생성된 **Multi Dresser** 오브젝트에서 아바타 루트를 지정합니다.
3. `↺` 버튼으로 아바타의 FX Controller와 Expression Menu를 다시 연결합니다.
4. 필요한 경우 표정을 바꿀 Body Mesh를 **Shape Key Targets**에 넣습니다.
5. `+` 버튼으로 옷장 카테고리를 만들고 이름과 카테고리 아이콘을 정합니다.
6. 각 메뉴 버튼에 주 대상 오브젝트를 드래그합니다.
7. 필요에 따라 연결 오브젝트, 쉐이프키 값, 머티리얼 교체, 파티클 오브젝트를 설정합니다.
8. 각 상태의 **미리보기**로 오브젝트 활성화, 쉐이프키, 머티리얼을 확인합니다.
9. **아이콘 편집**에서 카메라 구도와 효과를 조절합니다.

설정은 Play Mode와 업로드용 임시 아바타에 자동 생성·적용됩니다. 원본 아바타의 FX Controller, 메뉴, 파라미터는 작업 종료 후 복원되며 원본 에셋을 직접 덮어쓰지 않습니다.

---

## 💡 Lighting Designer

lilToon과 Poiyomi 아바타의 조명과 색감을 VRChat 메뉴에서 조절하도록 만들어 줍니다.

1. 아바타 루트를 우클릭하고 `Di Ne → Lighting Designer`를 선택합니다.
2. **Simple**에서 켜기/끄기 토글과 조명 밝기 범위를 설정합니다.
3. 필요한 추가 제어만 켜고 초기값과 Saved 여부를 정합니다.
4. 더 세밀한 설정은 **Advanced**에서 대상 셰이더, 제외 렌더러, 메뉴 프리셋, 렌더러 그룹을 설정합니다.
5. 하단 상태에서 대상 렌더러 수, 파라미터 비용, 경고를 확인합니다.

### 사용할 수 있는 제어

- 조명 밝기: 메뉴 슬라이더의 0과 1에 대응하는 실제 최소·최대 밝기를 지정
- 색온도, 채도, 흑백화
- 색조, 명도, 감마
- 에미션 강도
- 라이트 방향 고정
- 그림자 농도
- 아웃라인 색·두께
- 반사·광택

### 고급 기능

- **설정 프리셋**: 현재 설정을 프로젝트 에셋으로 저장하고 다른 아바타에 재사용
- **VRChat 메뉴 프리셋**: 여러 슬라이더 값을 버튼 한 번에 적용하며 추가 동기화 비트는 사용하지 않음
- **렌더러 그룹**: 특정 의상이나 파츠만 별도 슬라이더로 제어
- **제외 목록**: 특정 Renderer를 처리 대상에서 제외
- **파라미터 예산 표시**: 현재 아바타의 사용량과 Lighting Designer 추가 비용을 함께 표시

Play Mode와 업로드 시 임시 머티리얼과 FX/Menu 에셋을 만들어 비파괴적으로 적용합니다. 색 보정에 텍스처 준비가 필요한 경우에도 복제본만 처리하며 원본 텍스처와 머티리얼은 유지됩니다.

> 같은 아바타에서 Light Limit Changer와 함께 사용하면 서로 같은 셰이더 값을 제어할 수 있으므로 경고를 확인하세요.

---

## 🎚️ Smart Toggle

오브젝트 하나를 빠르게 VRChat 토글로 만들 때 사용합니다.

1. 켜고 끌 오브젝트를 우클릭하고 `Di Ne → Smart Toggle`을 선택합니다.
2. 메뉴 이름과 Bool 파라미터 이름을 확인합니다.
3. 기본 ON 여부와 값 저장 여부를 정합니다.
4. 토글을 Expression Menu 최상단에 둘지, 별도 서브메뉴에 묶을지 선택합니다.
5. 자동 아이콘을 사용하거나 **아이콘 편집**에서 직접 구도를 조절합니다.

Play Mode와 업로드 시 FX Layer, Bool Parameter, Expression Menu 항목이 임시 아바타에 자동 생성됩니다. 같은 그룹에 항목이 많으면 다음 페이지도 자동으로 만듭니다.

### Smart Icon 빠른 생성

Hierarchy에서 하나 이상의 오브젝트를 선택한 뒤 다음 메뉴를 사용할 수 있습니다.

- `Di Ne → Smart Icon → Create Separate Icons`: 선택한 오브젝트마다 256px 아이콘 생성
- `Di Ne → Smart Icon → Create Combined Icon`: 선택한 오브젝트들을 한 장의 256px 아이콘으로 생성

---

## ⚡ Opticore

아바타 루트에 컴포넌트 하나를 추가하면 Play Mode 임시 클론과 NDMF 업로드 빌드에서 최적화를 자동 적용합니다. 원본 씬 아바타와 원본 에셋은 유지됩니다.

### 현재 자동 적용되는 항목

- 안전한 BlendShape 프리즈와 0 크기 폴리곤 정리
- 깨진 Renderer 정리
- 같은 스켈레톤을 공유하는 호환 Skinned Mesh 병합
- 빈/무효 SubMesh, 중복 Material Slot, 미사용 Material Property 정리
- 참조되지 않는 leaf bone과 안전한 중간 bone 정리
- null·중복 PhysBone Collider / Ignore 참조 정리
- 중복 Collider 통합과 보수적인 endpoint 보정
- Missing Script와 안전한 빈 Hierarchy 정리

**Preserve Avatar Behavior**는 애니메이션되거나 참조 중인 Transform을 보호합니다. **Experimental Mode**는 BlendShape가 있는 Skinned Mesh 병합과 더 공격적인 정리를 허용하므로 업로드 전에 충분히 테스트하세요.

> Animator 최적화 항목은 UI에 표시되지만 현재 자동 패스에는 포함되지 않습니다. Opticore 자동 빌드에는 NDMF가 필요합니다.

### Remove Mesh In Box

1. `SkinnedMeshRenderer` 또는 `MeshRenderer`가 있는 오브젝트에 컴포넌트를 추가합니다.
2. Scene View 핸들이나 Inspector의 Center / Size / Rotation으로 박스를 배치합니다.
3. 박스 **안쪽 제거** 또는 모든 박스의 **바깥쪽 제거**를 선택합니다.
4. 박스가 더 필요하면 **Add Box**를 누릅니다.

실제 폴리곤 제거는 Play Mode와 NDMF 빌드의 복제 메시에만 적용됩니다.

---

## 📦 Package Patcher

여러 `.unitypackage`를 순서대로 설치하고, 가져온 루트 폴더를 지정한 한 폴더 아래로 정리합니다.

1. 대상 폴더 이름을 정합니다. 기본값은 `_1_Patch`입니다.
2. `.unitypackage`, `.zip`, `.rar`, `.7z` 파일 또는 파일이 들어 있는 폴더를 드래그합니다.
3. 압축 파일 안에서 발견된 `.unitypackage` 중 설치할 항목만 체크합니다.
4. **Start Import Selected**를 누릅니다.
5. 컴파일과 도메인 리로드가 발생해도 큐가 이어서 처리될 때까지 기다립니다.

- ZIP은 바로 읽을 수 있습니다.
- RAR / 7Z는 7-Zip, WinRAR 또는 Bandizip CLI가 필요합니다.
- 한글·일본어·특수문자가 포함된 경로는 안전한 임시 경로를 거쳐 가져옵니다.
- 같은 이름의 폴더나 파일이 충돌하면 GUID를 비교해 참조를 보존하며 병합하거나 별도 이름으로 분리합니다.
- 새로 가져온 최종 폴더에는 Project 창에 **NEW** 배지가 표시됩니다.
- 일반 가져오기가 실패할 때만 **Force Import Dialog**를 켜서 다시 시도하세요.

---

## 🧹 Asset Cleaner

선택한 씬들이 참조하지 않는 프로젝트 에셋을 폴더 트리로 보여 주고, 선택한 항목을 OS 휴지통으로 이동합니다.

1. 보존 기준으로 사용할 씬을 모두 체크합니다.
2. **정리 대상 종류·보호 설정**에서 검사할 파일 종류를 고릅니다.
3. 문자열 로딩이나 외부 시스템이 사용하는 라이브러리는 **보호 폴더**에 추가합니다.
4. **분석 — 미사용 에셋 찾기**를 누릅니다.
5. 결과의 경로와 용량을 확인하고 삭제할 항목만 체크합니다.
6. 하단 삭제 버튼을 누르면 OS 휴지통으로 이동합니다.

코드, 씬, DLL, asmdef, meta 파일과 Resources, StreamingAssets, Addressables, Gizmos 등 특수 폴더는 자동 보호됩니다. Preset/Data와 Other 유형도 기본적으로 삭제 후보에서 빠집니다.

> “미사용”은 **선택한 씬에서 직렬화 참조가 발견되지 않았다**는 뜻입니다. 선택하지 않은 씬, 문자열 경로, 외부 로더에서만 쓰는 에셋은 직접 보호해야 합니다.

---

## 🧩 Extra Modifier — VRM 준비

현재 Extra Modifier는 VRChat 아바타를 UniVRM으로 넘기기 전에 정리하는 작업 흐름을 제공합니다.

### 가장 안전한 흐름

1. **VRM 대상**에 아바타를 지정합니다.
2. PhysBone 처리 방식을 **변환 / 삭제 / 유지** 중에서 고릅니다.
3. MToon으로 옮길 Normal Map, MatCap, Emission, 그림자 색, Outline, Rim Light를 선택합니다.
4. 필요하면 자동 처리에 머티리얼 변환과 UniVRM T-Pose 고정을 포함합니다.
5. **복사본에 전체 자동 처리**를 누릅니다.
6. **사전 점검 실행**으로 Humanoid, Root Transform, Missing Script, PhysBone, 비호환 컴포넌트·셰이더를 확인합니다.
7. UniVRM의 Freeze T-Pose, MeshUtility, VRM 0.x / 1.0 Exporter로 이어서 작업합니다.

전체 자동 처리는 작업 복사본을 만든 뒤 다음을 순서대로 수행합니다.

- 이름이 같은 의상·헤어 본을 대응하는 아바타 본 아래로 병합
- VRC PhysBone과 Collider를 UniVRM 0.x SpringBone으로 변환하거나 선택에 따라 삭제/유지
- 가능한 정적 VRC Constraint를 Unity Constraint로 변환
- Modular Avatar, NDMF, VRC 등 VRM 비호환 MonoBehaviour 정리
- lilToon, Poiyomi, Standard 계열 머티리얼을 MToon으로 변환
- UniVRM 도구로 넘기기 전 문제 사전 점검

> 각 단계의 개별 버튼은 지정한 오브젝트를 직접 수정합니다. 원본을 보존하려면 **복사본에 전체 자동 처리**를 사용하세요. 캡슐형 PhysBone Collider는 여러 Sphere로 근사되며, VRM 0.x에 없는 Plane Collider는 건너뜁니다.

---

## ✨ 작은 편의 기능

Hierarchy의 각 GameObject 오른쪽에는 `● / ○` 활성화 버튼이 표시됩니다.

- 클릭: 해당 오브젝트의 Active 상태 전환
- 여러 오브젝트를 선택한 뒤 `Shift + 클릭`: 선택된 오브젝트를 한 번에 전환
- 모든 변경은 Undo를 지원

---

## ❓ 자주 묻는 문제

### `DiNe` 메뉴가 보이지 않아요

- Console의 컴파일 에러를 먼저 확인합니다.
- VCC에서 VRChat SDK, NDMF, Modular Avatar dependency가 설치되었는지 확인합니다.
- 패키지를 다시 불러오거나 Unity를 재시작합니다.

### Play Mode나 업로드 후 FX/Menu가 바뀐 채로 남았어요

- Multi Dresser의 아바타 재배정 `↺` 버튼을 눌러 임시 세션 복원을 시도합니다.
- Play Mode를 완전히 종료한 뒤 컴파일이 끝날 때까지 기다립니다.
- Console의 `[DiNe]` 복원 로그와 오류를 확인합니다.

### Lighting Designer가 일부 머티리얼에 적용되지 않아요

- 대상 셰이더가 lilToon 또는 Poiyomi인지 확인합니다.
- Target Shaders와 제외 Renderer 목록을 확인합니다.
- 잠긴 Poiyomi 머티리얼이나 지원되지 않는 프로퍼티는 하단 진단 경고를 확인합니다.

### Package Patcher에서 RAR / 7Z가 열리지 않아요

- 7-Zip, WinRAR 또는 Bandizip을 설치한 뒤 Unity를 다시 엽니다.
- 압축 안에 실제 `.unitypackage` 파일이 있는지 확인합니다.

### Asset Cleaner가 필요한 파일을 미사용으로 표시해요

- 그 파일을 사용하는 모든 씬을 선택했는지 확인합니다.
- 문자열로 불러오는 파일이나 재사용 라이브러리 폴더를 보호 목록에 추가합니다.
- 삭제 전 결과 경로를 확인하고, 실수했다면 OS 휴지통에서 복원합니다.

---

## 🔗 링크

- [VCC 설치 페이지](https://dine-png.github.io/Di-Ne-Tool-Page/)
- [GitHub Releases](https://github.com/Dine-png/Di_Ne_Tool/releases)
- [버그 제보 및 기능 제안](https://github.com/Dine-png/Di_Ne_Tool/issues)
- [BOOTH](https://booth.pm/en/items/8179911)

---

**Made with ❤️ for avatar creators**

_Last updated: 2026-08-22_
