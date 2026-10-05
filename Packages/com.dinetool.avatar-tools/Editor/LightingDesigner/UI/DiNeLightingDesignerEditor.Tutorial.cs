#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public sealed partial class DiNeLightingDesignerEditor
{
    private DiNeGuidedTutorial tutorial;
    private DiNeTutorialStep[][] tutorialCourses;
    private int tutorialCourse;
    private static readonly string[] TutorialCourseIds = { "brightness", "additional", "advanced", "targets-groups", "menu-presets", "settings-presets" };
    private static readonly string[] TutorialCourseEn = { "Brightness", "Additional Controls", "Advanced Controls", "Targets and Groups", "Menu Presets", "Settings Presets" };
    private static readonly string[] TutorialCourseKo = { "밝기", "추가 제어", "고급 제어", "대상과 그룹", "메뉴 프리셋", "설정 프리셋" };
    private static readonly string[] TutorialCourseJa = { "明るさ", "追加制御", "詳細制御", "対象とグループ", "メニュープリセット", "設定プリセット" };
    private static readonly string[][] TutorialOverviews =
    {
        new[]
        {
            "Lighting Designer creates VRChat menu controls for your avatar's appearance on supported materials. This course sets up one brightness slider so you can make the avatar easier to see in different worlds, with a chosen range and initial value. Place the component inside the avatar and enable the control; the menu and animations are generated automatically in Play Mode and on upload without changing source material assets.",
            "Lighting Designer는 지원 마테리얼을 사용하는 아바타의 외형을 VRChat 메뉴에서 조절할 수 있게 만듭니다. 이 과정에서는 밝기 슬라이더 하나와 조절 범위·초기값을 설정해 월드마다 아바타를 보기 좋은 밝기로 맞출 수 있습니다. 아바타 안에 컴포넌트를 배치하고 제어를 켜면 Play Mode와 업로드 시 메뉴·애니메이션이 자동 생성되며 원본 마테리얼 에셋은 바뀌지 않습니다.",
            "Lighting Designerは、対応マテリアルを使うアバターの見た目をVRChatメニューで調整できるようにします。このコースでは1つの明るさスライダーと範囲・初期値を設定し、ワールドに合わせてアバターを見やすくできます。アバター内にコンポーネントを配置して制御を有効にすると、Play Modeとアップロード時にメニュー・アニメーションを自動生成し、元のマテリアルアセットは変更しません。"
        },
        new[]
        {
            "Add VRChat sliders for color temperature, saturation, and light coloration. For example, make an outfit warmer, soften vivid colors, or reduce a world's colored lighting on your avatar. Enable only the controls you need, set their initial positions, and choose whether VRChat remembers their last values.",
            "색온도·채도·조명 색의 영향을 조절하는 VRChat 슬라이더를 추가할 수 있습니다. 의상을 따뜻한 색감으로 바꾸거나 강한 채도를 낮추고, 월드의 유색 조명에 아바타가 물드는 정도를 줄일 때 사용하세요. 필요한 제어만 켜고 초기 위치와 마지막 값 저장 여부를 정합니다.",
            "色温度・彩度・照明色の影響を調整するVRChatスライダーを追加できます。服を暖かい色合いにしたり、強い彩度を抑えたり、ワールドの色付き照明の影響を減らすときに使います。必要な制御だけを有効にし、初期位置と前回の値を保存するかを設定してください。"
        },
        new[]
        {
            "Create finer VRChat controls for texture color, emission, shadows, outlines, reflection, and fixed light direction. For example, tone down bright emission, blend between two outline colors, or keep a consistent lighting direction. Enable the controls supported by your materials, set their starting values and limits, and check diagnostics when a material feature is not visible.",
            "텍스처 색·발광·그림자·아웃라인·반사와 라이트 방향 고정까지 VRChat에서 세밀하게 조절할 수 있습니다. 강한 발광을 낮추거나 두 아웃라인 색 사이를 바꾸고, 일정한 조명 방향을 유지할 때 사용하세요. 마테리얼에서 지원하는 제어를 켜고 초기값·범위를 정한 뒤, 효과가 보이지 않으면 진단에서 마테리얼의 기능 상태를 확인합니다.",
            "テクスチャの色・発光・影・アウトライン・反射・ライト方向固定をVRChatで細かく調整できます。強すぎる発光を抑える、2つのアウトライン色を切り替える、一定の照明方向を保つ、といった用途があります。マテリアルが対応する制御を有効にして初期値・範囲を決め、効果が見えない場合は診断で機能の状態を確認してください。"
        },
        new[]
        {
            "Choose which avatar parts Lighting Designer affects, and give selected parts their own controls. Exclusions can preserve an effect object's existing look; renderer groups can adjust clothing brightness separately from the body. Set the shader targets and renderers, then check parameter cost and diagnostics before adding more separate controls.",
            "Lighting Designer를 적용할 아바타 파츠를 고르고 특정 파츠에 별도 조절 메뉴를 만들 수 있습니다. 제외 목록으로 효과 오브젝트의 외형을 유지하거나, 렌더러 그룹으로 의상의 밝기를 몸과 따로 조절할 때 사용하세요. 대상 셰이더·렌더러를 지정한 뒤 별도 제어를 더 추가하기 전에 파라미터 용량과 진단을 확인합니다.",
            "Lighting Designerを適用するパーツを選び、特定のパーツに個別の調整メニューを作れます。除外リストでエフェクトの見た目を保ったり、レンダラーグループで服の明るさを体と別に調整したりできます。対象シェーダー・レンダラーを設定し、個別制御を増やす前にパラメーター容量と診断を確認してください。"
        },
        new[]
        {
            "Create VRChat buttons that apply a chosen combination of lighting values in one press, such as a photo look or a dim-world setting. Give each preset a name, include the enabled controls it should change, and set their values. Pressing the generated menu button updates those controls; controls omitted from the preset keep their current values.",
            "촬영용 색감이나 어두운 월드용 설정처럼 여러 라이팅 값을 한 번에 적용하는 VRChat 버튼을 만들 수 있습니다. 프리셋 이름을 정하고 변경할 활성 제어 항목만 포함해 값을 지정하세요. 생성된 메뉴 버튼을 누르면 포함한 항목이 함께 바뀌고, 포함하지 않은 항목은 현재 값을 유지합니다.",
            "撮影用の色合いや暗いワールド用の設定など、複数のライティング値を一度に適用するVRChatボタンを作れます。プリセットに名前を付け、変更する有効な制御だけを含めて値を設定してください。生成されたメニューボタンを押すと対象の値が一緒に変わり、含めなかった項目は現在の値を維持します。"
        },
        new[]
        {
            "Save a Lighting Designer configuration as an asset and reuse it on another avatar or restore a previous setup. Settings presets include enabled controls, ranges, starting values, and VRChat menu presets. Load one into the current component, save a new asset, or overwrite an existing preset; renderer groups and exclusions remain specific to each avatar.",
            "Lighting Designer 설정을 에셋으로 저장해 다른 아바타에서 재사용하거나 이전 설정으로 돌아갈 수 있습니다. 설정 프리셋에는 활성 제어·조절 범위·초기값·VRChat 메뉴 프리셋이 저장됩니다. 현재 컴포넌트에 불러오거나 새 에셋으로 저장하고 기존 프리셋을 덮어쓸 수 있으며, 렌더러 그룹과 제외 목록은 각 아바타에서 따로 관리합니다.",
            "Lighting Designerの設定をアセットとして保存し、他のアバターで再利用したり以前の設定に戻したりできます。設定プリセットには有効な制御・調整範囲・初期値・VRChatメニュープリセットが保存されます。現在のコンポーネントへ読み込む、新規保存する、既存プリセットを上書きすることができ、レンダラーグループと除外リストはアバターごとに管理します。"
        }
    };

    private void EnsureTutorial()
    {
        if (tutorial == null)
        {
            tutorial = new DiNeGuidedTutorial(this, "lighting-designer");
            tutorialCourse = Mathf.Clamp(SessionState.GetInt("DiNe.LightingDesigner.TutorialCourse." + target.GetInstanceID(), 0), 0, 5);
        }
        if (tutorialCourses != null) return;
        tutorialCourses = new DiNeTutorialStep[6][];
        tutorialCourses[0] = new[]
        {
            DiNeTutorialStep.Required("avatar", "Place this component on the avatar root or one of its child objects in the scene. Lighting Designer finds the VRCAvatarDescriptor above it and builds controls for that avatar.", "씬의 아바타 루트나 그 자식 오브젝트에 이 컴포넌트를 배치하세요. 상위 VRCAvatarDescriptor를 찾아 해당 아바타의 조절 메뉴를 만듭니다.", "シーン内のアバターのルートまたは子オブジェクトにこのコンポーネントを配置してください。上位のVRCAvatarDescriptorを探し、そのアバターの調整メニューを作ります。", HasTutorialAvatar),
            DiNeTutorialStep.Required("targets", "Use a supported material, such as lilToon or Poiyomi, on at least one avatar renderer. The status must show target renderers so there is a visible part for the generated controls to affect.", "아바타 렌더러 하나 이상에 lilToon·Poiyomi 같은 지원 마테리얼을 사용하세요. 생성된 제어가 적용될 파츠가 있어야 하므로 상태에 대상 렌더러가 표시되는지 확인합니다.", "アバターのレンダラーにlilToon・Poiyomiなどの対応マテリアルを使ってください。生成した制御を適用するパーツが必要なので、状態に対象レンダラーが表示されることを確認します。", HasTutorialTargets),
            DiNeTutorialStep.Required(ControlTutorialId(DiNeLightingControl.LightMin, "enabled"), "Enable Lighting Brightness to add one VRChat slider for the avatar's minimum and maximum lighting. You can use it in-world to raise or lower the supported materials' brightness together.", "조명 밝기를 켜면 아바타의 최소·최대 조명을 함께 조절하는 VRChat 슬라이더 하나가 추가됩니다. 월드 안에서 지원 마테리얼의 밝기를 한꺼번에 올리거나 내릴 수 있습니다.", "ライティング明るさをオンにすると、最小・最大ライティングをまとめて調整するVRChatスライダーが1つ追加されます。ワールド内で対応マテリアルの明るさを一緒に上げ下げできます。", () => _designer != null && _designer.IsEnabled(DiNeLightingControl.LightMin), onEnter: () => _settingsMode = 0),
            DiNeTutorialStep.Optional("max-light", "Maximum Brightness is the actual shader value used when the VRChat slider reaches 1. Set this upper limit to decide how bright the avatar can become.", "최대 밝기는 VRChat 슬라이더가 1일 때 적용되는 실제 셰이더 밝기입니다. 이 상한으로 아바타를 얼마나 밝게 만들 수 있을지 정하세요.", "最大明るさはVRChatスライダーが1のときに適用する実際のシェーダー値です。この上限でアバターをどこまで明るくできるか決めてください。", () => _settingsMode = 0),
            DiNeTutorialStep.Optional("min-light", "Minimum Brightness is the shader value used when the VRChat slider reaches 0. Set this lower limit to control how dark the avatar can become, keeping it no higher than the maximum.", "최소 밝기는 VRChat 슬라이더가 0일 때 적용되는 셰이더 밝기입니다. 최대 밝기 이하에서 하한을 정해 아바타가 얼마나 어두워질 수 있을지 조절하세요.", "最小明るさはVRChatスライダーが0のときのシェーダー値です。最大明るさ以下で下限を設定し、どこまで暗くできるか決めてください。", () => _settingsMode = 0),
            InitialStep(DiNeLightingControl.LightMin, 0), SavedStep(DiNeLightingControl.LightMin, 0),
            DiNeTutorialStep.Optional("master", "Enable the ON/OFF toggle to add a master switch for Lighting Designer in VRChat. This lets you switch between the configured lighting adjustments and their OFF state with one menu action.", "켜기/끄기 토글을 켜면 VRChat에 Lighting Designer 전체 스위치를 추가합니다. 메뉴 한 번으로 설정한 조절 효과와 OFF 상태를 전환할 수 있습니다.", "ON/OFFトグルを有効にすると、VRChatにLighting Designer全体のスイッチを追加します。1回の操作で設定した調整効果とOFF状態を切り替えられます。", () => _settingsMode = 0),
            DiNeTutorialStep.Optional("master-default", "Default On chooses whether the master switch starts enabled when there is no saved value. Turn it on if the avatar should begin with your Lighting Designer adjustments active.", "기본 켜짐은 저장된 값이 없을 때 전체 스위치가 켜진 상태로 시작할지 정합니다. 아바타를 처음 사용할 때부터 Lighting Designer 효과를 적용하려면 켜세요.", "デフォルトONは保存値がないとき、全体スイッチをオンで開始するかを決めます。初めからLighting Designerの効果を有効にしたい場合はオンにしてください。", () => _settingsMode = 0),
            DiNeTutorialStep.Optional("master-saved", "Saved lets VRChat remember the master switch's last state. Leave it off if you want the switch to return to Default On rather than reuse the previous choice.", "값 저장을 켜면 VRChat이 전체 스위치의 마지막 상태를 기억합니다. 이전 선택 대신 기본 켜짐 설정으로 시작하게 하려면 끄세요.", "値を保存をオンにすると、VRChatが全体スイッチの前回の状態を記憶します。前回の選択を使わず、デフォルトONの設定で開始したい場合はオフにしてください。", () => _settingsMode = 0),
            DiNeTutorialStep.Optional("automatic", "Enter Play Mode to check the generated setup, or upload the avatar when ready to use its VRChat menu. Generation runs automatically and non-destructively; setting these fields alone does not apply the runtime controls in Edit Mode.", "Play Mode에서 생성된 구성을 확인하거나 준비된 아바타를 업로드해 VRChat 메뉴에서 사용하세요. 생성은 자동으로 비파괴 적용되며, 설정 입력만으로 Edit Mode에서 실행용 제어가 적용되지는 않습니다.", "Play Modeで生成した構成を確認するか、準備できたアバターをアップロードしてVRChatメニューで使ってください。生成は自動・非破壊で行われ、設定を入力しただけではEdit Modeに実行用の制御は適用されません。")
        };
        tutorialCourses[1] = ControlCourse(SimpleAdditionalControls, 0);
        var advanced = new List<DiNeTutorialStep>(ControlCourse(AdvancedControls, 1));
        advanced.Add(DiNeTutorialStep.Optional("light-direction", "With Fixed Light Direction enabled, enter the direction vector used while its VRChat toggle is on. This can keep the avatar's shading direction consistent as you move between worlds.", "라이트 방향 고정을 켜고 VRChat 토글이 ON일 때 사용할 방향 벡터를 지정하세요. 월드를 옮겨도 아바타의 셰이딩 방향을 일정하게 유지할 수 있습니다.", "ライト方向固定を有効にし、VRChatのトグルがオンのときに使う方向ベクトルを指定してください。ワールドを移動してもアバターの陰影方向を一定に保てます。", () => _settingsMode = 1));
        advanced.Add(DiNeTutorialStep.Optional("outline-from", "Choose the outline color used at slider position 0. Together with Color at 1, it defines the two colors that the in-world slider blends between.", "슬라이더가 0일 때 사용할 아웃라인 색을 고르세요. 1일 때 색과 함께 VRChat 슬라이더로 보간할 두 끝 색을 정합니다.", "スライダーが0のときのアウトライン色を選んでください。1のときの色と組み合わせ、VRChatのスライダーで補間する両端の色を決めます。", () => _settingsMode = 1));
        advanced.Add(DiNeTutorialStep.Optional("outline-to", "Choose the outline color used at slider position 1. For example, set different dark and light colors at the two ends to change the outline's mood with one slider.", "슬라이더가 1일 때 사용할 아웃라인 색을 고르세요. 양 끝에 어두운 색과 밝은 색을 지정하면 슬라이더 하나로 아웃라인의 분위기를 바꿀 수 있습니다.", "スライダーが1のときのアウトライン色を選んでください。両端を暗い色と明るい色にすれば、1つのスライダーでアウトラインの雰囲気を変えられます。", () => _settingsMode = 1));
        advanced.Add(DiNeTutorialStep.Optional("outline-width", "Maximum Width sets the thickest outline the slider can produce. The material must already have its outline feature enabled for this control to be visible.", "최대 두께는 슬라이더로 만들 수 있는 가장 굵은 아웃라인을 정합니다. 제어가 눈에 보이려면 마테리얼의 아웃라인 기능이 켜져 있어야 합니다.", "最大幅はスライダーで作れる最も太いアウトラインを決めます。変化を表示するには、マテリアルのアウトライン機能が有効である必要があります。", () => _settingsMode = 1));
        advanced.Add(DiNeTutorialStep.Optional("reflectance-max", "Maximum Reflectance sets the upper strength of the reflection and gloss slider. Use a lower limit when you want a subtler sheen, and check that the material's reflection feature is enabled.", "최대 반사율은 반사·광택 슬라이더의 상한 강도를 정합니다. 은은한 광택을 원하면 상한을 낮추고 마테리얼의 반사 기능이 켜져 있는지 확인하세요.", "最大反射率は反射・光沢スライダーの強さの上限を決めます。控えめな光沢にしたい場合は上限を下げ、マテリアルの反射機能が有効か確認してください。", () => _settingsMode = 1));
        tutorialCourses[2] = advanced.ToArray();
        tutorialCourses[3] = new[]
        {
            DiNeTutorialStep.Optional("shaders", "Choose the shader families whose materials should receive the enabled controls. Excluding a family keeps those materials outside Lighting Designer's target selection.", "켜 둔 제어를 적용할 셰이더 계열을 선택하세요. 선택에서 뺀 계열의 마테리얼은 Lighting Designer 대상에 포함되지 않습니다.", "有効な制御を適用するシェーダー系統を選んでください。対象から外した系統のマテリアルはLighting Designerの処理対象に含まれません。", () => _settingsMode = 1),
            DiNeTutorialStep.Optional("excludes", "Add renderers that should keep their own appearance to Excluded Renderers. For example, exclude an effect mesh that should not follow the body's brightness or color controls.", "기존 외형을 유지할 렌더러를 제외 목록에 넣으세요. 몸의 밝기·색 조절을 따르지 않아야 하는 효과 메시 등을 제외할 수 있습니다.", "独自の見た目を保ちたいレンダラーを除外リストに追加してください。例えば、体の明るさや色の調整に連動させないエフェクトのメッシュを除外できます。", () => { _settingsMode = 1; _showExcludes = true; }),
            DiNeTutorialStep.Optional("group-add", "Press Add to collect a set of renderers under one group. A group lets you give parts such as clothing their own chosen controls while the rest of the avatar uses the main controls.", "추가를 눌러 렌더러들을 하나의 그룹으로 묶으세요. 의상 같은 파츠는 선택한 항목을 따로 조절하고 나머지 아바타는 전체 제어를 사용하게 할 수 있습니다.", "追加を押してレンダラーを1つのグループにまとめてください。服などのパーツには選んだ制御を個別に用意し、他のパーツには全体制御を使えます。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-name", "Name the group after its parts, such as Hair or Clothing. The generated VRChat submenu uses this name so you can identify what its controls affect.", "머리카락·의상처럼 대상 파츠를 알 수 있는 그룹 이름을 입력하세요. 생성되는 VRChat 하위 메뉴에 이 이름이 표시되어 조절 대상을 구분할 수 있습니다.", "髪・服など、対象パーツが分かるグループ名を入力してください。生成されるVRChatサブメニューに表示され、調整対象を見分けられます。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-icon", "Choose an icon if you want the group's VRChat submenu to be easier to recognize. Leaving it empty uses the tool's default group icon.", "그룹의 VRChat 하위 메뉴를 쉽게 알아보려면 아이콘을 고르세요. 비워 두면 툴의 기본 그룹 아이콘을 사용합니다.", "グループのVRChatサブメニューを見分けやすくしたい場合はアイコンを選んでください。空欄ならツールの標準グループアイコンを使います。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-renderers", "Assign the renderers for the body parts or clothing that belong to this group. The separate controls affect these renderers, so review the list before selecting which controls to split.", "이 그룹에 속할 몸 파츠나 의상의 렌더러를 지정하세요. 별도 제어는 이 렌더러들에 적용되므로 따로 조절할 항목을 고르기 전에 목록을 확인합니다.", "このグループに含める体のパーツや服のレンダラーを指定してください。個別制御はこれらに適用されるので、分ける制御を選ぶ前に一覧を確認します。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-separate", "Enable a control globally, then check it here to give this group a separate parameter and menu control. For example, split Lighting Brightness to adjust the outfit independently; unselected controls still follow the avatar's main values.", "전체에서 제어 항목을 켠 뒤 여기서 체크하면 그룹에 별도 파라미터와 메뉴 제어를 만듭니다. 조명 밝기를 선택해 의상만 따로 조절할 수 있으며, 선택하지 않은 항목은 아바타의 전체 값을 따릅니다.", "全体で制御を有効にしてからここでチェックすると、グループに個別のパラメーターとメニュー制御を作ります。明るさを選べば服だけを個別に調整でき、未選択の項目は全体の値に連動します。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-delete", "Delete removes the group's configuration and its separate controls. Its renderer objects stay in the avatar and use the main controls unless excluded elsewhere.", "삭제는 그룹 설정과 별도 제어를 제거합니다. 렌더러 오브젝트는 아바타에 남으며 다른 곳에서 제외하지 않았다면 전체 제어를 사용합니다.", "削除はグループ設定と個別制御を取り除きます。レンダラーのオブジェクトは残り、別途除外していなければ全体制御を使います。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("budget", "Check the synced parameter total before adding sliders or group controls. Each separate slider adds 8 bits and each toggle adds 1 bit, so keep the combined avatar usage within the displayed limit.", "슬라이더나 그룹 제어를 추가하기 전에 동기화 파라미터 합계를 확인하세요. 별도 슬라이더마다 8bit, 토글마다 1bit가 더해지므로 아바타 전체 사용량이 표시된 한도를 넘지 않게 조절합니다.", "スライダーやグループ制御を増やす前に同期パラメーターの合計を確認してください。個別スライダーは8bit、トグルは1bitを追加するので、アバター全体の使用量を表示された上限以内に収めます。", () => _settingsMode = 1),
            DiNeTutorialStep.Optional("diagnostics", "Open diagnostics to check missing targets, parameter limits, and material features needed by enabled controls. Resolve the messages before testing a control whose effect is absent or incomplete.", "진단을 열어 대상 누락·파라미터 한도·제어에 필요한 마테리얼 기능 상태를 확인하세요. 효과가 보이지 않거나 일부만 적용되는 제어를 테스트하기 전에 안내된 문제를 해결합니다.", "診断を開き、対象の不足・パラメーター上限・制御に必要なマテリアル機能を確認してください。効果が見えない、または一部しか反映されない制御を試す前に、表示された問題を解消します。", () => _showDiagnostics = true)
        };
        tutorialCourses[4] = new[]
        {
            DiNeTutorialStep.Optional("menu-add", "Press Add to create a VRChat button for a combination of control values. For example, make one photo preset that adjusts brightness and saturation together.", "추가를 눌러 여러 제어 값을 묶어 적용하는 VRChat 버튼을 만드세요. 밝기와 채도를 함께 바꾸는 촬영용 프리셋을 만들 수 있습니다.", "追加を押して、複数の制御値をまとめて適用するVRChatボタンを作ってください。例えば明るさと彩度を一緒に変える撮影用プリセットを作れます。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-name", "Enter the name shown on the preset's VRChat button. Use a purpose such as Photo or Dark World so the desired look is easy to choose in-world.", "프리셋의 VRChat 버튼에 표시할 이름을 입력하세요. 촬영·어두운 월드처럼 용도를 적으면 월드 안에서 원하는 설정을 고르기 쉽습니다.", "プリセットのVRChatボタンに表示する名前を入力してください。撮影・暗いワールドなど用途を名前にすると、ワールド内で設定を選びやすくなります。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-icon", "Choose an icon to help identify this preset button in VRChat. If left empty, the generated button uses the default preset icon.", "VRChat에서 프리셋 버튼을 구분하기 쉽게 아이콘을 고르세요. 비워 두면 생성되는 버튼에 기본 프리셋 아이콘을 사용합니다.", "VRChatでプリセットボタンを見分けやすくするアイコンを選んでください。空欄なら生成されるボタンに標準プリセットアイコンを使います。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-include", "Enable the needed controls, then check only those this preset should change. Omitted controls keep their current values when you press the preset button.", "필요한 제어를 먼저 켜고 이 프리셋으로 바꿀 항목만 체크하세요. 포함하지 않은 항목은 프리셋 버튼을 눌러도 현재 값을 유지합니다.", "必要な制御を有効にし、このプリセットで変える項目だけをチェックしてください。含めなかった項目は、ボタンを押しても現在の値を維持します。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-value", "Set the value each included control should receive when the button is pressed. Slider values use 0–1 positions and toggles use OFF/ON; separate groups receive the same preset value for that control.", "버튼을 눌렀을 때 포함한 제어에 적용할 값을 지정하세요. 슬라이더는 0–1 위치, 토글은 OFF/ON으로 정하며 같은 제어를 따로 쓰는 그룹에도 동일한 프리셋 값이 적용됩니다.", "ボタンを押したとき、対象の制御に適用する値を指定してください。スライダーは0–1の位置、トグルはOFF/ONで設定し、同じ制御を個別に使うグループにも同じ値を適用します。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-delete", "Delete removes this preset and its generated VRChat button. The individual lighting controls remain available, so you can still adjust their values manually.", "삭제는 이 프리셋과 생성될 VRChat 버튼을 제거합니다. 개별 라이팅 제어는 유지되므로 각 값을 수동으로 계속 조절할 수 있습니다.", "削除はこのプリセットと生成されるVRChatボタンを取り除きます。個別のライティング制御は残るため、各値を手動で調整できます。", OpenTutorialMenuPresets)
        };
        tutorialCourses[5] = new[]
        {
            DiNeTutorialStep.Optional("settings-select", "Choose a saved settings asset to reuse a Lighting Designer configuration. Selecting it prepares the source preset; press Load to copy its settings into this component.", "저장된 설정 에셋을 골라 Lighting Designer 구성을 재사용하세요. 선택만으로는 컴포넌트가 바뀌지 않으며 불러오기를 눌러 설정을 복사합니다.", "保存した設定アセットを選び、Lighting Designerの構成を再利用してください。選択だけではコンポーネントは変わらず、読み込みを押すと設定をコピーします。"),
            DiNeTutorialStep.Optional("settings-load", "Load replaces the component's portable settings, including controls, ranges, and menu presets, with the selected asset's settings. Its renderer groups and exclusions stay as configured for this avatar, and Undo can restore the previous settings.", "불러오기는 제어·범위·메뉴 프리셋 같은 공통 설정을 선택한 에셋의 내용으로 바꿉니다. 이 아바타의 렌더러 그룹·제외 목록은 유지되며 이전 설정은 Undo로 되돌릴 수 있습니다.", "読み込みは制御・範囲・メニュープリセットなど共通の設定を、選択アセットの内容に置き換えます。このアバターのレンダラーグループ・除外リストは維持され、前の設定はUndoで戻せます。"),
            DiNeTutorialStep.Optional("settings-save", "Save as New Preset writes the current reusable settings to a new asset at the path you choose. Use it to keep a baseline or share the same control setup across avatars; renderer groups and exclusions are not saved.", "새 프리셋으로 저장은 선택한 경로에 현재 공통 설정을 새 에셋으로 저장합니다. 기준 설정을 남기거나 여러 아바타에서 같은 제어 구성을 쓸 때 활용하며, 렌더러 그룹·제외 목록은 저장하지 않습니다.", "新規プリセットとして保存は、指定した場所に現在の共通設定を新しいアセットとして保存します。基準設定を残したり複数のアバターで同じ構成を使ったりできますが、レンダラーグループ・除外リストは保存しません。"),
            DiNeTutorialStep.Optional("settings-overwrite", "Overwrite with Current updates the selected preset asset with this component's reusable settings. Use it after improving a saved setup; future loads from that asset use the new settings.", "덮어쓰기는 선택한 프리셋 에셋을 이 컴포넌트의 현재 공통 설정으로 갱신합니다. 저장해 둔 구성을 개선한 뒤 사용하면 이후 이 에셋을 불러올 때 새 설정을 사용합니다.", "現在の設定で上書きは、選択プリセットをこのコンポーネントの共通設定で更新します。保存済みの構成を改善した後に使うと、次回このアセットを読み込む際は新しい設定になります。")
        };
    }

    private void DrawTutorialControls()
    {
        string[] names = CurrentLanguage == DiNeLightingLanguage.Korean ? TutorialCourseKo
            : CurrentLanguage == DiNeLightingLanguage.Japanese ? TutorialCourseJa : TutorialCourseEn;
        if (tutorial.IsExpanded)
        {
            int selected = EditorGUILayout.Popup(T("튜토리얼", "Tutorial", "チュートリアル"), tutorialCourse, names);
            if (selected != tutorialCourse)
            {
                tutorialCourse = selected;
                SessionState.SetInt("DiNe.LightingDesigner.TutorialCourse." + target.GetInstanceID(), selected);
            }
        }
        tutorial.Configure(TutorialCourseIds[tutorialCourse], TutorialCourseEn[tutorialCourse], TutorialCourseKo[tutorialCourse], TutorialCourseJa[tutorialCourse], tutorialCourses[tutorialCourse],
            overviewEn: TutorialOverviews[tutorialCourse][0], overviewKo: TutorialOverviews[tutorialCourse][1], overviewJa: TutorialOverviews[tutorialCourse][2]);
        tutorial.DrawControls();
    }

    private DiNeTutorialStep[] ControlCourse(DiNeLightingControl[] controls, int mode)
    {
        var steps = new List<DiNeTutorialStep>();
        foreach (var control in controls)
        {
            var names = TutorialControlNames(control);
            var purpose = TutorialControlPurpose(control);
            steps.Add(DiNeTutorialStep.Optional(ControlTutorialId(control, "enabled"),
                purpose[0] + " Enable " + names[0] + " to add its VRChat control.",
                purpose[1] + " " + names[1] + " 항목을 켜면 VRChat 조절 메뉴에 추가됩니다.",
                purpose[2] + " " + names[2] + "を有効にするとVRChatの調整メニューに追加されます。", () => _settingsMode = mode));
            steps.Add(InitialStep(control, mode));
            steps.Add(SavedStep(control, mode));
        }
        return steps.ToArray();
    }

    private DiNeTutorialStep InitialStep(DiNeLightingControl control, int mode)
    {
        var names = TutorialControlNames(control);
        string[] text;
        if (control == DiNeLightingControl.LightDirection)
            text = new[]
            {
                "Default On chooses whether Fixed Light Direction starts active when there is no saved value. Enable it to start with the direction specified below instead of the world's lighting direction.",
                "기본 켜짐은 저장된 값이 없을 때 라이트 방향 고정을 활성 상태로 시작할지 정합니다. 켜 두면 월드 조명 방향 대신 아래에서 지정한 방향으로 시작합니다.",
                "デフォルトONは保存値がないとき、ライト方向固定を有効な状態で開始するかを決めます。オンにすると、ワールドの照明方向ではなく下で指定した方向で開始します。"
            };
        else if (control == DiNeLightingControl.LightMin)
            text = new[]
            {
                "Choose the brightness slider's initial position from 0 to 1. Position 0 uses Minimum Brightness, 1 uses Maximum Brightness, and positions between them interpolate the two values.",
                "밝기 슬라이더의 초기 위치를 0–1에서 정하세요. 0은 최소 밝기, 1은 최대 밝기이며 그 사이는 두 값 사이를 보간합니다.",
                "明るさスライダーの初期位置を0–1で設定してください。0は最小明るさ、1は最大明るさで、その間は両方の値を補間します。"
            };
        else
        {
            bool midpointOriginal = control == DiNeLightingControl.ColorTemperature || control == DiNeLightingControl.Saturation
                || control == DiNeLightingControl.Brightness || control == DiNeLightingControl.Gamma;
            string en = midpointOriginal ? "0.5 preserves the original appearance for this control."
                : control == DiNeLightingControl.Hue ? "0 keeps the original hue; other positions rotate its colors."
                : "This is the starting value used when no saved value is available.";
            string ko = midpointOriginal ? "이 항목은 0.5에서 원래 외형을 유지합니다."
                : control == DiNeLightingControl.Hue ? "0은 원래 색조이며 다른 위치에서는 색이 회전합니다."
                : "저장된 값이 없을 때 이 초기값으로 시작합니다.";
            string ja = midpointOriginal ? "この制御は0.5で元の見た目を維持します。"
                : control == DiNeLightingControl.Hue ? "0は元の色相で、他の位置では色を回転します。"
                : "保存値がないときは、この初期値で開始します。";
            text = new[] { "Choose " + names[0] + "'s starting slider position from 0 to 1. " + en,
                names[1] + " 슬라이더의 초기 위치를 0–1에서 정하세요. " + ko,
                names[2] + "スライダーの初期位置を0–1で設定してください。" + ja };
        }
        return DiNeTutorialStep.Optional(ControlTutorialId(control, "initial"), text[0], text[1], text[2], () => _settingsMode = mode);
    }

    private DiNeTutorialStep SavedStep(DiNeLightingControl control, int mode)
    {
        var names = TutorialControlNames(control);
        return DiNeTutorialStep.Optional(ControlTutorialId(control, "saved"),
            "Saved lets VRChat remember the last " + names[0] + " value you chose. Turn it off if this control should start from its configured initial value each time instead.",
            "값 저장을 켜면 VRChat이 마지막으로 선택한 " + names[1] + " 값을 기억합니다. 매번 지정한 초기값으로 시작하려면 끄세요.",
            "値を保存をオンにすると、VRChatが前回選んだ" + names[2] + "の値を記憶します。毎回設定した初期値で開始する場合はオフにしてください。", () => _settingsMode = mode);
    }
    private static string ControlTutorialId(DiNeLightingControl control, string field) => "control-" + control + "-" + field;
    private void OpenTutorialGroups() { _settingsMode = 1; _showGroups = true; }
    private void OpenTutorialMenuPresets() { _settingsMode = 1; _showMenuPresets = true; }
    private bool HasTutorialAvatar() => _designer != null && !EditorUtility.IsPersistent(_designer)
        && _designer.gameObject.scene.IsValid() && _designer.GetComponentInParent<VRCAvatarDescriptor>(true) != null;
    private bool HasTutorialTargets() => HasTutorialAvatar() && DiNeLightingDiagnostics.CollectTargetRenderers(_designer.GetComponentInParent<VRCAvatarDescriptor>(true), _designer).Count > 0;

    private void TutorialProperty(SerializedProperty property, GUIContent label, string id, bool includeChildren = false)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, label, includeChildren);
        if (EditorGUI.EndChangeCheck()) tutorial.NotifyAction(id);
        tutorial.Draw(id, GUILayoutUtility.GetLastRect());
    }

    private void TutorialFallback(Rect rect, params string[] ids)
    {
        foreach (string id in ids) tutorial.Draw(id, rect);
    }

    private void TutorialControlFallback(DiNeLightingControl control, Rect rect)
    {
        TutorialFallback(rect, ControlTutorialId(control, "initial"), ControlTutorialId(control, "saved"));
        if (control == DiNeLightingControl.LightMin) TutorialFallback(rect, "max-light", "min-light");
        if (control == DiNeLightingControl.OutlineTint) TutorialFallback(rect, "outline-from", "outline-to");
        if (control == DiNeLightingControl.OutlineWidth) tutorial.Draw("outline-width", rect);
        if (control == DiNeLightingControl.Reflectance) tutorial.Draw("reflectance-max", rect);
        if (control == DiNeLightingControl.LightDirection) tutorial.Draw("light-direction", rect);
    }

    private static string[] TutorialControlNames(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.ColorTemperature: return new[] { "Color Temperature", "색온도", "色温度" };
            case DiNeLightingControl.Saturation: return new[] { "Saturation", "채도", "彩度" };
            case DiNeLightingControl.Monochrome: return new[] { "Monochrome", "흑백화", "モノクロ" };
            case DiNeLightingControl.Hue: return new[] { "Hue", "색조", "色相" };
            case DiNeLightingControl.Brightness: return new[] { "Brightness", "명도", "明度" };
            case DiNeLightingControl.Gamma: return new[] { "Gamma", "감마", "ガンマ" };
            case DiNeLightingControl.Emission: return new[] { "Emission Strength", "에미션 강도", "エミッション強度" };
            case DiNeLightingControl.LightDirection: return new[] { "Fixed Light Direction", "라이트 방향 고정", "ライト方向固定" };
            case DiNeLightingControl.ShadowStrength: return new[] { "Shadow Strength", "그림자 농도", "影の濃さ" };
            case DiNeLightingControl.OutlineTint: return new[] { "Outline Tint", "아웃라인 색", "アウトライン色" };
            case DiNeLightingControl.OutlineWidth: return new[] { "Outline Width", "아웃라인 두께", "アウトライン幅" };
            case DiNeLightingControl.Reflectance: return new[] { "Reflectance / Gloss", "반사/광택", "反射・光沢" };
            default: return new[] { "Lighting Brightness", "조명 밝기", "ライティング明るさ" };
        }
    }

    private static string[] TutorialControlPurpose(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.ColorTemperature: return new[] { "Color Temperature shifts the appearance cooler below 0.5 or warmer above it.", "색온도는 0.5보다 낮추면 차갑게, 높이면 따뜻한 색감으로 바꿉니다.", "色温度は0.5より下げると冷たく、上げると暖かい色合いに変えます。" };
            case DiNeLightingControl.Saturation: return new[] { "Saturation makes texture colors less vivid below 0.5 or more vivid above it.", "채도는 0.5보다 낮추면 텍스처 색을 탁하게, 높이면 선명하게 만듭니다.", "彩度は0.5より下げるとテクスチャの色を鈍く、上げると鮮やかにします。" };
            case DiNeLightingControl.Monochrome: return new[] { "Monochrome reduces colored lighting on the avatar, helping its colors stay less tinted by the world.", "흑백화는 아바타에 적용되는 조명 색 성분을 줄여 월드의 유색 조명에 덜 물들게 합니다.", "モノクロはアバターにかかる照明の色成分を減らし、ワールドの照明色の影響を抑えます。" };
            case DiNeLightingControl.Hue: return new[] { "Hue rotates the texture's colors around the color wheel, allowing an in-world color variation.", "색조는 텍스처 색을 색상환을 따라 회전시켜 월드 안에서 색감 변화를 줄 수 있습니다.", "色相はテクスチャの色を色相環に沿って回転させ、ワールド内で色の変化を付けられます。" };
            case DiNeLightingControl.Brightness: return new[] { "Brightness adjusts the texture's own lightness, with 0.5 preserving its original appearance.", "명도는 텍스처 자체의 밝기를 조절하며 0.5에서 원래 외형을 유지합니다.", "明度はテクスチャ自体の明るさを調整し、0.5で元の見た目を維持します。" };
            case DiNeLightingControl.Gamma: return new[] { "Gamma adjusts midtone brightness so you can change how dark or light texture details look.", "감마는 중간톤의 밝기를 조절해 텍스처의 세부 무늬가 밝거나 어둡게 보이는 정도를 바꿉니다.", "ガンマは中間調の明るさを調整し、テクスチャの細部の明暗を変えます。" };
            case DiNeLightingControl.Emission: return new[] { "Emission Strength adjusts the intensity of existing glow effects, such as luminous clothing accents.", "에미션 강도는 빛나는 의상 장식처럼 이미 설정된 발광 효과의 강도를 조절합니다.", "エミッション強度は光る服の装飾など、既に設定された発光効果の強さを調整します。" };
            case DiNeLightingControl.LightDirection: return new[] { "Fixed Light Direction adds a toggle that uses your chosen direction instead of the world's light direction.", "라이트 방향 고정은 월드 조명 대신 지정한 방향을 사용하는 토글을 만듭니다.", "ライト方向固定はワールドの照明方向の代わりに、指定した方向を使うトグルを作ります。" };
            case DiNeLightingControl.ShadowStrength: return new[] { "Shadow Strength adjusts how dark the avatar's shaded areas appear.", "그림자 농도는 아바타의 그늘진 부분이 얼마나 진하게 보일지 조절합니다.", "影の濃さはアバターの陰になった部分がどれほど暗く見えるかを調整します。" };
            case DiNeLightingControl.OutlineTint: return new[] { "Outline Tint blends between two chosen colors for an existing outline.", "아웃라인 색은 기존 아웃라인을 지정한 두 색 사이에서 보간합니다.", "アウトライン色は既存のアウトラインを、指定した2つの色の間で補間します。" };
            case DiNeLightingControl.OutlineWidth: return new[] { "Outline Width changes the thickness of an existing outline up to the maximum you set.", "아웃라인 두께는 설정한 최대값 안에서 기존 아웃라인의 굵기를 바꿉니다.", "アウトライン幅は設定した最大値まで、既存のアウトラインの太さを変えます。" };
            case DiNeLightingControl.Reflectance: return new[] { "Reflectance / Gloss adjusts an existing surface's reflection and sheen strength.", "반사/광택은 표면에 설정된 반사와 광택의 강도를 조절합니다.", "反射・光沢は表面に設定された反射と光沢の強さを調整します。" };
            default: return new[] { "Lighting Brightness moves minimum and maximum lighting together within your chosen range.", "조명 밝기는 지정한 범위에서 최소·최대 조명을 함께 움직입니다.", "ライティング明るさは設定した範囲内で最小・最大ライティングを一緒に変えます。" };
        }
    }
}

public sealed partial class DiNeLightingDesignerPresetEditor
{
    private DiNeGuidedTutorial presetTutorial;
    private DiNeTutorialStep[] presetTutorialSteps;
    private Texture2D presetBrandIcon;
    private GUIStyle presetTitleStyle, presetDescriptionStyle, presetSelectedStyle, presetNormalStyle;

    private void EnsurePresetTutorial()
    {
        if (presetTutorial == null) presetTutorial = new DiNeGuidedTutorial(this, "lighting-preset");
        if (presetTutorialSteps == null) presetTutorialSteps = new[]
        {
            DiNeTutorialStep.Optional("version", "Preset Version identifies the saved settings format. Check it when comparing presets or reporting a problem loading an older asset.", "프리셋 버전은 저장된 설정의 형식을 구분합니다. 프리셋을 비교하거나 예전 에셋을 불러오는 문제를 알릴 때 확인하세요.", "プリセットバージョンは保存設定の形式を示します。プリセットの比較や古いアセットの読み込み問題を報告する際に確認してください。"),
            DiNeTutorialStep.Optional("controls", "Enabled Controls counts the lighting items this asset will turn on when loaded. It helps you identify a small brightness setup versus a preset with more appearance controls.", "활성 제어 항목은 이 에셋을 불러올 때 켜질 라이팅 항목 수입니다. 간단한 밝기 구성인지 여러 외형 조절이 포함된 구성인지 구분할 수 있습니다.", "有効な制御項目は、このアセットを読み込んだときに有効になる項目数です。明るさだけの簡単な構成か、外見を細かく調整する構成かを見分けられます。"),
            DiNeTutorialStep.Optional("menu-presets", "VRChat Menu Presets counts the saved combinations that become in-world preset buttons. Loading the settings brings these combinations into the component along with its controls.", "VRChat 메뉴 프리셋은 월드 안의 프리셋 버튼이 될 값 조합의 수입니다. 설정을 불러오면 제어 구성과 함께 이 조합들도 컴포넌트에 복사됩니다.", "VRChatメニュープリセットは、ワールド内のプリセットボタンになる値の組み合わせの数です。読み込むと制御構成と共に、これらの組み合わせもコンポーネントにコピーされます。"),
            DiNeTutorialStep.Optional("load", "In the avatar's Lighting Designer component, select this asset under Settings Preset and press Load. This applies the reusable settings there; use Save as New Preset or Overwrite with Current in that component to save edits.", "아바타의 Lighting Designer 컴포넌트에서 설정 프리셋에 이 에셋을 선택하고 불러오기를 누르세요. 공통 설정이 해당 컴포넌트에 적용되며, 수정한 내용은 그곳의 새 프리셋으로 저장·덮어쓰기로 저장합니다.", "アバターのLighting Designerコンポーネントで、設定プリセットからこのアセットを選び読み込みを押してください。共通設定がそのコンポーネントに適用され、編集内容はそこで新規保存・上書きで保存できます。")
        };
        presetTutorial.Configure("inspect", "Lighting Preset", "라이팅 프리셋", "Lighting Preset", presetTutorialSteps,
            overviewEn: "This asset stores a reusable Lighting Designer configuration, including control settings and VRChat menu presets. Inspect the counts here, then select it in an avatar's Lighting Designer component and press Load to reuse the setup. Renderer groups and exclusions are kept on each avatar rather than in this preset.",
            overviewKo: "이 에셋은 제어 설정과 VRChat 메뉴 프리셋을 포함한 Lighting Designer 공통 구성을 저장합니다. 여기서 항목 수를 확인한 뒤 아바타의 Lighting Designer 컴포넌트에서 선택해 불러오면 같은 구성을 재사용할 수 있습니다. 렌더러 그룹·제외 목록은 프리셋 대신 각 아바타에서 관리합니다.",
            overviewJa: "このアセットは制御設定とVRChatメニュープリセットを含む、再利用可能なLighting Designerの構成を保存します。ここで項目数を確認し、アバターのLighting Designerコンポーネントで選んで読み込むと同じ構成を再利用できます。レンダラーグループ・除外リストはプリセットではなく各アバターで管理します。");
    }

    private void DrawPresetHeader()
    {
        if (presetTitleStyle == null)
        {
            presetBrandIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
            presetTitleStyle = new GUIStyle(EditorStyles.label)
            { font = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf"), fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            presetDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.8f, .8f, .8f) } };
            presetSelectedStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            presetNormalStyle = new GUIStyle(GUI.skin.button) { normal = { textColor = new Color(.8f, .8f, .8f) } };
        }
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = new Color(.9f, .9f, .9f);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(presetBrandIcon, GUILayout.Width(72f), GUILayout.Height(72f));
                GUILayout.Space(6f);
                GUILayout.Label("Lighting Preset", presetTitleStyle, GUILayout.Height(72f));
                GUILayout.FlexibleSpace();
            }
            GUILayout.Label(DiNeLightingLocalization.T("저장된 라이팅 설정을 확인합니다.", "Inspect saved lighting settings.", "保存済みライティング設定を確認します。"), presetDescriptionStyle);
        }
        GUI.backgroundColor = previous;
        GUILayout.Space(5f);
        int selected = (int)DiNeLightingLocalization.CurrentLanguage;
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < DiNeLightingLocalization.LanguageButtonLabels.Length; i++)
            {
                GUI.backgroundColor = selected == i ? new Color(.30f, .82f, .76f) : new Color(.5f, .5f, .5f);
                if (GUILayout.Button(DiNeLightingLocalization.LanguageButtonLabels[i], selected == i ? presetSelectedStyle : presetNormalStyle, GUILayout.Height(35f)))
                    DiNeLightingLocalization.CurrentLanguage = (DiNeLightingLanguage)i;
                GUI.backgroundColor = previous;
            }
        }
        GUILayout.Space(15f);
    }
}
#endif
