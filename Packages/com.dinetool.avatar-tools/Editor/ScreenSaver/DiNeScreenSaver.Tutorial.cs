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
                        DiNeTutorialStep.Optional("capture-target", "Choose Game View or Scene View to capture.", "캡처할 게임 뷰·씬 뷰를 선택하세요.", "撮影するGame View・Scene Viewを選んでください。"),
                        DiNeTutorialStep.Required("camera", "For Game View, assign a camera. For Scene View, open the Scene window.", "게임 뷰에서는 카메라를 지정하세요. 씬 뷰에서는 씬 창을 여세요.", "Game Viewではカメラを指定し、Scene ViewではSceneウィンドウを開いてください。", () => _captureTarget == CaptureTarget.GameView ? _camera != null : SceneView.lastActiveSceneView != null),
                        DiNeTutorialStep.Optional("resolution", "Choose FHD, QHD, UHD or Custom for the output size.", "출력 크기를 FHD·QHD·UHD·Custom에서 선택하세요.", "出力サイズをFHD・QHD・UHD・Customから選んでください。"),
                        DiNeTutorialStep.Optional("aspect", "With a resolution preset, choose the aspect ratio.", "해상도 프리셋을 선택했다면 화면 비율을 고르세요.", "解像度プリセットを選んだら縦横比を選んでください。"),
                        DiNeTutorialStep.Optional("custom-size", "Choose Custom, then enter the output width and height.", "Custom을 선택하고 출력 너비·높이를 입력하세요.", "Customを選び、出力の幅・高さを入力してください。"),
                        DiNeTutorialStep.Optional("background", "Choose Transparent or Color for the background.", "배경을 투명·컬러 중에서 선택하세요.", "背景を透明・カラーから選んでください。"),
                        DiNeTutorialStep.Optional("background-color", "For a color background, choose the background color here.", "컬러 배경을 선택했다면 배경색을 고르세요.", "カラー背景を選んだら背景色を選んでください。"),
                        DiNeTutorialStep.Optional("game-fov", "For a perspective camera, adjust the preview's field of view.", "원근 카메라에서는 미리보기의 시야각을 조절하세요.", "透視カメラではプレビューの視野角を調整してください。"),
                        DiNeTutorialStep.Optional("game-rotate", "In the Game View preview, drag with the left mouse button to rotate.", "게임 뷰 미리보기에서 왼쪽 버튼으로 드래그해 회전하세요.", "Game Viewのプレビューを左ドラッグして回転してください。"),
                        DiNeTutorialStep.Optional("game-pan", "Right-drag or middle-drag in the preview to move the view.", "미리보기에서 오른쪽·가운데 버튼으로 드래그해 시점을 이동하세요.", "プレビューを右・中ドラッグして視点を移動してください。"),
                        DiNeTutorialStep.Optional("game-zoom", "Scroll the mouse wheel in the preview to zoom.", "미리보기에서 마우스 휠로 확대·축소하세요.", "プレビュー内でマウスホイールを回してズームしてください。"),
                        DiNeTutorialStep.Optional("game-reset", "Use Reset to return the preview to the camera's current view.", "초기화를 눌러 카메라의 현재 시점으로 돌아가세요.", "リセットでカメラの現在の視点に戻してください。"),
                        DiNeTutorialStep.Optional("game-focus", "Select an object, then click Focus or press F in the preview.", "오브젝트를 선택하고 포커스를 누르거나 미리보기에서 F를 누르세요.", "オブジェクトを選び、フォーカスを押すかプレビュー内でFを押してください。"),
                        DiNeTutorialStep.Optional("game-apply", "Apply to Camera saves this preview view to the selected camera.", "카메라에 적용을 누르면 미리보기 시점을 선택한 카메라에 적용합니다.", "カメラに適用でプレビューの視点を選択カメラに適用できます。"),
                        DiNeTutorialStep.Optional("capture", "Click Capture Screenshot when the view is ready.", "화면이 준비되면 스크린샷 캡처를 누르세요.", "画面が整ったらスクリーンショット撮影を押してください。"),
                        DiNeTutorialStep.Optional("open-folder", "Open Folder shows the output folder.", "폴더 열기로 출력 폴더를 여세요.", "フォルダを開くで出力フォルダを開いてください。"),
                        DiNeTutorialStep.Optional("ping-folder", "Ping Folder locates the output folder in the Project window.", "폴더 찾기로 프로젝트에서 출력 폴더를 찾으세요.", "フォルダを確認でProjectウィンドウの出力フォルダを確認してください。"),
                    },
                    new[]
                    {
                        DiNeTutorialStep.Required("icon-target", "Assign the object to turn into an icon.", "아이콘으로 만들 오브젝트를 지정하세요.", "アイコンにするオブジェクトを指定してください。", () => _iconTarget != null),
                        DiNeTutorialStep.Optional("icon-rotate", "Left-drag in the preview to rotate the object.", "미리보기에서 왼쪽 버튼으로 드래그해 회전하세요.", "プレビューを左ドラッグして回転してください。"),
                        DiNeTutorialStep.Optional("icon-pan", "Right-drag or middle-drag in the preview to move the object.", "미리보기에서 오른쪽·가운데 버튼으로 드래그해 이동하세요.", "プレビューを右・中ドラッグして移動してください。"),
                        DiNeTutorialStep.Optional("icon-zoom", "Scroll the mouse wheel in the preview to zoom.", "미리보기에서 마우스 휠로 확대·축소하세요.", "プレビュー内でマウスホイールを回してズームしてください。"),
                        DiNeTutorialStep.Optional("icon-reset", "Click Reset View to reset rotation, zoom and position.", "시점 초기화로 회전·확대·위치를 초기화하세요.", "視点リセットで回転・ズーム・位置をリセットしてください。"),
                        DiNeTutorialStep.Optional("icon-direction", "Choose a direction button for a front, back, side, top or bottom view.", "방향 버튼으로 정면·뒤·옆·위·아래 시점을 선택하세요.", "方向ボタンで正面・背面・側面・上・下の視点を選んでください。"),
                        DiNeTutorialStep.Optional("icon-zoom-preset", "Choose a zoom button to set the icon's framing.", "줌 버튼으로 아이콘의 확대 비율을 선택하세요.", "ズームボタンでアイコンの拡大率を選んでください。"),
                        DiNeTutorialStep.Optional("icon-idle", "Turn Idle Pose on to use the idle pose for the preview.", "아이들 포즈를 켜서 미리보기에 적용하세요.", "アイドルポーズをオンにしてプレビューに適用してください。"),
                        DiNeTutorialStep.Optional("icon-outline", "Turn Outline on to add an outline.", "외곽선을 켜세요.", "アウトラインをオンにしてください。"),
                        DiNeTutorialStep.Optional("icon-outline-color", "With Outline on, choose its color.", "외곽선을 켜고 색상을 선택하세요.", "アウトラインをオンにし、色を選んでください。"),
                        DiNeTutorialStep.Optional("icon-outline-size", "With Outline on, adjust its thickness.", "외곽선을 켜고 두께를 조절하세요.", "アウトラインをオンにし、太さを調整してください。"),
                        DiNeTutorialStep.Optional("icon-forbidden", "Turn the forbidden mark on when you want it on the icon.", "금지 표시가 필요하면 켜세요.", "禁止マークが必要ならオンにしてください。"),
                        DiNeTutorialStep.Optional("icon-forbidden-size", "With the forbidden mark on, adjust its size.", "금지 표시를 켜고 크기를 조절하세요.", "禁止マークをオンにし、サイズを調整してください。"),
                        DiNeTutorialStep.Optional("icon-forbidden-opacity", "Adjust the forbidden mark's opacity.", "금지 표시의 불투명도를 조절하세요.", "禁止マークの不透明度を調整してください。"),
                        DiNeTutorialStep.Optional("icon-forbidden-position", "Choose whether the forbidden mark appears in front or behind.", "금지 표시를 앞·뒤 중 어디에 둘지 선택하세요.", "禁止マークを手前・奥のどちらに置くか選んでください。"),
                        DiNeTutorialStep.Optional("icon-generate", "Click Generate Icon to save. In an icon editor, this button overwrites that icon.", "아이콘 생성으로 저장하세요. 아이콘 편집 중이면 이 버튼으로 덮어씁니다.", "アイコン生成で保存してください。アイコン編集中はこのボタンで上書きします。"),
                        DiNeTutorialStep.Optional("icon-copy", "If a file already exists, use Create Copy to save a separate icon.", "파일이 이미 있으면 복사본 생성으로 별도 아이콘을 저장하세요.", "ファイルが既にある場合はコピー生成で別のアイコンを保存してください。"),
                        DiNeTutorialStep.Optional("open-folder", "Open Folder shows the saved icons.", "폴더 열기로 저장된 아이콘을 확인하세요.", "フォルダを開くで保存したアイコンを確認してください。"),
                        DiNeTutorialStep.Optional("ping-folder", "Ping Folder locates the icon folder in the Project window.", "폴더 찾기로 프로젝트에서 아이콘 폴더를 찾으세요.", "フォルダを確認でProjectウィンドウのアイコンフォルダを確認してください。"),
                    },
                };
            }
            bool screenshot = _mode == ToolMode.Screenshot;
            _tutorial.Configure(_mode.ToString(), screenshot ? "Screenshot" : "Icon", screenshot ? "스크린샷" : "아이콘", screenshot ? "スクリーンショット" : "アイコン", _tutorialCourses[(int)_mode]);
            _tutorial.BeginFrame();
        }

        private void TutorialAnchor(string id) => _tutorial?.Anchor(id, GUILayoutUtility.GetLastRect());
        private void TutorialAnchor(string id, Rect rect) => _tutorial?.Anchor(id, rect);
        private void TutorialDraw(params string[] ids) { foreach (string id in ids) _tutorial?.Draw(id); }
        private void TutorialNotify(string id) => _tutorial?.NotifyAction(id);
    }
}
