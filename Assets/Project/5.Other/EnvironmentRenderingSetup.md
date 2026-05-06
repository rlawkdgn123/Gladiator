# 환경 렌더링 정리

이 문서는 콜로세움/터레인 화면의 렌더링 관련 파일 위치와 조절 방법을 정리한 문서입니다.

## 한 곳에서 조절하기

주요 값은 씬의 `Global Volume (Realistic)` 오브젝트에 붙어 있는 `EnvironmentRenderingTuner` 컴포넌트에서 조절합니다.

위치:

- 씬: `Assets/Project/0.Scenes/BattleField.unity`
- 오브젝트: `Global Volume (Realistic)`
- 컴포넌트: `EnvironmentRenderingTuner`

`EnvironmentRenderingTuner`는 아래 값을 인스펙터에서 바로 바꿀 수 있게 직렬화해 둔 조절용 컴포넌트입니다.

- Terrain Hex 블렌딩
- Global Volume 포스트프로세싱
- 화면 상단 빛 퍼짐 오버레이

값을 바꾸면 에디터에서도 `OnValidate`로 바로 적용됩니다. 적용이 안 보이면 컴포넌트 우측 메뉴에서 `Apply Environment Rendering Settings`를 실행하면 됩니다.

## Terrain

현재 Terrain은 Unity 샘플 `TileBreakupHex`를 쓰지 않고 직접 만든 `TerrainHex` 머티리얼을 사용합니다.

사용 중인 씬:

- `Assets/Project/0.Scenes/BattleField.unity`
- `Assets/Project/0.Scenes/GSTestScene.unity`

사용 중인 머티리얼:

- `Assets/Project/4.Resources/Terrain/TerrainHex.mat`

셰이더 파일:

- `Assets/Project/4.Resources/Terrain/Shader/TerrainHex.shader`
- `Assets/Project/4.Resources/Terrain/Shader/TerrainHexInput.hlsl`
- `Assets/Project/4.Resources/Terrain/Shader/TerrainHexPasses.hlsl`
- `Assets/Project/4.Resources/Terrain/Shader/TerrainHexDepthNormalsPass.hlsl`

Terrain 데이터:

- Terrain Layer: `Assets/Project/4.Resources/Terrain/Layers`
- Terrain Data: `Assets/Project/4.Resources/Terrain/Textures/Terrain/Terrain.asset`

인스펙터 조절 위치:

- `Global Volume (Realistic)` > `EnvironmentRenderingTuner` > `Terrain Hex`

주요 값:

- `Enable Height Blend`: 텍스처 레이어 경계를 높이 기반으로 섞을지 여부입니다.
- `Height Transition`: 레이어 경계가 섞이는 폭입니다. 높일수록 경계가 부드러워집니다.
- `Hex Scale`: 육각 분할 패턴의 크기입니다.
- `Hex Contrast`: 셀 경계 선명도입니다. 낮을수록 덜 딱딱합니다.
- `Hex Rotation Strength`: 샘플링 회전 강도입니다. 반복 패턴을 줄이는 데 씁니다.

현재 기본값:

- `Enable Height Blend`: 켬
- `Height Transition`: `0.35`
- `Hex Scale`: `0.5`
- `Hex Contrast`: `1.2`
- `Hex Rotation Strength`: `0.25`

## 제거한 샘플 파일

이제 Terrain이 샘플 셰이더를 쓰지 않으므로 아래 항목을 정리했습니다.

- `Assets/Samples`
- `Assets/Samples.meta`
- `Assets/Project/4.Resources/Terrain/Textures/Terrain/TileBreakupHex.mat`
- `Assets/Project/4.Resources/Terrain/Textures/Terrain/TileBreakupHex.shadergraph`

## Post Processing

사용 중인 Volume:

- 씬 오브젝트: `Global Volume (Realistic)`
- 프로필: `Assets/Project/5.Other/Settings/RealisticClearProfile.asset`

인스펙터 조절 위치:

- `Global Volume (Realistic)` > `EnvironmentRenderingTuner` > `Volume Profile`
- `Bloom`
- `Color Adjustments`
- `White Balance`
- `Vignette`
- `Lens and Film`
- `Shadows / Midtones / Highlights`

주요 조절법:

- 화면 전체가 너무 밝으면 `Post Exposure`를 낮춥니다.
- 대비가 너무 강하면 `Contrast`를 낮춥니다.
- 색이 너무 진하면 `Saturation`을 낮춥니다.
- 햇빛 느낌을 더 주고 싶으면 `Temperature`를 조금 올립니다.
- 가장자리 어두움이 과하면 `Vignette Intensity`를 낮춥니다.
- 빛 번짐이 부족하면 `Bloom Intensity`를 올립니다.
- 빛 번짐 범위를 넓히고 싶으면 `Bloom Scatter`를 올립니다.
- 렌즈 색수차가 거슬리면 `Chromatic Aberration`을 낮춥니다.
- 그림자를 더 차갑게 보이게 하려면 `Shadows`의 B 값을 올리거나 W 값을 낮춥니다.
- 하이라이트를 더 따뜻하게 하려면 `Highlights`의 R/G 값을 조금 올리고 B 값을 조금 낮춥니다.

`Depth Of Field`는 게임 화면 가독성을 위해 기본값이 꺼져 있습니다.

## 화면 상단 빛 퍼짐

상단에서 빛이 퍼져 내려오는 효과는 `ScreenTopLightShaftOverlay`로 처리합니다.

스크립트:

- `Assets/Project/1.Scripts/VFX/Runtime/ScreenTopLightShaftOverlay.cs`

붙어 있는 오브젝트:

- `Main Camera`

하지만 실제 조절은 `Global Volume (Realistic)`의 `EnvironmentRenderingTuner`에서 하는 것을 권장합니다.

인스펙터 조절 위치:

- `Global Volume (Realistic)` > `EnvironmentRenderingTuner` > `Screen Top Light`

주요 값:

- `Screen Light Color`: 빛 색입니다. 노란색/주황색이면 햇빛 느낌이 강해집니다.
- `Screen Light Intensity`: 빛 전체 강도입니다.
- `Screen Light Vertical Reach`: 빛이 아래로 내려오는 거리입니다.
- `Screen Light Horizontal Spread`: 빛이 좌우로 퍼지는 폭입니다.
- `Screen Light Falloff`: 아래로 내려갈수록 사라지는 속도입니다.
- `Screen Light Source`: 화면 기준 빛 시작 위치입니다. `(0, 0)`은 좌하단, `(1, 1)`은 우상단입니다.
- `Screen Light Tilt`: 빛줄기 기울기입니다.
- `Screen Light Shaft Width`: 중앙 빛줄기 두께입니다.
- `Screen Light Top Glow`: 화면 상단 근처의 밝은 띠 강도입니다.

현재 기본값은 화면 상단 중앙 근처에서 따뜻한 빛이 아래로 살짝 퍼지는 느낌입니다.

두 번째 스크린샷처럼 상단 중앙에 얇은 밝은 빛을 더 만들고 싶으면:

- `Screen Light Source`의 Y를 `0.93 ~ 0.98` 사이로 둡니다.
- `Screen Light Intensity`를 `0.25 ~ 0.45` 사이에서 올립니다.
- `Screen Light Vertical Reach`를 `0.25 ~ 0.45` 사이로 조절합니다.
- `Screen Light Horizontal Spread`를 `0.6 ~ 0.9` 사이로 조절합니다.
- 색이 너무 노랗다면 `Screen Light Color`의 B 값을 조금 올립니다.
