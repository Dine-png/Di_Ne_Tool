using UnityEngine;

public partial class DiNeMaterialTool
{
    private DiNeGuidedTutorial _tutorial;
    private DiNeTutorialStep[][] _tutorialCourses;
    private static readonly string[][] TutorialOverviews =
    {
        new[]
        {
            "Apply a saved lilToon look to several materials at once, such as matching the shading of multiple clothing pieces. Choose an object, a preset, and the materials to change; the preset copies its saved colors, values, and texture assignments. Preview Check reports the selected targets, and Apply Preset writes to the material assets with Undo support.",
            "저장된 lilToon 스타일을 여러 마테리얼에 한 번에 적용해 옷 파츠들의 색감과 셰이딩을 맞출 수 있습니다. 대상 오브젝트와 프리셋을 고르고 적용할 마테리얼을 선택하면, 프리셋에 저장된 색·수치·텍스처 연결을 복사합니다. 미리보기 확인은 적용 대상 수를 알려 주며, 프리셋 적용은 마테리얼 에셋을 변경하고 Undo로 되돌릴 수 있습니다.",
            "保存済みのlilToonの見た目を複数のマテリアルへ一括適用し、服のパーツなどの色やシェーディングを揃えられます。対象オブジェクト、プリセット、変更するマテリアルを選ぶと、保存された色・数値・テクスチャの割り当てをコピーします。プレビュー確認は適用対象数を表示し、プリセット適用はマテリアルアセットを変更します。変更はUndoで戻せます。"
        },
        new[]
        {
            "Remove texture assignments from selected lilToon or Poiyomi features, for example MatCap or emission effects you no longer need. Choose the feature sections and materials, review the removal list, then clear their texture slots or also disable supported feature switches. Texture files stay in the project; the selected material assets change, with Undo support.",
            "lilToon·Poiyomi 마테리얼에서 더 이상 필요 없는 MatCap이나 발광 효과 등의 텍스처 연결을 정리할 수 있습니다. 정리할 기능 섹션과 마테리얼을 고르고 제거 목록을 확인한 뒤, 텍스처 슬롯만 비우거나 지원되는 기능 스위치도 함께 끕니다. 텍스처 파일은 프로젝트에 남으며, 선택한 마테리얼 에셋의 변경은 Undo로 되돌릴 수 있습니다.",
            "lilToon・Poiyomiのマテリアルで、不要になったMatCapや発光効果などのテクスチャ割り当てを整理できます。機能のセクションとマテリアルを選び、削除一覧を確認してから、テクスチャスロットを空にするか対応する機能スイッチもオフにします。テクスチャファイルはプロジェクトに残り、マテリアルアセットの変更はUndoで戻せます。"
        },
        new[]
        {
            "Find which textures use the most GPU memory and reduce their estimated VRAM cost through compression or maximum resolution. Scan an object, inspect large textures and their users, then adjust individual textures or a checked batch. These actions change texture import settings for every object using the same texture, and do not support Undo; compare the image quality and reported savings before choosing settings.",
            "GPU 메모리를 많이 쓰는 텍스처를 찾아 압축 방식이나 최대 해상도를 조절해 예상 VRAM 사용량을 줄일 수 있습니다. 오브젝트를 스캔하고 용량이 큰 텍스처와 사용 위치를 확인한 뒤, 개별 항목이나 선택한 여러 항목을 변경하세요. 텍스처 임포트 설정을 바꾸므로 같은 텍스처를 쓰는 모든 오브젝트에 영향을 주며 Undo는 지원하지 않습니다. 화질과 표시된 절약량을 함께 보고 설정을 고르세요.",
            "GPUメモリを多く使うテクスチャを見つけ、圧縮方式や最大解像度で推定VRAM使用量を減らせます。オブジェクトをスキャンして大きなテクスチャと使用箇所を確認し、個別または選択した複数の項目を変更します。変更はインポート設定に反映され、同じテクスチャを使う全オブジェクトに影響し、Undoには対応しません。画質と表示される削減量を確認して設定を選んでください。"
        },
        new[]
        {
            "Edit shader settings on several lilToon or Poiyomi materials together, such as giving all clothing parts the same shadow or emission strength. Choose a shader group and check the materials, then edit their shared material inspector. Each edited field changes immediately on the selected material assets; other fields keep their own values, and Ctrl+Z can undo the change.",
            "여러 lilToon·Poiyomi 마테리얼의 셰이더 설정을 함께 조절해 옷 파츠들의 그림자나 발광 강도를 맞출 수 있습니다. 셰이더 그룹과 조절할 마테리얼을 선택한 뒤 공통 마테리얼 인스펙터에서 값을 바꾸세요. 수정한 항목은 선택한 마테리얼 에셋에 즉시 반영되고 나머지 항목은 각자의 값을 유지합니다. Ctrl+Z로 변경을 되돌릴 수 있습니다.",
            "複数のlilToon・Poiyomiマテリアルのシェーダー設定をまとめて調整し、服のパーツなどの影や発光の強さを揃えられます。シェーダーグループとマテリアルを選び、共通のマテリアルInspectorで値を変更してください。編集した項目は選択したマテリアルアセットへ即座に反映され、他の項目は元の値を維持します。Ctrl+Zで変更を戻せます。"
        }
    };

    private void BeginTutorialFrame()
    {
        if (_tutorial == null) _tutorial = new DiNeGuidedTutorial(this, "material-tool");
        if (_tutorialCourses == null)
        {
            var target = DiNeTutorialStep.Required("target", "Assign an avatar root or a clothing object to scan its renderers' materials. This defines which materials and textures appear in the current mode.", "아바타 루트나 의상 오브젝트를 지정해 렌더러의 마테리얼을 스캔하세요. 지정한 대상에 따라 현재 모드에 표시되는 마테리얼과 텍스처가 정해집니다.", "アバターのルートや服のオブジェクトを指定し、レンダラーのマテリアルをスキャンしてください。指定した対象によって、このモードに表示されるマテリアルとテクスチャが決まります。", () => _targetObject != null);
            var children = DiNeTutorialStep.Optional("children", "Include child objects to scan all parts below the assigned object. For example, enable this on an avatar root to include its body and clothing renderers.", "자식 포함을 켜면 지정한 오브젝트 아래의 모든 파츠를 함께 스캔합니다. 아바타 루트를 지정했다면 몸과 의상의 렌더러까지 포함할 수 있습니다.", "子オブジェクトを含めると、指定オブジェクト以下のパーツをまとめてスキャンします。アバターのルートを指定した場合、体や服のレンダラーも対象にできます。");
            var inactive = DiNeTutorialStep.Optional("inactive", "When child objects are included, enable this to scan disabled parts too. This lets you include clothing that is currently hidden in the hierarchy.", "자식 포함 상태에서 이 항목을 켜면 비활성 파츠도 스캔합니다. Hierarchy에서 꺼 둔 의상까지 작업 대상에 넣을 수 있습니다.", "子オブジェクトを含める場合、これをオンにすると非アクティブなパーツもスキャンします。Hierarchyで非表示にした服も対象にできます。");
            _tutorialCourses = new[]
            {
                new[]
                {
                    target, children, inactive,
                    DiNeTutorialStep.Optional("preview", "Turn Preview Mode on to review how many materials would receive the preset without writing changes. This checks the target selection; it does not render the preset's appearance.", "미리보기 모드를 켜면 마테리얼을 변경하지 않고 프리셋 적용 대상 수를 확인할 수 있습니다. 대상 선택을 확인하는 기능이며 프리셋의 외형을 렌더링하지는 않습니다.", "プレビューモードをオンにすると、変更を保存せずにプリセットの適用対象数を確認できます。対象選択を確認する機能で、適用後の見た目を描画する機能ではありません。"),
                    DiNeTutorialStep.Optional("refresh", "Refresh to rebuild the list of lilToon materials on the assigned object. Use it after adding a clothing part or changing a renderer's material assignments.", "새로고침을 눌러 대상 오브젝트의 lilToon 마테리얼 목록을 다시 만드세요. 의상 파츠를 추가하거나 렌더러의 마테리얼 연결을 바꾼 뒤 사용하면 됩니다.", "更新で対象オブジェクトのlilToonマテリアル一覧を再作成してください。服のパーツを追加したり、レンダラーのマテリアル割り当てを変えた後に使います。"),
                    DiNeTutorialStep.Optional("library-scan", "Scan Project finds saved lilToon presets for the library. Run it after importing a preset pack so the new looks are available to select.", "프로젝트 스캔은 저장된 lilToon 프리셋을 찾아 라이브러리에 표시합니다. 프리셋 팩을 임포트한 뒤 실행하면 새 스타일을 선택할 수 있습니다.", "プロジェクトスキャンは保存済みのlilToonプリセットを探してライブラリに表示します。プリセット集をインポートした後に実行すると、新しい見た目を選べます。"),
                    DiNeTutorialStep.Optional("library-category", "Choose a category such as Skin, Hair, or Cloth to narrow the preset list. Categories help you find a suitable starting look for the part you are editing.", "피부·머리카락·의상 같은 카테고리로 프리셋 목록을 좁히세요. 편집할 파츠에 맞는 스타일을 찾기 쉬워집니다.", "肌・髪・服などのカテゴリでプリセット一覧を絞ってください。編集するパーツに合う見た目を探しやすくなります。"),
                    DiNeTutorialStep.Optional("library-search", "Type part of a preset name to filter the library. Press × to clear the search and show the category's full list again.", "프리셋 이름의 일부를 입력해 라이브러리를 검색하세요. ×를 누르면 검색을 지우고 해당 카테고리의 전체 목록을 다시 볼 수 있습니다.", "プリセット名の一部を入力してライブラリを絞ってください。×を押すと検索を消し、カテゴリの全一覧へ戻れます。"),
                    DiNeTutorialStep.Optional("library-preset", "Click a preset to choose the saved look to copy. Its detail panel shows how many colors, values, vectors, and textures it contains before you apply it.", "프리셋을 클릭해 복사할 스타일을 선택하세요. 적용하기 전에 상세 패널에서 저장된 색·수치·벡터·텍스처 항목 수를 확인할 수 있습니다.", "プリセットをクリックしてコピーする見た目を選んでください。適用前に詳細パネルで、保存された色・数値・ベクトル・テクスチャの項目数を確認できます。"),
                    DiNeTutorialStep.Optional("preset-materials", "Check only the materials that should share this preset, for example all pieces of one outfit. All or Clear changes the selection; unchecked materials are left as they are.", "한 의상의 모든 파츠처럼 같은 프리셋을 적용할 마테리얼만 체크하세요. 전체 선택·해제는 대상 선택을 바꾸며, 체크하지 않은 마테리얼은 유지됩니다.", "1着の服の各パーツなど、同じプリセットに揃えるマテリアルだけをチェックしてください。全て選択・解除で対象を変更でき、未選択のマテリアルはそのまま残ります。"),
                    DiNeTutorialStep.Optional("material-ping", "Click Ping to highlight this material asset in the Project window. You can inspect the source asset before applying settings shared by other objects that use it.", "Ping을 눌러 Project 창에서 해당 마테리얼 에셋을 표시하세요. 같은 에셋을 사용하는 다른 오브젝트에도 영향을 주는 설정을 적용하기 전에 원본을 확인할 수 있습니다.", "PingでProjectウィンドウのマテリアルアセットを表示してください。同じアセットを使う他のオブジェクトにも影響する設定を適用する前に、元のアセットを確認できます。"),
                    DiNeTutorialStep.Optional("preset-apply", "Preview Check reports the number of selected targets. Switch to Apply Mode and use Apply Preset to copy the preset's saved properties into their material assets; Ctrl+Z can undo the change.", "미리보기 확인은 선택한 적용 대상 수를 알려 줍니다. 적용 모드에서 프리셋 적용을 누르면 저장된 속성이 대상 마테리얼 에셋에 복사되며, Ctrl+Z로 되돌릴 수 있습니다.", "プレビュー確認は選択した対象数を表示します。適用モードでプリセット適用を押すと、保存されたプロパティが対象マテリアルアセットへコピーされ、Ctrl+Zで戻せます。"),
                },
                new[]
                {
                    target, children, inactive,
                    DiNeTutorialStep.Optional("preview", "Turn Preview Mode on to inspect which texture assignments would be cleared. Removal actions then report their target count without changing the materials.", "미리보기 모드를 켜고 비워질 텍스처 연결을 확인하세요. 이 상태에서 제거를 누르면 마테리얼을 변경하지 않고 제거 예정 수를 알려 줍니다.", "プレビューモードをオンにして、解除されるテクスチャ割り当てを確認してください。この状態で削除を押すと、マテリアルを変更せずに対象数を表示します。"),
                    DiNeTutorialStep.Optional("refresh", "Refresh to scan the assigned object's lilToon and Poiyomi materials again. The list shows texture assignments in the feature sections currently selected for cleanup.", "새로고침으로 대상의 lilToon·Poiyomi 마테리얼을 다시 스캔하세요. 현재 정리 대상으로 선택한 기능 섹션의 텍스처 연결이 목록에 표시됩니다.", "更新で対象のlilToon・Poiyomiマテリアルを再スキャンしてください。整理対象に選んだ機能セクションのテクスチャ割り当てが一覧に表示されます。"),
                    DiNeTutorialStep.Optional("diet-sections", "Check the feature sections you intend to strip, such as unused MatCap or emission maps. Select All and Deselect All change every section, so check the list before applying.", "쓰지 않는 MatCap·발광 맵처럼 정리하려는 기능 섹션을 체크하세요. 전체 선택·해제는 모든 섹션에 적용되므로 실행 전에 대상 목록을 확인하세요.", "不要なMatCapや発光マップなど、整理したい機能セクションをチェックしてください。全て選択・解除は全セクションに反映されるので、適用前に対象一覧を確認してください。"),
                    DiNeTutorialStep.Optional("diet-materials", "Check the materials to clean, then expand each card to see the exact texture slots and thumbnails. This lets you keep effects you still need by excluding their materials or sections.", "정리할 마테리얼을 체크하고 카드를 펼쳐 실제 텍스처 슬롯과 썸네일을 확인하세요. 필요한 효과는 해당 마테리얼이나 섹션을 선택에서 빼서 유지할 수 있습니다.", "整理するマテリアルをチェックし、カードを展開して実際のテクスチャスロットとサムネイルを確認してください。必要な効果は、そのマテリアルやセクションを対象から外して残せます。"),
                    DiNeTutorialStep.Optional("material-ping", "Ping highlights the material asset being cleaned in the Project window. Confirm its identity when several clothing parts use similarly named materials.", "Ping은 정리할 마테리얼 에셋을 Project 창에서 표시합니다. 여러 의상 파츠에 비슷한 이름의 마테리얼이 있을 때 실제 대상을 확인하세요.", "Pingは整理するマテリアルアセットをProjectウィンドウで表示します。服のパーツに似た名前のマテリアルがある場合、実際の対象を確認してください。"),
                    DiNeTutorialStep.Optional("diet-remove", "Remove Textures Only clears the selected texture slots while keeping the feature switches. In Apply Mode it edits material assets with Undo support; the texture files are not deleted.", "텍스쳐만 제거는 기능 스위치를 유지하면서 선택한 텍스처 슬롯을 비웁니다. 적용 모드에서는 마테리얼 에셋을 변경하고 Undo로 되돌릴 수 있으며, 텍스처 파일은 삭제하지 않습니다.", "テクスチャのみ削除は機能スイッチを維持して、選択したテクスチャスロットを空にします。適用モードではマテリアルアセットを変更し、Undoで戻せます。テクスチャファイルは削除しません。"),
                    DiNeTutorialStep.Optional("diet-disable", "Remove + Disable also turns off supported switches for the selected sections, such as outline or MatCap. Use this when removing the whole effect; sections without a disable switch, such as Shadow / AO, only have their textures cleared.", "제거 + 기능 끄기는 아웃라인·MatCap처럼 선택한 섹션의 지원되는 기능 스위치도 끕니다. 효과 자체를 없앨 때 사용하며, 쉐도우 / AO처럼 끄기 스위치가 없는 섹션은 텍스처 연결만 비웁니다.", "削除＋機能を無効化は、アウトラインやMatCapなど選択セクションの対応スイッチもオフにします。効果自体をなくしたいときに使い、シャドウ / AOなど無効化スイッチのないセクションはテクスチャ割り当てだけを解除します。"),
                },
                new[]
                {
                    target, children, inactive,
                    DiNeTutorialStep.Optional("refresh", "Refresh to list unique textures used by the scanned materials, largest estimated VRAM cost first. The total helps you identify which textures are worth reducing.", "새로고침으로 스캔한 마테리얼이 쓰는 텍스처를 예상 VRAM 용량이 큰 순서로 확인하세요. 같은 텍스처는 한 번만 집계하며, 합계를 보고 먼저 줄일 항목을 찾을 수 있습니다.", "更新でスキャンしたマテリアルのテクスチャを、推定VRAM使用量の大きい順に確認してください。同じテクスチャは1回だけ集計され、合計から先に減らす対象を探せます。"),
                    DiNeTutorialStep.Optional("vram-select", "Check textures that can use the same compression or resolution, for example small clothing masks. All or Clear changes this batch selection, and the selected VRAM total shows its current cost.", "작은 의상 마스크처럼 같은 압축·해상도를 적용할 텍스처를 체크하세요. 전체·해제로 일괄 변경 대상을 고를 수 있으며 선택 합계에서 현재 용량을 확인합니다.", "小さな服のマスクなど、同じ圧縮・解像度に変更するテクスチャをチェックしてください。全体・解除で一括対象を選び、選択合計で現在の使用量を確認できます。"),
                    DiNeTutorialStep.Optional("vram-bulk-format", "Enable Format to include compression in the batch change and choose a suitable format. Compression can reduce VRAM, but preserve the alpha or normal-map detail your selected textures need.", "압축을 체크하면 일괄 변경에 압축 방식이 포함됩니다. VRAM을 줄일 수 있는 형식을 고르되, 선택한 텍스처에 필요한 알파나 노멀 맵의 세부 표현을 고려하세요.", "形式をオンにすると、圧縮方式が一括変更に含まれます。VRAMを減らせる方式を選びつつ、対象テクスチャに必要なアルファやノーマルマップの細部も考慮してください。"),
                    DiNeTutorialStep.Optional("vram-bulk-size", "Enable Size to set a maximum resolution for every checked texture, for example 1024 for small accessories. Lower limits reduce memory but can soften fine details.", "해상도를 체크하면 선택한 텍스처들의 최대 해상도를 함께 설정합니다. 작은 장식에 1024를 쓰는 식으로 제한을 낮추면 메모리를 줄일 수 있지만 세부 무늬가 흐려질 수 있습니다.", "解像度をオンにすると、選択テクスチャの最大解像度をまとめて設定できます。小さな装飾を1024にするなど上限を下げるとメモリを減らせますが、細かい模様がぼやける場合があります。"),
                    DiNeTutorialStep.Optional("vram-bulk-apply", "Apply reimports the checked textures using only the enabled Format and Size settings. This changes the texture assets for all their users and cannot be undone with Ctrl+Z.", "적용은 체크한 텍스처를 켜 둔 압축·해상도 설정으로 다시 임포트합니다. 같은 텍스처를 사용하는 모든 곳에 반영되며 Ctrl+Z로 되돌릴 수 없습니다.", "適用はチェックしたテクスチャを、有効にした形式・解像度の設定で再インポートします。同じテクスチャを使う全箇所に反映され、Ctrl+Zでは戻せません。"),
                    DiNeTutorialStep.Optional("vram-thumbnail", "Click a thumbnail to highlight its texture asset, or double-click to open the larger preview. Inspect small details and transparency when deciding how far to lower its resolution.", "썸네일을 클릭하면 텍스처 에셋을 표시하고, 두 번 클릭하면 큰 미리보기 창을 엽니다. 해상도를 얼마나 낮출지 정할 때 세부 무늬와 투명도를 확인하세요.", "サムネイルをクリックするとテクスチャアセットを表示し、ダブルクリックで大きなプレビューを開きます。解像度をどこまで下げるか決めるとき、細部や透明部分を確認してください。"),
                    DiNeTutorialStep.Optional("vram-format", "Change a texture's Format list to reimport just that asset with another compression format. Use individual settings when a texture needs different alpha or quality handling from the rest of the batch.", "텍스처의 압축 목록을 바꾸면 해당 에셋만 다른 형식으로 다시 임포트합니다. 일괄 대상과 다른 알파·화질 처리가 필요한 텍스처에 개별 설정을 사용하세요.", "テクスチャの形式一覧を変更すると、そのアセットだけを別の圧縮方式で再インポートします。一括対象とは異なるアルファや画質の扱いが必要な場合、個別設定を使ってください。"),
                    DiNeTutorialStep.Optional("vram-size", "Change a texture's Size list to reimport only that asset at the chosen maximum resolution. Keep detailed face or clothing textures larger than simple masks if the preview shows a visible quality loss.", "텍스처의 해상도 목록을 바꾸면 해당 에셋만 선택한 최대 해상도로 다시 임포트합니다. 미리보기에서 화질 저하가 보이면 얼굴·의상의 세밀한 텍스처는 단순한 마스크보다 크게 유지하세요.", "テクスチャの解像度一覧を変えると、そのアセットだけを選んだ最大解像度で再インポートします。画質の低下が見える場合、顔や服の細かいテクスチャは単純なマスクより大きく保ってください。"),
                    DiNeTutorialStep.Optional("vram-references", "Expand Objects or Mats to see the scanned renderers and materials that share this texture. Locate those users to judge where a compression or size change will affect the avatar.", "오브젝트·마테리얼 목록을 펼치면 스캔 범위에서 이 텍스처를 공유하는 렌더러와 마테리얼을 볼 수 있습니다. 사용 위치를 찾아 압축·해상도 변경이 아바타의 어느 부분에 영향을 주는지 판단하세요.", "オブジェクト・マテリアル一覧を展開すると、スキャン範囲内でこのテクスチャを共有するレンダラーとマテリアルを確認できます。使用箇所を見て、圧縮・解像度の変更がアバターのどこに影響するか判断してください。"),
                    DiNeTutorialStep.Optional("vram-optimize", "Optimize applies this texture's suggested compression and/or resolution changes after confirmation. The displayed savings estimate helps compare the benefit, but the suggestion still needs a visual quality check.", "개별 최적화는 확인 후 이 텍스처에 제안된 압축·해상도 변경을 적용합니다. 표시된 예상 절약량으로 효과를 비교하고, 제안 설정에서도 원하는 화질이 유지되는지 확인하세요.", "個別の最適化は、確認後にこのテクスチャの推奨圧縮・解像度変更を適用します。表示された推定削減量で効果を比較し、推奨設定でも必要な画質が保たれるか確認してください。"),
                    DiNeTutorialStep.Optional("vram-optimize-all", "Optimize All applies suggested changes to every eligible scanned texture, regardless of the batch checkboxes. Review the affected count and estimated savings in the confirmation before reimporting the assets.", "전체 최적화는 일괄 선택 체크와 관계없이 스캔한 모든 최적화 가능 텍스처에 제안을 적용합니다. 에셋을 다시 임포트하기 전에 확인 창의 대상 수와 예상 절약량을 검토하세요.", "全て最適化は、一括選択のチェックに関係なく、スキャンした全ての最適化可能なテクスチャへ推奨変更を適用します。再インポートする前に、確認画面の対象数と推定削減量を確認してください。"),
                },
                new[]
                {
                    target, children, inactive,
                    DiNeTutorialStep.Optional("refresh", "Refresh to scan the object's editable lilToon and Poiyomi materials again. Use it after changing material assignments so the batch editor contains the current parts.", "새로고침으로 대상의 편집 가능한 lilToon·Poiyomi 마테리얼을 다시 스캔하세요. 마테리얼 연결을 바꾼 뒤 사용하면 현재 파츠가 일괄 편집 목록에 반영됩니다.", "更新で対象の編集可能なlilToon・Poiyomiマテリアルを再スキャンしてください。割り当てを変更した後に使うと、現在のパーツが一括編集一覧へ反映されます。"),
                    DiNeTutorialStep.Optional("bulk-group", "Choose the shader group whose settings you want to edit when several groups appear. Materials with compatible shader families and variants are grouped so the inspector can offer their fields together.", "여러 그룹이 표시되면 편집할 셰이더 그룹을 선택하세요. 호환되는 셰이더 계열·변형의 마테리얼을 묶어 공통 설정을 한 인스펙터에서 조절합니다.", "複数のグループが表示されたら、編集するシェーダーグループを選んでください。互換性のあるシェーダー系統・バリアントをまとめ、共通の設定を1つのInspectorで調整します。"),
                    DiNeTutorialStep.Optional("bulk-list", "Expand Materials and check only the parts that should change together. For example, select several clothing materials while leaving the skin unchecked.", "마테리얼 목록을 펼쳐 함께 변경할 파츠만 체크하세요. 예를 들어 의상 마테리얼 여러 개를 선택하고 피부는 선택에서 뺄 수 있습니다.", "マテリアル一覧を展開し、一緒に変更するパーツだけをチェックしてください。例えば複数の服のマテリアルを選び、肌を対象から外せます。", () => _bulkListFoldout = true),
                    DiNeTutorialStep.Optional("material-ping", "The small Ping button highlights a material asset in the Project window. Check its source when you need to identify which part belongs in the batch selection.", "작은 Ping 버튼은 Project 창에서 마테리얼 에셋을 표시합니다. 어떤 파츠를 일괄 선택에 넣을지 판단할 때 원본 에셋을 확인하세요.", "小さいPingボタンはProjectウィンドウでマテリアルアセットを表示します。どのパーツを一括対象に含めるか判断するとき、元のアセットを確認してください。"),
                    DiNeTutorialStep.Optional("bulk-properties", "Edit a field to set that property on every checked material immediately, for example a common shadow strength. Mixed values appear as —; unchanged fields retain their individual values, and Ctrl+Z undoes the edit.", "그림자 강도를 맞추는 식으로 항목을 수정하면 체크한 모든 마테리얼에 즉시 반영됩니다. 서로 다른 값은 —로 표시되고 수정하지 않은 항목은 각자의 값을 유지하며, Ctrl+Z로 되돌릴 수 있습니다.", "影の強さを揃えるなど、項目を編集するとチェックした全マテリアルへ即座に反映されます。異なる値は—と表示され、未編集の項目は各自の値を保ち、Ctrl+Zで変更を戻せます。"),
                },
            };
        }
        var names = new[] { new[] { "Preset Apply", "프리셋 적용", "プリセット適用" }, new[] { "Diet", "다이어트", "ダイエット" }, new[] { "VRAM Optimize", "VRAM 최적화", "VRAM最適化" }, new[] { "Bulk Edit", "일괄 조절", "一括調整" } };
        int course = (int)_mode;
        _tutorial.Configure(_mode.ToString(), names[course][0], names[course][1], names[course][2], _tutorialCourses[course],
            overviewEn: TutorialOverviews[course][0], overviewKo: TutorialOverviews[course][1], overviewJa: TutorialOverviews[course][2]);
        _tutorial.BeginFrame();
    }

    private void TutorialAnchor(string id) => _tutorial?.Anchor(id, GUILayoutUtility.GetLastRect());
    private void TutorialAnchor(string id, Rect rect) => _tutorial?.Anchor(id, rect);
    private void TutorialDraw(params string[] ids) { foreach (string id in ids) _tutorial?.Draw(id); }
    private void TutorialNotify(string id) => _tutorial?.NotifyAction(id);
}
