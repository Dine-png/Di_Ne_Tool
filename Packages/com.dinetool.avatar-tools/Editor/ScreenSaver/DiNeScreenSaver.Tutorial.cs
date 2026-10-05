using UnityEditor;
using UnityEngine;

namespace DiNeScreenSaver
{
    public partial class DiNeScreenSaver
    {
        private DiNeGuidedTutorial _tutorial;
        private DiNeTutorialStep[][] _tutorialCourses;

        private void BeginTutorialFrame()
        {
            if (_tutorial == null) _tutorial = new DiNeGuidedTutorial(this, "screen-saver");
            if (_tutorialCourses == null)
            {
                _tutorialCourses = new[]
                {
                    new[]
                    {
                        DiNeTutorialStep.Optional("capture-target", "Game View captures a chosen camera, while Scene View captures the editor's current viewpoint. Choose Game View or Scene View to capture.", "게임 뷰는 지정한 카메라를, 씬 뷰는 에디터의 현재 시점을 이미지로 저장합니다. 캡처할 게임 뷰·씬 뷰를 선택하세요.", "Game Viewは指定したカメラ、Scene Viewはエディターの現在の視点を画像に保存します。 撮影するGame View・Scene Viewを選んでください。"),
                        DiNeTutorialStep.Required("camera", "The capture source determines the scene framing you will save as a screenshot. For Game View, assign a camera. For Scene View, open the Scene window.", "캡처 원본은 스크린샷으로 저장할 씬의 구도를 정합니다. 게임 뷰에서는 카메라를 지정하세요. 씬 뷰에서는 씬 창을 여세요.", "撮影元はスクリーンショットに保存するシーンの構図を決めます。 Game Viewではカメラを指定し、Scene ViewではSceneウィンドウを開いてください。", () => _captureTarget == CaptureTarget.GameView ? _camera != null : SceneView.lastActiveSceneView != null),
                        DiNeTutorialStep.Optional("resolution", "Output size sets the saved image's pixel dimensions rather than the size of the tool window. Choose FHD, QHD, UHD or Custom for the output size.", "출력 크기는 도구 창 크기가 아니라 저장할 이미지의 픽셀 크기를 정합니다. 출력 크기를 FHD·QHD·UHD·Custom에서 선택하세요.", "出力サイズはツールウィンドウではなく、保存する画像のピクセル数を決めます。 出力サイズをFHD・QHD・UHD・Customから選んでください。"),
                        DiNeTutorialStep.Optional("aspect", "Aspect ratio changes the framing for a wide banner, square image or portrait screenshot. With a resolution preset, choose the aspect ratio.", "화면 비율은 가로 배너·정사각형 이미지·세로 스크린샷에 맞게 구도를 바꿉니다. 해상도 프리셋을 선택했다면 화면 비율을 고르세요.", "縦横比は横長のバナー・正方形の画像・縦長のスクリーンショットに合わせて構図を変えます。 解像度プリセットを選んだら縦横比を選んでください。"),
                        DiNeTutorialStep.Optional("custom-size", "Custom size is useful when a portfolio or image slot needs exact pixel dimensions. Choose Custom, then enter the output width and height.", "직접 크기는 포트폴리오나 이미지 표시 영역에 정확한 픽셀 크기가 필요할 때 사용합니다. Custom을 선택하고 출력 너비·높이를 입력하세요.", "カスタムサイズはポートフォリオや画像枠で正確なピクセル数が必要な場合に使います。 Customを選び、出力の幅・高さを入力してください。"),
                        DiNeTutorialStep.Optional("background", "A transparent PNG is useful for compositing, while a color background provides a solid backdrop. Choose Transparent or Color for the background.", "투명 PNG는 다른 이미지와 합성할 때 사용하고 컬러 배경은 단색 바탕을 만듭니다. 배경을 투명·컬러 중에서 선택하세요.", "透明PNGはほかの画像との合成に使え、カラー背景は単色の背景を作ります。 背景を透明・カラーから選んでください。"),
                        DiNeTutorialStep.Optional("background-color", "The background color appears behind the captured scene, for example to improve contrast around an avatar. For a color background, choose the background color here.", "배경색은 캡처한 씬 뒤에 표시되어 아바타와 배경의 대비 등을 조절할 수 있습니다. 컬러 배경을 선택했다면 배경색을 고르세요.", "背景色は撮影したシーンの後ろに表示され、アバターとのコントラストなどを調整できます。 カラー背景を選んだら背景色を選んでください。"),
                        DiNeTutorialStep.Optional("game-fov", "Field of view changes perspective framing, letting you fit more of the scene or use a tighter composition. For a perspective camera, adjust the preview's field of view.", "시야각은 원근 구도를 바꿔 더 넓은 씬을 담거나 좁은 구도로 촬영하게 합니다. 원근 카메라에서는 미리보기의 시야각을 조절하세요.", "視野角は透視の構図を変え、広い範囲を写したり狭い構図で撮影したりできます。 透視カメラではプレビューの視野角を調整してください。"),
                        DiNeTutorialStep.Optional("game-rotate", "Preview rotation changes the shot's viewing angle so you can compose it before capture. In the Game View preview, drag with the left mouse button to rotate.", "미리보기 회전은 촬영 시점을 바꿔 캡처 전에 구도를 맞추게 합니다. 게임 뷰 미리보기에서 왼쪽 버튼으로 드래그해 회전하세요.", "プレビューの回転は撮影角度を変え、撮影前に構図を調整できるようにします。 Game Viewのプレビューを左ドラッグして回転してください。"),
                        DiNeTutorialStep.Optional("game-pan", "Panning shifts the preview framing, useful for placing the avatar to one side of a banner. Right-drag or middle-drag in the preview to move the view.", "시점 이동은 미리보기 구도를 옮겨 배너 한쪽에 아바타를 배치할 때 등에 사용합니다. 미리보기에서 오른쪽·가운데 버튼으로 드래그해 시점을 이동하세요.", "視点移動はプレビューの構図をずらし、バナーの片側にアバターを配置する場合などに使います。 プレビューを右・中ドラッグして視点を移動してください。"),
                        DiNeTutorialStep.Optional("game-zoom", "Zoom changes how much of the subject fits in the screenshot preview. Scroll the mouse wheel in the preview to zoom.", "확대·축소는 스크린샷 미리보기에서 피사체가 차지하는 크기를 바꿉니다. 미리보기에서 마우스 휠로 확대·축소하세요.", "ズームはスクリーンショットのプレビューで被写体が占める大きさを変えます。 プレビュー内でマウスホイールを回してズームしてください。"),
                        DiNeTutorialStep.Optional("game-reset", "Reset discards preview framing changes and matches the selected camera again. Use Reset to return the preview to the camera's current view.", "초기화는 미리보기 구도 변경을 되돌려 선택한 카메라의 시점에 다시 맞춥니다. 초기화를 눌러 카메라의 현재 시점으로 돌아가세요.", "リセットはプレビューの構図変更を取り消し、選択したカメラの視点へ戻します。 リセットでカメラの現在の視点に戻してください。"),
                        DiNeTutorialStep.Optional("game-focus", "Focus frames the selected object, making it easier to compose a close-up of an avatar or prop. Select an object, then click Focus or press F in the preview.", "포커스는 선택한 오브젝트에 구도를 맞춰 아바타·소품의 근접 촬영을 쉽게 합니다. 오브젝트를 선택하고 포커스를 누르거나 미리보기에서 F를 누르세요.", "フォーカスは選択オブジェクトに構図を合わせ、アバターや小物の近接撮影をしやすくします。 オブジェクトを選び、フォーカスを押すかプレビュー内でFを押してください。"),
                        DiNeTutorialStep.Optional("game-apply", "This writes the preview pose and applicable view settings to the scene camera, useful when you want to keep the shot. Apply to Camera saves this preview view to the selected camera.", "촬영 구도를 유지하고 싶을 때 미리보기의 위치·회전과 적용 가능한 시점 설정을 씬 카메라에 기록합니다. 카메라에 적용을 누르면 미리보기 시점을 선택한 카메라에 적용합니다.", "撮影の構図を残したい場合に、プレビューの位置・回転と適用可能な視点設定をシーンのカメラへ書き込みます。 カメラに適用でプレビューの視点を選択カメラに適用できます。"),
                        DiNeTutorialStep.Optional("capture", "Capture saves a PNG using your chosen framing, output size and background settings. Click Capture Screenshot when the view is ready.", "캡처는 선택한 구도·출력 크기·배경 설정으로 PNG 파일을 저장합니다. 화면이 준비되면 스크린샷 캡처를 누르세요.", "撮影は選択した構図・出力サイズ・背景設定でPNGファイルを保存します。 画面が整ったらスクリーンショット撮影を押してください。"),
                        DiNeTutorialStep.Optional("open-folder", "The output folder contains the generated PNG files for reviewing or using outside Unity. Open Folder shows the output folder.", "출력 폴더에는 생성한 PNG가 저장되어 결과를 확인하거나 Unity 밖에서 사용할 수 있습니다. 폴더 열기로 출력 폴더를 여세요.", "出力フォルダには生成したPNGが保存され、結果の確認やUnity外での利用ができます。 フォルダを開くで出力フォルダを開いてください。"),
                        DiNeTutorialStep.Optional("ping-folder", "Locating the output in Project makes the generated image assets easy to select and assign in Unity. Ping Folder locates the output folder in the Project window.", "Project에서 출력 위치를 찾으면 생성한 이미지 에셋을 Unity에서 선택·배정하기 쉽습니다. 폴더 찾기로 프로젝트에서 출력 폴더를 찾으세요.", "Projectで出力先を確認すると、生成した画像アセットをUnityで選択・割り当てしやすくなります。 フォルダを確認でProjectウィンドウの出力フォルダを確認してください。"),
                    },
                    new[]
                    {
                        DiNeTutorialStep.Required("icon-target", "The target is rendered into a transparent square PNG, useful for an outfit or prop's Expressions Menu icon. Assign the object to turn into an icon.", "대상을 투명한 정사각형 PNG로 렌더링해 의상·소품의 Expressions Menu 아이콘 등에 사용합니다. 아이콘으로 만들 오브젝트를 지정하세요.", "対象を透明な正方形PNGへレンダリングし、衣装や小物のExpressions Menuアイコンなどに使います。 アイコンにするオブジェクトを指定してください。", () => _iconTarget != null),
                        DiNeTutorialStep.Optional("icon-rotate", "Rotation chooses the object's most recognizable angle for the icon. Left-drag in the preview to rotate the object.", "회전은 아이콘에서 오브젝트를 알아보기 쉬운 각도를 고를 때 사용합니다. 미리보기에서 왼쪽 버튼으로 드래그해 회전하세요.", "回転はアイコン内でオブジェクトを判別しやすい角度を選ぶために使います。 プレビューを左ドラッグして回転してください。"),
                        DiNeTutorialStep.Optional("icon-pan", "Moving the object changes its position within the icon's square framing. Right-drag or middle-drag in the preview to move the object.", "이동은 정사각형 아이콘 구도 안에서 오브젝트의 위치를 바꿉니다. 미리보기에서 오른쪽·가운데 버튼으로 드래그해 이동하세요.", "移動は正方形アイコンの構図内でオブジェクトの位置を変えます。 プレビューを右・中ドラッグして移動してください。"),
                        DiNeTutorialStep.Optional("icon-zoom", "Zoom changes the framing so small props can fill more of the icon. Scroll the mouse wheel in the preview to zoom.", "줌은 작은 소품이 아이콘을 더 크게 채우도록 구도를 조절합니다. 미리보기에서 마우스 휠로 확대·축소하세요.", "ズームは小さな小物をアイコン内で大きく見せるために構図を調整します。 プレビュー内でマウスホイールを回してズームしてください。"),
                        DiNeTutorialStep.Optional("icon-reset", "Resetting the view gives you a fresh starting composition without changing the source object. Click Reset View to reset rotation, zoom and position.", "시점 초기화는 원본 오브젝트를 바꾸지 않고 시작 구도로 돌아가게 합니다. 시점 초기화로 회전·확대·위치를 초기화하세요.", "視点をリセットすると元のオブジェクトを変えずに、最初の構図へ戻せます。 視点リセットで回転・ズーム・位置をリセットしてください。"),
                        DiNeTutorialStep.Optional("icon-direction", "Direction presets quickly align the object to a consistent view across a set of icons. Choose a direction button for a front, back, side, top or bottom view.", "방향 프리셋은 여러 아이콘에서 시점을 일정하게 맞출 때 사용합니다. 방향 버튼으로 정면·뒤·옆·위·아래 시점을 선택하세요.", "方向プリセットは複数のアイコンで視点を揃える際に使います。 方向ボタンで正面・背面・側面・上・下の視点を選んでください。"),
                        DiNeTutorialStep.Optional("icon-zoom-preset", "Zoom presets help keep the apparent object size consistent between related icons. Choose a zoom button to set the icon's framing.", "줌 프리셋은 관련 아이콘 사이에서 오브젝트가 보이는 크기를 일정하게 맞추는 데 사용합니다. 줌 버튼으로 아이콘의 확대 비율을 선택하세요.", "ズームプリセットは関連するアイコン間で、オブジェクトの見かけの大きさを揃えるために使います。 ズームボタンでアイコンの拡大率を選んでください。"),
                        DiNeTutorialStep.Optional("icon-idle", "Idle Pose uses the VRChat idle animation on a compatible humanoid preview for a more relaxed pose. Turn Idle Pose on to use the idle pose for the preview.", "자연스러운 포즈는 호환 휴머노이드 미리보기에 VRChat 기본 애니메이션을 적용해 편안한 자세로 만듭니다. 아이들 포즈를 켜서 미리보기에 적용하세요.", "自然なポーズは対応するHumanoidプレビューにVRChatのアイドルアニメーションを適用し、自然な姿勢にします。 アイドルポーズをオンにしてプレビューに適用してください。"),
                        DiNeTutorialStep.Optional("icon-outline", "An outline improves the silhouette's visibility when the icon is displayed small. Turn Outline on to add an outline.", "외곽선은 아이콘이 작게 표시될 때 실루엣을 알아보기 쉽게 합니다. 외곽선을 켜세요.", "アウトラインはアイコンが小さく表示されるときに、シルエットを見やすくします。 アウトラインをオンにしてください。"),
                        DiNeTutorialStep.Optional("icon-outline-color", "Outline color sets the contrast between the object edge and the menu background. With Outline on, choose its color.", "외곽선 색상은 오브젝트 가장자리와 메뉴 배경 사이의 대비를 정합니다. 외곽선을 켜고 색상을 선택하세요.", "アウトライン色はオブジェクトの縁とメニュー背景とのコントラストを決めます。 アウトラインをオンにし、色を選んでください。"),
                        DiNeTutorialStep.Optional("icon-outline-size", "Thickness controls how prominent the edge is, so you can avoid losing detail in a small icon. With Outline on, adjust its thickness.", "두께는 가장자리의 강조 정도를 바꾸므로 작은 아이콘에서 세부가 묻히지 않게 조절합니다. 외곽선을 켜고 두께를 조절하세요.", "太さは縁の強調度を変えるため、小さなアイコンで細部が隠れないように調整します。 アウトラインをオンにし、太さを調整してください。"),
                        DiNeTutorialStep.Optional("icon-forbidden", "A forbidden overlay creates a crossed-out version, useful for an unavailable or disabled item. Turn the forbidden mark on when you want it on the icon.", "금지 오버레이는 사용 불가·비활성 항목을 나타내는 금지 표시 아이콘을 만듭니다. 금지 표시가 필요하면 켜세요.", "禁止オーバーレイは、使用不可や無効な項目を示す禁止マーク付きアイコンを作ります。 禁止マークが必要ならオンにしてください。"),
                        DiNeTutorialStep.Optional("icon-forbidden-size", "Overlay size determines how much of the icon the forbidden mark covers. With the forbidden mark on, adjust its size.", "금지 표시 크기는 표시가 아이콘을 얼마나 덮는지 정합니다. 금지 표시를 켜고 크기를 조절하세요.", "禁止マークのサイズは、アイコンをどれだけ覆うかを決めます。 禁止マークをオンにし、サイズを調整してください。"),
                        DiNeTutorialStep.Optional("icon-forbidden-opacity", "Lower opacity leaves more of the object visible through the forbidden mark. Adjust the forbidden mark's opacity.", "불투명도를 낮추면 금지 표시 뒤의 오브젝트가 더 잘 보입니다. 금지 표시의 불투명도를 조절하세요.", "不透明度を下げると禁止マークの後ろのオブジェクトが見えやすくなります。 禁止マークの不透明度を調整してください。"),
                        DiNeTutorialStep.Optional("icon-forbidden-position", "Front or behind determines whether the mark covers the object or sits behind its silhouette. Choose whether the forbidden mark appears in front or behind.", "앞·뒤 선택은 금지 표시가 오브젝트를 덮을지 실루엣 뒤에 놓일지 정합니다. 금지 표시를 앞·뒤 중 어디에 둘지 선택하세요.", "手前・奥の選択は、禁止マークでオブジェクトを覆うか、シルエットの後ろに置くかを決めます。 禁止マークを手前・奥のどちらに置くか選んでください。"),
                        DiNeTutorialStep.Optional("icon-generate", "Generating saves the preview and effects as an icon image ready to assign to menu controls. Click Generate Icon to save. In an icon editor, this button overwrites that icon.", "생성하면 미리보기 구도와 효과를 메뉴 조작에 배정할 아이콘 이미지로 저장합니다. 아이콘 생성으로 저장하세요. 아이콘 편집 중이면 이 버튼으로 덮어씁니다.", "生成するとプレビューの構図と効果を、メニュー操作へ割り当てられるアイコン画像として保存します。 アイコン生成で保存してください。アイコン編集中はこのボタンで上書きします。"),
                        DiNeTutorialStep.Optional("icon-copy", "A separate copy lets you keep an existing icon while saving a different angle or effect variation. If a file already exists, use Create Copy to save a separate icon.", "복사본은 기존 아이콘을 보존하면서 다른 각도·효과 버전을 저장할 때 사용합니다. 파일이 이미 있으면 복사본 생성으로 별도 아이콘을 저장하세요.", "コピーは既存のアイコンを残しながら、異なる角度や効果のバリエーションを保存する際に使います。 ファイルが既にある場合はコピー生成で別のアイコンを保存してください。"),
                        DiNeTutorialStep.Optional("open-folder", "The output folder contains the generated PNG files for reviewing or using outside Unity. Open Folder shows the saved icons.", "출력 폴더에는 생성한 PNG가 저장되어 결과를 확인하거나 Unity 밖에서 사용할 수 있습니다. 폴더 열기로 저장된 아이콘을 확인하세요.", "出力フォルダには生成したPNGが保存され、結果の確認やUnity外での利用ができます。 フォルダを開くで保存したアイコンを確認してください。"),
                        DiNeTutorialStep.Optional("ping-folder", "Locating the output in Project makes the generated image assets easy to select and assign in Unity. Ping Folder locates the icon folder in the Project window.", "Project에서 출력 위치를 찾으면 생성한 이미지 에셋을 Unity에서 선택·배정하기 쉽습니다. 폴더 찾기로 프로젝트에서 아이콘 폴더를 찾으세요.", "Projectで出力先を確認すると、生成した画像アセットをUnityで選択・割り当てしやすくなります。 フォルダを確認でProjectウィンドウのアイコンフォルダを確認してください。"),
                    },
                };
            }
            bool screenshot = _mode == ToolMode.Screenshot;
            string[] overview = screenshot ? new[]
            {
                "Save a PNG screenshot from a chosen camera or the editor's Scene View, useful for avatar previews and portfolio images. Choose the output resolution and background, then compose the shot in the preview. Captures are saved under Assets/Di Ne/ScreenShot; Apply to Camera also lets you keep a preview composition on the scene camera.",
                "지정한 카메라나 에디터 씬 뷰를 PNG 스크린샷으로 저장해 아바타 소개·포트폴리오 이미지에 사용합니다. 출력 해상도와 배경을 정하고 미리보기에서 구도를 맞춘 뒤 촬영합니다. 결과는 Assets/Di Ne/ScreenShot에 저장되며, 카메라에 적용으로 미리보기 구도를 씬 카메라에도 남길 수 있습니다.",
                "指定したカメラやエディターのScene ViewをPNGスクリーンショットに保存し、アバター紹介やポートフォリオ画像に使えます。出力解像度と背景を選び、プレビューで構図を合わせて撮影します。結果はAssets/Di Ne/ScreenShotに保存され、カメラに適用でプレビューの構図をシーンのカメラにも残せます。"
            } : new[]
            {
                "Render an avatar, outfit or prop as a transparent square PNG for uses such as an Expressions Menu icon. Adjust its angle and framing, optionally add an outline or forbidden mark, then generate the image. When opened to edit an existing icon, Generate overwrites it; Create Copy keeps the old image and saves a variation.",
                "아바타·의상·소품을 투명한 정사각형 PNG로 만들어 Expressions Menu 아이콘 등에 사용합니다. 각도와 구도를 정하고 필요하면 외곽선·금지 표시를 더한 뒤 이미지를 생성합니다. 기존 아이콘 편집으로 연 경우 생성은 해당 파일을 덮어쓰며, 복사본 생성은 기존 이미지를 남기고 다른 버전을 저장합니다.",
                "アバター・衣装・小物を透明な正方形PNGにし、Expressions Menuアイコンなどに使えます。角度と構図を調整し、必要に応じてアウトラインや禁止マークを付けて生成します。既存アイコンの編集で開いた場合、生成はそのファイルを上書きし、コピー生成は元の画像を残して別の版を保存します。"
            };
            _tutorial.Configure(_mode.ToString(), screenshot ? "Screenshot" : "Icon", screenshot ? "스크린샷" : "아이콘", screenshot ? "スクリーンショット" : "アイコン", _tutorialCourses[(int)_mode],
                overviewEn: overview[0], overviewKo: overview[1], overviewJa: overview[2]);
            _tutorial.BeginFrame();
        }

        private void TutorialAnchor(string id) => _tutorial?.Anchor(id, GUILayoutUtility.GetLastRect());
        private void TutorialAnchor(string id, Rect rect) => _tutorial?.Anchor(id, rect);
        private void TutorialDraw(params string[] ids) { foreach (string id in ids) _tutorial?.Draw(id); }
        private void TutorialNotify(string id) => _tutorial?.NotifyAction(id);
    }
}
