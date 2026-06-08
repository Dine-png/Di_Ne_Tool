using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DiNeOpticore))]
public class DiNeOpticoreEditor : Editor
{
    private enum LanguagePreset
    {
        English,
        Korean,
        Japanese,
    }

    private static readonly Color ColAccent = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColGood = new Color(0.22f, 0.70f, 0.36f);
    private static readonly Color ColWarn = new Color(0.90f, 0.66f, 0.20f);
    private static readonly Color ColMuted = new Color(0.58f, 0.58f, 0.63f);
    private static readonly Color ColLine = new Color(0.30f, 0.30f, 0.35f, 0.8f);

    private Texture2D _windowIcon;
    private Font _titleFont;
    private LanguagePreset _language = LanguagePreset.Korean;

    private bool _showGeometry = true;
    private bool _showMaterials = true;
    private bool _showRig = true;
    private bool _showCleanup = true;

    private SerializedProperty _optimizeMeshes;
    private SerializedProperty _optimizeMaterials;
    private SerializedProperty _optimizeRigAndBones;
    private SerializedProperty _optimizePhysBones;
    private SerializedProperty _optimizeAnimator;
    private SerializedProperty _removeUnusedObjects;
    private SerializedProperty _preserveAvatarBehavior;
    private SerializedProperty _experimentalMode;

    private string L(string en, string ko, string ja)
    {
        switch (_language)
        {
            case LanguagePreset.Korean:
                return ko;
            case LanguagePreset.Japanese:
                return ja;
            default:
                return en;
        }
    }

    private void OnEnable()
    {
        _windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
        _titleFont = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");

        _optimizeMeshes = serializedObject.FindProperty("_optimizeMeshes");
        _optimizeMaterials = serializedObject.FindProperty("_optimizeMaterials");
        _optimizeRigAndBones = serializedObject.FindProperty("_optimizeRigAndBones");
        _optimizePhysBones = serializedObject.FindProperty("_optimizePhysBones");
        _optimizeAnimator = serializedObject.FindProperty("_optimizeAnimator");
        _removeUnusedObjects = serializedObject.FindProperty("_removeUnusedObjects");
        _preserveAvatarBehavior = serializedObject.FindProperty("_preserveAvatarBehavior");
        _experimentalMode = serializedObject.FindProperty("_experimentalMode");

        if (EditorPrefs.HasKey("DiNeOpticore_ComponentLang"))
            _language = (LanguagePreset)EditorPrefs.GetInt("DiNeOpticore_ComponentLang");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader();
        DrawLanguageBar();
        DrawHorizontalLine();

        if (!HasRequiredNDMF())
        {
            DrawNDMFRequiredCard();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawOverviewCard();
        DrawHorizontalLine();

        _showGeometry = DrawSectionHeader(
            _showGeometry,
            L("Geometry", "\uC9C0\uC624\uBA54\uD2B8\uB9AC", "\u30B8\u30AA\u30E1\u30C8\u30EA"),
            L(
                "Renderer count, mesh grouping, and draw-side complexity belong here.",
                "\uB80C\uB354\uB7EC \uC218, \uBA54\uC26C \uBB36\uC74C, \uB4DC\uB85C\uC6B0 \uBE44\uC6A9\uACFC \uAC19\uC740 \uD56D\uBAA9\uC785\uB2C8\uB2E4.",
                "\u30EC\u30F3\u30C0\u30E9\u30FC\u6570\u3001\u30E1\u30C3\u30B7\u30E5\u306E\u307E\u3068\u307E\u308A\u3001\u63CF\u753B\u5074\u306E\u8907\u96D1\u3055\u3092\u6271\u3044\u307E\u3059."));
        if (_showGeometry)
        {
            DrawModuleCard(
                _optimizeMeshes,
                L("Optimize Meshes", "\uBA54\uC26C \uCD5C\uC801\uD654", "\u30E1\u30C3\u30B7\u30E5\u6700\u9069\u5316"),
                L(
                    "Reduce renderer-side overhead, freeze safe blendShapes, and clean up mesh data where possible.",
                    "\uB80C\uB354\uB7EC \uC624\uBC84\uD5E4\uB4DC\uB97C \uC904\uC774\uACE0, \uC548\uC804\uD55C BlendShape\uB97C \uD504\uB9AC\uC988\uD558\uBA70, \uBA54\uC26C \uB370\uC774\uD130\uB97C \uC815\uB9AC\uD569\uB2C8\uB2E4.",
                    "\u30EC\u30F3\u30C0\u30E9\u30FC\u5074\u306E\u8CA0\u62C5\u3092\u4E0B\u3052\u3001\u5B89\u5168\u306A BlendShape \u3092\u51CD\u7D50\u3057\u3001\u30E1\u30C3\u30B7\u30E5\u30C7\u30FC\u30BF\u3092\u6574\u7406\u3057\u307E\u3059."),
                L(
                    "Current automatic pass: freezes safe blendShapes, removes zero-sized polygons, removes broken renderer components, and merges skinned meshes that share a skeleton (remapping bone weights into a union bone list and consolidating same-material sub-meshes to cut draw calls). Meshes that still have blendShapes are left unmerged.",
                    "\uD604\uC7AC \uC790\uB3D9 \uD328\uC2A4: \uC548\uC804\uD55C BlendShape \uD504\uB9AC\uC988, \uC81C\uB85C \uC0AC\uC774\uC988 \uD3F4\uB9AC\uACE4 \uC81C\uAC70, \uAE68\uC9C4 \uB80C\uB354\uB7EC \uC81C\uAC70, \uADF8\uB9AC\uACE0 \uAC19\uC740 \uC2A4\uCF08\uB808\uD1A4\uC744 \uACF5\uC720\uD558\uB294 Skinned Mesh \uBCD1\uD569(\uBCF8 \uC6E8\uC774\uD2B8\uB97C union \uBCF8 \uB9AC\uC2A4\uD2B8\uB85C \uC7AC\uB9E4\uD551\uD558\uACE0 \uB3D9\uC77C \uBA38\uD2F0\uB9AC\uC5BC \uC11C\uBE0C\uBA54\uC26C\uB97C \uD569\uCCD0 \uB4DC\uB85C\uC6B0\uCF5C \uAC10\uC18C)\uC744 \uC218\uD589\uD569\uB2C8\uB2E4. BlendShape\uAC00 \uB0A8\uC544\uC788\uB294 \uBA54\uC26C\uB294 \uBCD1\uD569\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.",
                    "\u73FE\u5728\u306E\u81EA\u52D5\u30D1\u30B9: \u5B89\u5168\u306A BlendShape \u306E\u51CD\u7D50\u3001\u30BC\u30ED\u30B5\u30A4\u30BA\u30DD\u30EA\u30B4\u30F3\u306E\u524A\u9664\u3001\u58CA\u308C\u305F\u30EC\u30F3\u30C0\u30E9\u30FC\u306E\u524A\u9664\u3001\u305D\u3057\u3066\u540C\u3058\u30B9\u30B1\u30EB\u30C8\u30F3\u3092\u5171\u6709\u3059\u308B Skinned Mesh \u306E\u7D50\u5408\uFF08\u30DC\u30FC\u30F3\u30A6\u30A7\u30A4\u30C8\u3092\u7D71\u5408\u30DC\u30FC\u30F3\u30EA\u30B9\u30C8\u306B\u518D\u30DE\u30C3\u30D4\u30F3\u30B0\u3057\u3001\u540C\u4E00\u30DE\u30C6\u30EA\u30A2\u30EB\u306E\u30B5\u30D6\u30E1\u30C3\u30B7\u30E5\u3092\u307E\u3068\u3081\u3066\u63CF\u753B\u30B3\u30FC\u30EB\u3092\u524A\u6E1B\uFF09\u3092\u884C\u3044\u307E\u3059\u3002BlendShape \u304C\u6B8B\u3063\u3066\u3044\u308B\u30E1\u30C3\u30B7\u30E5\u306F\u7D50\u5408\u3057\u307E\u305B\u3093\u3002"),
                true);
        }

        _showMaterials = DrawSectionHeader(
            _showMaterials,
            L("Materials", "\uBA38\uD2F0\uB9AC\uC5BC", "\u30DE\u30C6\u30EA\u30A2\u30EB"),
            L(
                "This section covers material slot cleanup. Texture and VRAM work stays in Material Tool.",
                "\uBA38\uD2F0\uB9AC\uC5BC \uC2AC\uB86F \uC815\uB9AC \uC704\uC8FC \uC139\uC158\uC785\uB2C8\uB2E4. \uD14D\uC2A4\uCC98 / VRAM \uC791\uC5C5\uC740 Material Tool\uC5D0 \uB0A8\uAE41\uB2C8\uB2E4.",
                "\u30DE\u30C6\u30EA\u30A2\u30EB\u30B9\u30ED\u30C3\u30C8\u6574\u7406\u306E\u30BB\u30AF\u30B7\u30E7\u30F3\u3067\u3059\u3002\u30C6\u30AF\u30B9\u30C1\u30E3 / VRAM \u8ABF\u6574\u306F Material Tool \u3067\u884C\u3044\u307E\u3059."));
        if (_showMaterials)
        {
            DrawModuleCard(
                _optimizeMaterials,
                L("Optimize Materials", "\uBA38\uD2F0\uB9AC\uC5BC \uCD5C\uC801\uD654", "\u30DE\u30C6\u30EA\u30A2\u30EB\u6700\u9069\u5316"),
                L(
                    "Reorganize duplicate material setup and reduce slot-side waste.",
                    "\uC911\uBCF5 \uBA38\uD2F0\uB9AC\uC5BC \uAD6C\uC131\uC744 \uC815\uB9AC\uD558\uACE0 \uC2AC\uB86F \uB0AD\uBE44\uB97C \uC904\uC785\uB2C8\uB2E4.",
                    "\u91CD\u8907\u3057\u305F\u30DE\u30C6\u30EA\u30A2\u30EB\u69CB\u6210\u3092\u6574\u7406\u3057\u3001\u30B9\u30ED\u30C3\u30C8\u5074\u306E\u7121\u99C4\u3092\u6E1B\u3089\u3057\u307E\u3059."),
                L(
                    "Current automatic pass: trims extra slots, removes empty or unbound submeshes, merges duplicate slots, and clears unused material properties.",
                    "\uD604\uC7AC \uC790\uB3D9 \uD328\uC2A4: \uC5EC\uBD84 \uC2AC\uB86F \uC815\uB9AC, \uBE48 / \uBB34\uD6A8 \uC11C\uBE0C\uBA54\uC26C \uC81C\uAC70, \uC911\uBCF5 \uC2AC\uB86F \uBCD1\uD569, \uBBF8\uC0AC\uC6A9 \uBA38\uD2F0\uB9AC\uC5BC \uD504\uB85C\uD37C\uD2F0 \uC815\uB9AC\uB97C \uC218\uD589\uD569\uB2C8\uB2E4.",
                    "\u73FE\u5728\u306E\u81EA\u52D5\u30D1\u30B9: \u4F59\u5206\u306A\u30B9\u30ED\u30C3\u30C8\u6574\u7406\u3001\u7A7A / \u7121\u52B9\u306A\u30B5\u30D6\u30E1\u30C3\u30B7\u30E5\u306E\u524A\u9664\u3001\u91CD\u8907\u30B9\u30ED\u30C3\u30C8\u306E\u7D71\u5408\u3001\u672A\u4F7F\u7528\u30DE\u30C6\u30EA\u30A2\u30EB\u30D7\u30ED\u30D1\u30C6\u30A3\u306E\u6574\u7406\u3092\u884C\u3044\u307E\u3059."),
                true);

            using (new BackgroundColorScope(ColAccent))
            {
                if (GUILayout.Button(L("Open Material Tool", "Material Tool \uC5F4\uAE30", "Material Tool \u3092\u958B\u304F"), GUILayout.Height(28f)))
                    DiNeMaterialTool.ShowWindow();
            }
        }

        _showRig = DrawSectionHeader(
            _showRig,
            L("Rig, Physics & Motion", "\uB9AC\uADF8, \uBB3C\uB9AC, \uBAA8\uC158", "\u30EA\u30B0\u30FB\u7269\u7406\u30FB\u30E2\u30FC\u30B7\u30E7\u30F3"),
            L(
                "Bones, PhysBones, and animator complexity live here.",
                "\uBCF8, PhysBone, Animator \uBCF5\uC7A1\uB3C4\uB97C \uC5EC\uAE30\uC11C \uB2E4\uB8F9\uB2C8\uB2E4.",
                "\u30DC\u30FC\u30F3\u3001PhysBone\u3001Animator \u306E\u8907\u96D1\u3055\u3092\u3053\u3053\u3067\u6271\u3044\u307E\u3059."));
        if (_showRig)
        {
            DrawModuleCard(
                _optimizeRigAndBones,
                L("Optimize Rig / Bones", "\uB9AC\uADF8 / \uBCF8 \uCD5C\uC801\uD654", "\u30EA\u30B0 / \u30DC\u30FC\u30F3\u6700\u9069\u5316"),
                L(
                    "Reduce unnecessary hierarchy weight while keeping skinning stable.",
                    "\uC2A4\uD0A8\uB2DD \uC548\uC815\uC131\uC744 \uC720\uC9C0\uD558\uBA74\uC11C \uBD88\uD544\uC694\uD55C \uACC4\uCE35 \uBD80\uD558\uB97C \uC904\uC785\uB2C8\uB2E4.",
                    "\u30B9\u30AD\u30CB\u30F3\u30B0\u306E\u5B89\u5B9A\u6027\u3092\u4FDD\u3061\u306A\u304C\u3089\u3001\u4E0D\u8981\u306A\u968E\u5C64\u8CA0\u8377\u3092\u4E0B\u3052\u307E\u3059."),
                L(
                    "Current automatic pass: removes unreferenced leaf bones and collapses safe identity intermediate bones while protecting renderer and humanoid references.",
                    "\uD604\uC7AC \uC790\uB3D9 \uD328\uC2A4: \uB80C\uB354\uB7EC / \uD734\uBA38\uB178\uC774\uB4DC \uCC38\uC870\uB97C \uBCF4\uD638\uD558\uBA74\uC11C \uCC38\uC870\uB418\uC9C0 \uC54A\uB294 leaf bone \uC81C\uAC70\uC640 \uC548\uC804\uD55C identity \uC911\uAC04 bone collapse\uB97C \uC218\uD589\uD569\uB2C8\uB2E4.",
                    "\u73FE\u5728\u306E\u81EA\u52D5\u30D1\u30B9: \u30EC\u30F3\u30C0\u30E9\u30FC / \u30D2\u30E5\u30FC\u30DE\u30CE\u30A4\u30C9\u53C2\u7167\u3092\u4FDD\u8B77\u3057\u306A\u304C\u3089\u3001\u672A\u53C2\u7167\u306E leaf bone \u524A\u9664\u3068\u5B89\u5168\u306A identity \u4E2D\u9593 bone \u306E\u5727\u7E2E\u3092\u884C\u3044\u307E\u3059."),
                true);

            DrawModuleCard(
                _optimizePhysBones,
                L("Optimize PhysBones", "PhysBone \uCD5C\uC801\uD654", "PhysBone \u6700\u9069\u5316"),
                L(
                    "Reduce unnecessary simulation work from chains and colliders.",
                    "\uCCB4\uC778\uACFC \uCF5C\uB77C\uC774\uB354\uC5D0\uC11C \uC0DD\uAE30\uB294 \uBD88\uD544\uC694\uD55C \uC2DC\uBBAC\uB808\uC774\uC158 \uBE44\uC6A9\uC744 \uC904\uC785\uB2C8\uB2E4.",
                    "\u30C1\u30A7\u30FC\u30F3\u3084\u30B3\u30E9\u30A4\u30C0\u30FC\u7531\u6765\u306E\u4E0D\u8981\u306A\u30B7\u30DF\u30E5\u30EC\u30FC\u30B7\u30E7\u30F3\u8CA0\u8377\u3092\u4E0B\u3052\u307E\u3059."),
                L(
                    "Current automatic pass: removes null or duplicate PhysBone collider / ignore entries.",
                    "\uD604\uC7AC \uC790\uB3D9 \uD328\uC2A4: null \uB610\uB294 \uC911\uBCF5 PhysBone collider / ignore \uCC38\uC870\uB97C \uC815\uB9AC\uD569\uB2C8\uB2E4.",
                    "\u73FE\u5728\u306E\u81EA\u52D5\u30D1\u30B9: null \u307E\u305F\u306F\u91CD\u8907\u3057\u305F PhysBone collider / ignore \u53C2\u7167\u3092\u6574\u7406\u3057\u307E\u3059."),
                true);

            DrawModuleCard(
                _optimizeAnimator,
                L("Optimize Animator", "Animator \uCD5C\uC801\uD654", "Animator \u6700\u9069\u5316"),
                L(
                    "Reduce layer, transition, and parameter-side evaluation waste.",
                    "\uB808\uC774\uC5B4, \uC804\uC774, \uD30C\uB77C\uBBF8\uD130 \uD3C9\uAC00 \uBE44\uC6A9\uC744 \uC904\uC785\uB2C8\uB2E4.",
                    "\u30EC\u30A4\u30E4\u30FC\u3001\u9077\u79FB\u3001\u30D1\u30E9\u30E1\u30FC\u30BF\u8A55\u4FA1\u306E\u7121\u99C4\u3092\u6E1B\u3089\u3057\u307E\u3059."),
                L(
                    "Animator optimizer is excluded from the current Opticore port scope.",
                    "Animator \uCD5C\uC801\uD654\uB294 \uD604\uC7AC Opticore \uC774\uC2DD \uBC94\uC704\uC5D0\uC11C \uC81C\uC678\uB418\uC5C8\uC2B5\uB2C8\uB2E4.",
                    "Animator \u6700\u9069\u5316\u306F\u73FE\u5728\u306E Opticore \u79FB\u690D\u7BC4\u56F2\u304B\u3089\u5916\u3057\u3066\u3044\u307E\u3059."),
                false);
        }

        _showCleanup = DrawSectionHeader(
            _showCleanup,
            L("Cleanup & Safety", "\uC815\uB9AC \uBC0F \uC548\uC804", "\u6574\u7406\u3068\u5B89\u5168"),
            L(
                "These safety rules are part of the automatic Opticore pass.",
                "\uC774 \uC548\uC804 \uADDC\uCE59\uB4E4\uC740 Opticore \uC790\uB3D9 \uD328\uC2A4\uC758 \uC77C\uBD80\uC785\uB2C8\uB2E4.",
                "\u3053\u308C\u3089\u306E\u5B89\u5168\u30EB\u30FC\u30EB\u306F Opticore \u306E\u81EA\u52D5\u30D1\u30B9\u306E\u4E00\u90E8\u3067\u3059."));
        if (_showCleanup)
        {
            DrawModuleCard(
                _removeUnusedObjects,
                L("Remove Unused Objects", "\uBBF8\uC0AC\uC6A9 \uC624\uBE0C\uC81D\uD2B8 \uC815\uB9AC", "\u672A\u4F7F\u7528\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u6574\u7406"),
                L(
                    "Remove empty hierarchy objects from the temporary auto-applied avatar without touching the original avatar.",
                    "\uC6D0\uBCF8 \uC544\uBC14\uD0C0\uB294 \uB450\uACE0 \uC784\uC2DC \uC790\uB3D9 \uC801\uC6A9 \uC544\uBC14\uD0C0\uC5D0\uC11C \uBE48 \uACC4\uCE35 \uC624\uBE0C\uC81D\uD2B8\uB97C \uC815\uB9AC\uD569\uB2C8\uB2E4.",
                    "\u5143\u306E\u30A2\u30D0\u30BF\u30FC\u306B\u306F\u89E6\u308C\u305A\u3001\u4E00\u6642\u7684\u306B\u81EA\u52D5\u9069\u7528\u3055\u308C\u305F\u30A2\u30D0\u30BF\u30FC\u306E\u7A7A\u306E\u968E\u5C64\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u3092\u6574\u7406\u3057\u307E\u3059."),
                L(
                    "Expected effect: fewer empty transforms, fewer missing-script leftovers, and a cleaner hierarchy after auto apply.",
                    "\uC608\uC0C1 \uD6A8\uACFC: \uBE48 Transform \uAC10\uC18C, missing script \uC794\uC5EC \uC815\uB9AC, \uC790\uB3D9 \uC801\uC6A9 \uD6C4 \uACC4\uCE35 \uAD6C\uC870 \uC815\uB9AC.",
                    "\u671F\u5F85\u529F\u679C: \u7A7A\u306E Transform \u6E1B\u5C11\u3001missing script \u6B8B\u308A\u306E\u6574\u7406\u3001\u81EA\u52D5\u9069\u7528\u5F8C\u306E\u968E\u5C64\u6574\u7406."),
                true);

            DrawModuleCard(
                _preserveAvatarBehavior,
                L("Preserve Avatar Behavior", "\uC544\uBC14\uD0C0 \uB3D9\uC791 \uC720\uC9C0", "\u30A2\u30D0\u30BF\u30FC\u306E\u632F\u308B\u821E\u3044\u7DAD\u6301"),
                L(
                    "Keep preview changes conservative so the visual test stays trustworthy.",
                    "\uD504\uB9AC\uBDF0 \uBCC0\uACBD\uC744 \uBCF4\uC218\uC801\uC73C\uB85C \uC720\uC9C0\uD574 \uD14C\uC2A4\uD2B8 \uC2E0\uB8B0\uC131\uC744 \uC9C0\uD0B5\uB2C8\uB2E4.",
                    "\u30D7\u30EC\u30D3\u30E5\u30FC\u5909\u66F4\u3092\u4FDD\u5B88\u7684\u306B\u4FDD\u3061\u3001\u30C6\u30B9\u30C8\u306E\u4FE1\u983C\u6027\u3092\u9AD8\u3081\u307E\u3059."),
                L(
                    "Current automatic pass: protects referenced or animated transforms before cleanup runs.",
                    "\uD604\uC7AC \uC790\uB3D9 \uD328\uC2A4: \uC815\uB9AC \uC804\uC5D0 \uCC38\uC870\uB418\uAC70\uB098 \uC560\uB2C8\uBA54\uC774\uC158\uB41C Transform\uB97C \uBCF4\uD638\uD569\uB2C8\uB2E4.",
                    "\u73FE\u5728\u306E\u81EA\u52D5\u30D1\u30B9: \u6574\u7406\u524D\u306B\u53C2\u7167\u3055\u308C\u3066\u3044\u308B / \u30A2\u30CB\u30E1\u30FC\u30B7\u30E7\u30F3\u3055\u308C\u308B Transform \u3092\u4FDD\u8B77\u3057\u307E\u3059."),
                true);

            DrawModuleCard(
                _experimentalMode,
                L("Experimental Mode", "\uC2E4\uD5D8 \uBAA8\uB4DC", "\u5B9F\u9A13\u30E2\u30FC\u30C9"),
                L(
                    "Enables more aggressive experimental passes that need extra QA \u2014 including merging skinned meshes that have blendShapes or are blendShape-animated.",
                    "\uCD94\uAC00 \uAC80\uC218\uAC00 \uD544\uC694\uD55C \uB354 \uACF5\uACA9\uC801\uC778 \uC2E4\uD5D8 \uD328\uC2A4\uB97C \uCF2D\uB2C8\uB2E4 \u2014 BlendShape\uAC00 \uC788\uAC70\uB098 BlendShape\uAC00 \uC560\uB2C8\uBA54\uC774\uC158\uB418\uB294 Skinned Mesh \uBCD1\uD569 \uD3EC\uD568.",
                    "\u8FFD\u52A0QA\u304C\u5FC5\u8981\u306A\u3001\u3088\u308A\u653B\u3081\u305F\u5B9F\u9A13\u30D1\u30B9\u3092\u6709\u52B9\u306B\u3057\u307E\u3059 \u2014 BlendShape \u3092\u6301\u3064 / BlendShape \u304C\u30A2\u30CB\u30E1\u30FC\u30B7\u30E7\u30F3\u3055\u308C\u308B Skinned Mesh \u306E\u7D50\u5408\u3092\u542B\u307F\u307E\u3059\u3002"),
                L(
                    "Current pass: also merges skinned meshes whose only animation is blendShape weights (shapes are carried over and their animations are redirected), and allows more aggressive empty-object cleanup. Not fully verified \u2014 test before shipping.",
                    "\uD604\uC7AC \uD328\uC2A4: \uBE14\uB80C\uB4DC\uC170\uC774\uD504 \uAC00\uC911\uCE58\uB9CC \uC560\uB2C8\uBA54\uC774\uC158\uB418\uB294 Skinned Mesh\uB3C4 \uBCD1\uD569\uD558\uACE0(\uC170\uC774\uD504\uB97C \uC62E\uAE30\uACE0 \uC560\uB2C8\uBA54\uC774\uC158\uC744 \uC7AC\uC5F0\uACB0), \uBE48 \uC624\uBE0C\uC81D\uD2B8 \uC815\uB9AC\uB97C \uB354 \uACF5\uACA9\uC801\uC73C\uB85C \uD5C8\uC6A9\uD569\uB2C8\uB2E4. \uC644\uC804 \uAC80\uC99D \uC804\uC774\uB2C8 \uBC30\uD3EC \uC804 \uD14C\uC2A4\uD2B8\uD558\uC138\uC694.",
                    "\u73FE\u5728\u306E\u30D1\u30B9: BlendShape \u30A6\u30A7\u30A4\u30C8\u306E\u307F\u30A2\u30CB\u30E1\u30FC\u30B7\u30E7\u30F3\u3055\u308C\u308B Skinned Mesh \u3082\u7D50\u5408\u3057\uFF08\u30B7\u30A7\u30A4\u30D7\u3092\u79FB\u3057\u30A2\u30CB\u30E1\u30FC\u30B7\u30E7\u30F3\u3092\u518D\u30EA\u30F3\u30AF\uFF09\u3001\u7A7A\u30AA\u30D6\u30B8\u30A7\u30AF\u30C8\u6574\u7406\u3092\u3088\u308A\u653B\u3081\u3066\u884C\u3048\u307E\u3059\u3002\u672A\u691C\u8A3C\u306E\u305F\u3081\u914D\u5E03\u524D\u306B\u30C6\u30B9\u30C8\u3057\u3066\u304F\u3060\u3055\u3044\u3002"),
                true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool HasRequiredNDMF()
    {
#if DINE_HAS_NDMF
        return true;
#else
        return false;
#endif
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (_windowIcon != null)
            GUILayout.Label(_windowIcon, GUILayout.Width(64f), GUILayout.Height(64f));

        GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
        {
            font = _titleFont,
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        GUILayout.Space(6f);
        GUILayout.Label("Opticore", titleStyle, GUILayout.Height(64f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.Label(
            L(
                "A component-style optimizer for VRChat avatars.",
                "VRChat \uC544\uBC14\uD0C0\uB97C \uC704\uD55C \uCEF4\uD3EC\uB10C\uD2B8\uD615 \uCD5C\uC801\uD654 \uD234\uC785\uB2C8\uB2E4.",
                "VRChat \u30A2\u30D0\u30BF\u30FC\u5411\u3051\u306E\u30B3\u30F3\u30DD\u30FC\u30CD\u30F3\u30C8\u578B\u6700\u9069\u5316\u30C4\u30FC\u30EB\u3067\u3059."),
            new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.82f, 0.82f, 0.82f) }
            });

        EditorGUILayout.EndVertical();
    }

    private void DrawNDMFRequiredCard()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label(L("NDMF Required", "NDMF 필요", "NDMF が必要です"), new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = ColWarn }
        });

        GUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            L(
                "Opticore currently requires the NDMF package to enable its automatic build pipeline. Install `nadena.dev.ndmf` first, then reopen this component.",
                "Opticore\uB294 \uD604\uC7AC \uC790\uB3D9 \uBE4C\uB4DC \uD30C\uC774\uD504\uB77C\uC778\uC744 \uC704\uD574 NDMF \uD328\uD0A4\uC9C0\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4. `nadena.dev.ndmf`\uB97C \uBA3C\uC800 \uC124\uCE58\uD55C \uB4A4 \uC774 \uCEF4\uD3EC\uB10C\uD2B8\uB97C \uB2E4\uC2DC \uC5F4\uC5B4\uC8FC\uC138\uC694.",
                "Opticore \u306F\u73FE\u5728\u3001\u81EA\u52D5\u30D3\u30EB\u30C9\u30D1\u30A4\u30D7\u30E9\u30A4\u30F3\u306E\u305F\u3081\u306B NDMF \u30D1\u30C3\u30B1\u30FC\u30B8\u304C\u5FC5\u8981\u3067\u3059\u3002`nadena.dev.ndmf` \u3092\u5148\u306B\u5C0E\u5165\u3057\u3066\u304B\u3089\u3001\u3053\u306E\u30B3\u30F3\u30DD\u30FC\u30CD\u30F3\u30C8\u3092\u518D\u5EA6\u958B\u3044\u3066\u304F\u3060\u3055\u3044."),
            MessageType.Warning);

        GUILayout.Space(4f);
        GUILayout.Label(
            L(
                "If you install Di Ne Tool through VCC / VPM, NDMF is usually pulled in automatically. Manual package installs may require adding it yourself.",
                "VCC / VPM\uC73C\uB85C Di Ne Tool\uC744 \uC124\uCE58\uD558\uBA74 \uBCF4\uD1B5 NDMF\uAC00 \uD568\uAED8 \uB4E4\uC5B4\uC635\uB2C8\uB2E4. \uC218\uB3D9 \uD328\uD0A4\uC9C0 \uC124\uCE58\uB294 \uBCC4\uB3C4 \uCD94\uAC00\uAC00 \uD544\uC694\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                "VCC / VPM \u3067 Di Ne Tool \u3092\u5C0E\u5165\u3059\u308B\u3068\u3001\u901A\u5E38\u306F NDMF \u3082\u81EA\u52D5\u3067\u5C0E\u5165\u3055\u308C\u307E\u3059\u3002\u624B\u52D5\u3067\u30D1\u30C3\u30B1\u30FC\u30B8\u3092\u5165\u308C\u305F\u5834\u5408\u306F\u3001\u5225\u9014\u8FFD\u52A0\u304C\u5FC5\u8981\u306A\u3053\u3068\u304C\u3042\u308A\u307E\u3059."),
            new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.86f, 0.86f, 0.90f) }
            });
        EditorGUILayout.EndVertical();
    }

    private void DrawLanguageBar()
    {
        int current = (int)_language;
        int next = DrawToolbar(current, new[] { "English", "\uD55C\uAD6D\uC5B4", "\u65E5\u672C\u8A9E" });
        if (next == current)
            return;

        _language = (LanguagePreset)next;
        EditorPrefs.SetInt("DiNeOpticore_ComponentLang", next);
    }

    private void DrawOverviewCard()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label(L("Overview", "\uAC1C\uC694", "\u6982\u8981"), new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = ColAccent }
        });

        GUILayout.Space(3f);
        EditorGUILayout.BeginHorizontal();
        DrawMiniCard(L("Enabled", "\uD65C\uC131", "\u6709\u52B9"), $"{CountEnabledModules()} / 6", ColAccent);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            L(
                "Opticore now runs automatically on temporary Play Mode clones, and on upload through the NDMF build pipeline. The original scene avatar is left untouched.",
                "Opticore\uB294 \uC774\uC81C Play Mode \uC784\uC2DC \uD074\uB860\uACFC NDMF \uBE4C\uB4DC \uD30C\uC774\uD504\uB77C\uC778 \uC5C5\uB85C\uB4DC \uACBD\uB85C\uC5D0\uC11C \uC790\uB3D9 \uC2E4\uD589\uB418\uBA70, \uC6D0\uBCF8 \uC52C \uC544\uBC14\uD0C0\uB294 \uAC74\uB4DC\uB9AC\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.",
                "Opticore \u306F\u4ECA\u5F8C Play Mode \u306E\u4E00\u6642\u30AF\u30ED\u30FC\u30F3\u3068 NDMF \u30D3\u30EB\u30C9\u30D1\u30A4\u30D7\u30E9\u30A4\u30F3\u7D4C\u7531\u306E\u30A2\u30C3\u30D7\u30ED\u30FC\u30C9\u3067\u81EA\u52D5\u5B9F\u884C\u3055\u308C\u3001\u5143\u306E\u30B7\u30FC\u30F3\u30A2\u30D0\u30BF\u30FC\u306F\u5909\u66F4\u3057\u307E\u305B\u3093."),
            MessageType.Info);

        GUILayout.Space(2f);
        GUILayout.Label(
            L(
                "Current automatic coverage: blendShape freeze, zero-sized polygon cleanup, skinned mesh merge with bone remapping, material slot and submesh cleanup, unused material property cleanup, rig cleanup, PhysBone reference cleanup, PhysBone collider dedupe, PhysBone isAnimated cleanup, conservative endpointPosition replacement, missing-script cleanup, and hierarchy cleanup. Animator optimizer is intentionally excluded from this port.",
                "\uD604\uC7AC \uC790\uB3D9 \uC801\uC6A9 \uBC94\uC704: BlendShape \uD504\uB9AC\uC988, \uC81C\uB85C \uC0AC\uC774\uC988 \uD3F4\uB9AC\uACE4 \uC815\uB9AC, \uBCF4\uC218\uC801 Skinned Mesh \uBCD1\uD569, \uBA38\uD2F0\uB9AC\uC5BC \uC2AC\uB86F / \uC11C\uBE0C\uBA54\uC26C \uC815\uB9AC, \uBBF8\uC0AC\uC6A9 \uBA38\uD2F0\uB9AC\uC5BC \uD504\uB85C\uD37C\uD2F0 \uC815\uB9AC, \uB9AC\uADF8 \uC815\uB9AC, PhysBone \uCC38\uC870 \uC815\uB9AC, PhysBone Collider \uC911\uBCF5 \uD1B5\uD569, PhysBone isAnimated \uC815\uB9AC, \uBCF4\uC218\uC801 endpointPosition \uB300\uCCB4, missing script \uC815\uB9AC, \uACC4\uCE35 \uC815\uB9AC. Animator \uCD5C\uC801\uD654\uB294 \uC758\uB3C4\uC801\uC73C\uB85C \uC774 \uC774\uC2DD \uBC94\uC704\uC5D0\uC11C \uC81C\uC678\uD588\uC2B5\uB2C8\uB2E4.",
                "\u73FE\u5728\u306E\u81EA\u52D5\u9069\u7528\u7BC4\u56F2: BlendShape \u306E\u51CD\u7D50\u3001\u30BC\u30ED\u30B5\u30A4\u30BA\u30DD\u30EA\u30B4\u30F3\u6574\u7406\u3001\u4FDD\u5B88\u7684\u306A Skinned Mesh \u7D50\u5408\u3001\u30DE\u30C6\u30EA\u30A2\u30EB\u30B9\u30ED\u30C3\u30C8 / \u30B5\u30D6\u30E1\u30C3\u30B7\u30E5\u6574\u7406\u3001\u672A\u4F7F\u7528\u30DE\u30C6\u30EA\u30A2\u30EB\u30D7\u30ED\u30D1\u30C6\u30A3\u6574\u7406\u3001\u30EA\u30B0\u6574\u7406\u3001PhysBone \u53C2\u7167\u6574\u7406\u3001PhysBone Collider \u306E\u91CD\u8907\u7D71\u5408\u3001PhysBone isAnimated \u6574\u7406\u3001\u4FDD\u5B88\u7684\u306A endpointPosition \u7F6E\u63DB\u3001missing script \u6574\u7406\u3001\u968E\u5C64\u6574\u7406\u3002Animator \u6700\u9069\u5316\u306F\u610F\u56F3\u7684\u306B\u3053\u306E\u79FB\u690D\u7BC4\u56F2\u304B\u3089\u5916\u3057\u3066\u3044\u307E\u3059."),
            new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 10,
                normal = { textColor = ColMuted }
            });

        EditorGUILayout.EndVertical();
    }

    private void DrawModuleCard(SerializedProperty property, string title, string description, string expectedEffect, bool livePreviewReady)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        property.boolValue = EditorGUILayout.ToggleLeft(title, property.boolValue, new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 });
        DrawStatusBadge(
            livePreviewReady
                ? L("Live", "\uC2E4\uD589", "\u5B9F\u884C")
                : L("Soon", "\uC608\uC815", "\u4E88\u5B9A"),
            livePreviewReady ? ColGood : ColWarn);
        EditorGUILayout.EndHorizontal();

        GUILayout.Label(description, new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize = 10,
            normal = { textColor = new Color(0.86f, 0.86f, 0.90f) }
        });

        GUILayout.Space(3f);
        DrawInfoRow(L("Expected effect", "\uC608\uC0C1 \uD6A8\uACFC", "\u671F\u5F85\u529F\u679C"), expectedEffect);
        EditorGUILayout.EndVertical();
    }

    private bool DrawSectionHeader(bool expanded, string title, string subtitle)
    {
        EditorGUILayout.BeginVertical("box");
        expanded = EditorGUILayout.Foldout(expanded, title, true, new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 12,
        });
        GUILayout.Label(subtitle, new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            fontSize = 10,
            normal = { textColor = ColMuted }
        });
        EditorGUILayout.EndVertical();
        return expanded;
    }

    private void DrawInfoRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = ColAccent }
        }, GUILayout.Width(110f));
        GUILayout.Label(value, new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            fontSize = 10,
            normal = { textColor = new Color(0.82f, 0.82f, 0.86f) }
        });
        EditorGUILayout.EndHorizontal();
    }

    private void DrawMiniCard(string title, string value, Color accent)
    {
        EditorGUILayout.BeginVertical("box", GUILayout.MinWidth(90f));
        GUILayout.Label(title, new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = accent }
        });
        GUILayout.Label(value, new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            normal = { textColor = Color.white }
        });
        EditorGUILayout.EndVertical();
    }

    private void DrawStatusBadge(string text, Color color)
    {
        GUILayout.Label(text, new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = color }
        }, GUILayout.Width(40f));
    }

    private int CountEnabledModules()
    {
        int count = 0;
        if (_optimizeMeshes.boolValue) count++;
        if (_optimizeMaterials.boolValue) count++;
        if (_optimizeRigAndBones.boolValue) count++;
        if (_optimizePhysBones.boolValue) count++;
        if (_optimizeAnimator.boolValue) count++;
        if (_removeUnusedObjects.boolValue) count++;
        return count;
    }

    private int DrawToolbar(int selected, string[] options)
    {
        EditorGUILayout.BeginHorizontal();
        int next = selected;
        for (int i = 0; i < options.Length; i++)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = i == selected ? ColAccent : new Color(0.50f, 0.50f, 0.50f, 1f);
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                fontStyle = i == selected ? FontStyle.Bold : FontStyle.Normal,
                fontSize = 12,
                normal = { textColor = i == selected ? Color.white : new Color(0.82f, 0.82f, 0.82f) }
            };
            if (GUILayout.Button(options[i], style, GUILayout.Height(24f)))
                next = i;
            GUI.backgroundColor = previous;
        }
        EditorGUILayout.EndHorizontal();
        return next;
    }

    private static void DrawHorizontalLine()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, ColLine);
        GUILayout.Space(4f);
    }

    private readonly struct BackgroundColorScope : System.IDisposable
    {
        private readonly Color _previous;

        public BackgroundColorScope(Color next)
        {
            _previous = GUI.backgroundColor;
            GUI.backgroundColor = next;
        }

        public void Dispose()
        {
            GUI.backgroundColor = _previous;
        }
    }
}
