#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.BuildPipeline;

public class DiNeMultiDresserAutoApply : IVRCSDKBuildRequestedCallback, IVRCSDKPreprocessAvatarCallback
{
    private const string TempRootFolder = "Assets/Di Ne/MultiDresser/__Temp";
    private const string SessionStateKey = "DiNe.MultiDresser.TempSessions";

    private static readonly Dictionary<int, TemporarySession> ActiveSessions = new Dictionary<int, TemporarySession>();
    private static readonly Dictionary<string, AnimatorControllerParameterType> KnownAnimatorParameters = new Dictionary<string, AnimatorControllerParameterType>
    {
        { "GestureLeft", AnimatorControllerParameterType.Int },
        { "GestureRight", AnimatorControllerParameterType.Int },
        { "GestureLeftWeight", AnimatorControllerParameterType.Float },
        { "GestureRightWeight", AnimatorControllerParameterType.Float },
        { "VRMode", AnimatorControllerParameterType.Int },
        { "Viseme", AnimatorControllerParameterType.Int },
        { "Voice", AnimatorControllerParameterType.Float },
        { "Upright", AnimatorControllerParameterType.Float },
        { "AngularY", AnimatorControllerParameterType.Float },
        { "VelocityX", AnimatorControllerParameterType.Float },
        { "VelocityY", AnimatorControllerParameterType.Float },
        { "VelocityZ", AnimatorControllerParameterType.Float },
        { "VelocityMagnitude", AnimatorControllerParameterType.Float },
        { "Grounded", AnimatorControllerParameterType.Bool },
        { "Seated", AnimatorControllerParameterType.Bool },
        { "AFK", AnimatorControllerParameterType.Bool },
        { "IsLocal", AnimatorControllerParameterType.Bool },
        { "IsOnFriendsList", AnimatorControllerParameterType.Bool },
        { "InStation", AnimatorControllerParameterType.Bool },
        { "MuteSelf", AnimatorControllerParameterType.Bool },
        { "TrackingType", AnimatorControllerParameterType.Int },
        { "AvatarVersion", AnimatorControllerParameterType.Int },
        { "IsAnimatorEnabled", AnimatorControllerParameterType.Bool },
        { "ScaleFactor", AnimatorControllerParameterType.Float },
        { "ScaleFactorInverse", AnimatorControllerParameterType.Float },
        { "EyeHeightAsMeters", AnimatorControllerParameterType.Float },
        { "EyeHeightAsPercent", AnimatorControllerParameterType.Float },
    };
    // 현재 후킹된 SDK 빌더 인스턴스. 단순 bool 가드가 아니라 인스턴스를 추적한다.
    // SDK 패널을 닫았다 다시 열면 빌더가 새로 생성되는데, bool 가드는 한 번 true가 되면
    // 새 빌더를 다시 후킹하지 못해 2번째 이후 업로드에서 종료 콜백이 오지 않는다(복원 누락).
    private static IVRCSdkAvatarBuilderApi hookedBuilder;

    // 빌드/업로드 진행 여부. 플레이 모드 진입 시 발생하는 도메인 리로드에도 살아남도록
    // SessionState에 저장한다. 빌드 중에는 플레이 모드 종료가 더미를 지우지 못하게 막는다.
    private const string BuildInProgressKey = "DiNe.MultiDresser.BuildInProgress";
    private static bool BuildInProgress
    {
        get => SessionState.GetBool(BuildInProgressKey, false);
        set => SessionState.SetBool(BuildInProgressKey, value);
    }

    public int callbackOrder => 0;

    [Serializable]
    private class PersistedState
    {
        public List<PersistedSession> sessions = new List<PersistedSession>();
    }

    [Serializable]
    private class PersistedSession
    {
        public int descriptorStateVersion;
        public string descriptorId;
        public string tempFolderPath;
        public string originalFxControllerPath;
        public string originalMenuPath;
        public string originalParametersPath;
        public bool originalCustomizeAnimationLayers;
        public bool originalFxLayerExisted;
        public bool originalFxLayerWasInBase;
        public int originalFxLayerIndex = -1;
        public bool originalFxLayerIsDefault;
        public List<PersistedDresserBinding> dressers = new List<PersistedDresserBinding>();
    }

    [Serializable]
    private class PersistedDresserBinding
    {
        public string dresserId;
        public string originalAnimatorControllerPath;
        public string originalExpressionsMenuPath;
    }

    private sealed class DresserBinding
    {
        public DiNeMultiDresser Dresser;
        public AnimatorController OriginalAnimatorController;
        public VRCExpressionsMenu OriginalExpressionsMenu;
    }

    private sealed class TemporarySession
    {
        public int DescriptorStateVersion;
        public VRCAvatarDescriptor Descriptor;
        public string TempFolderPath;
        public RuntimeAnimatorController OriginalFxController;
        public VRCExpressionsMenu OriginalMenu;
        public VRCExpressionParameters OriginalParameters;
        public bool OriginalCustomizeAnimationLayers;
        public bool OriginalFxLayerExisted;
        public bool OriginalFxLayerWasInBase;
        public int OriginalFxLayerIndex = -1;
        public bool OriginalFxLayerIsDefault;
        public AnimatorController TempAnimatorController;
        public VRCExpressionsMenu TempExpressionsMenu;
        public VRCExpressionParameters TempExpressionParameters;
        public List<DresserBinding> Dressers = new List<DresserBinding>();
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
    }

    private static void OnBeforeAssemblyReload()
    {
        PersistSessions();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // 실제 SDK 업로드는 플레이 모드를 거치지 않는다. 따라서 플레이 모드로 진입하는
            // 시점에 BuildInProgress가 켜져 있다면, 종료 콜백을 보내지 않는 테스트 툴
            // (Av3Emulator/Gesture Manager 등)이 남긴 잔여 상태다. 그대로 두면 플레이 모드
            // 종료 후 복원이 영구히 막히므로 여기서 내려준다.
            BuildInProgress = false;

            // Apply temporary FX/Menu assets before Unity clones the scene for play mode.
            // EnteredPlayMode is too late for tools that cache avatar controllers during startup.
            ApplyAllDressersInScene();
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // 이제 씬은 Unity가 만든 플레이 모드 클론이다. 이 시점에는 원본 에셋을 건드리지 않고
            // Poiyomi 키워드/Animated 태그와 lilToon 색 보정 정규화를 안전하게 적용할 수 있다.
            NormalizeLightingForPlayMode();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += () => TryRestoreIfIdle("play mode ended");
        }
    }

    private static void NormalizeLightingForPlayMode()
    {
        var descriptors = UnityEngine.Object.FindObjectsOfType<VRCAvatarDescriptor>(true)
            .Where(descriptor => descriptor != null && descriptor.gameObject.scene.IsValid() &&
                !EditorUtility.IsPersistent(descriptor));

        foreach (var descriptor in descriptors)
        {
            if (!descriptor.GetComponentsInChildren<DiNeLightingDesigner>(true)
                .Any(designer => designer != null && designer.enabled))
                continue;

            try
            {
                DiNeLightingBaker.NormalizeForPlayMode(descriptor.gameObject);
            }
            catch (Exception e)
            {
                // The temporary FX curves already expect normalized textures.
                // Leaving them on unbaked materials would show a corrupt preview.
                // Exiting play mode lets the existing edit-mode hook restore FX
                // and remove all temporary assets after the play clones are gone.
                Debug.LogError(
                    $"[DiNe 라이팅 디자이너] '{descriptor.name}'의 머티리얼 정규화에 실패하여 플레이 모드 미리보기를 중단합니다: {e.Message}\n{e.StackTrace}",
                    descriptor);
                EditorApplication.isPlaying = false;
                return;
            }
        }
    }

    // 업로드 흐름(씬 원본은 건드리지 않는다):
    //   OnBuildRequested          : 이전 잔여 세션 정리, 빌드 진행 플래그만 켠다.
    //   Preprocess(-12000, 클론)   : SDK가 만든 빌드 클론에만 임시 FX/메뉴/파라미터를 만들어 붙인다.
    //   Preprocess(0, 클론)        : 라이팅 디자이너 머티리얼 정규화(NDMF/MA가 머티리얼을 바꾼 뒤).
    //   Postprocess / 빌더 종료    : 클론은 SDK가 버리므로 임시 폴더만 지우면 끝. 복원할 게 없다.
    // 씬 아바타에 직접 적용·복원하는 방식은 플레이 모드 테스트(ExitingEditMode)에서만 쓴다.
    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType != VRCSDKRequestedBuildType.Avatar)
            return true;

        try
        {
            // 플레이 모드 테스트 등이 남긴 임시 세션이 씬에 남아 있으면 먼저 원본으로 되돌린다.
            // 그래야 클론이 진짜 원본을 복사해 오고, 더미 위에 더미가 쌓이지 않는다.
            BuildInProgress = false;
            RestoreAllSessions("before build");

            BuildInProgress = true;
            EnsureBuilderHooks();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Failed to prepare Multi Dresser build: {e.Message}\n{e.StackTrace}");
        }

        return true;
    }

    /// <summary>
    /// 빌드 클론에 임시 세션을 만들어 붙인다(callbackOrder -12000 훅에서 호출).
    /// NDMF의 본 처리(-11000)는 시작하자마자 EditorOnly 태그 오브젝트를 파괴하는데, Multi Dresser
    /// 오브젝트는 DiNeMultiCleaner가 EditorOnly 태그를 강제하므로 그보다 먼저 돌아야 드레서가 살아 있다.
    /// 또 이 순서여야 Modular Avatar 등이 우리 레이어/메뉴/파라미터까지 함께 병합해 준다.
    /// </summary>
    internal static void ApplyToBuildAvatar(GameObject avatarGameObject)
    {
        try
        {
            // 플레이 모드에서 호출되는 preprocess는 테스트 툴(Av3Emulator/Gesture Manager 등)이
            // 부르는 것으로, SDK 빌드 종료 콜백이 오지 않는다. 이때 BuildInProgress를 켜면
            // 플레이 모드 종료 후 복원이 영구히 막히므로, 에디트 모드(실제 업로드)에서만 켠다.
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                BuildInProgress = true;
                EnsureBuilderHooks();
            }

            // 플레이 모드에서는 ExitingEditMode에서 씬 아바타에 이미 적용돼 있으므로
            // ApplyTemporarySession의 활성 세션 가드가 중복 적용을 막는다.
            ApplyDressersForAvatarRoot(avatarGameObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Failed to apply temporary Multi Dresser data to build avatar: {e.Message}\n{e.StackTrace}");
        }
    }

    public bool OnPreprocessAvatar(GameObject avatarGameObject)
    {
        try
        {
            // 여기서 넘어오는 avatarGameObject는 SDK가 만든 빌드 클론이므로,
            // 렌더러의 머티리얼을 갈아끼워도 씬의 원본 아바타는 영향을 받지 않는다.
            DiNeLightingBaker.NormalizeForBuild(avatarGameObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Avatar build stopped because material normalization failed: {e.Message}\n{e.StackTrace}");
            OnAvatarBuildFailed();
            return false;
        }

        return true;
    }

    private static void EnsureBuilderHooks()
    {
        if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder) || builder == null)
            return;

        // 이미 같은 빌더 인스턴스에 후킹돼 있으면 중복 후킹 방지.
        if (ReferenceEquals(builder, hookedBuilder))
            return;

        // 빌더가 새로 생성됐다면(패널 재오픈 등) 죽었을 수 있는 이전 빌더의 후킹을 정리한 뒤
        // 현재 빌더에 다시 후킹한다. 이렇게 해야 매 업로드마다 종료 콜백이 확실히 도착해
        // 임시(__Temp) 에셋이 원본으로 복원된다.
        if (hookedBuilder != null)
        {
            try
            {
                hookedBuilder.OnSdkBuildFinish -= OnBuildEnded;
                hookedBuilder.OnSdkBuildError -= OnBuildEnded;
                hookedBuilder.OnSdkUploadFinish -= OnBuildEnded;
                hookedBuilder.OnSdkUploadError -= OnBuildEnded;
            }
            catch
            {
                // 이전 빌더가 이미 파괴된 경우 등은 무시한다.
            }
        }

        builder.OnSdkBuildFinish += OnBuildEnded;
        builder.OnSdkBuildError += OnBuildEnded;
        builder.OnSdkUploadFinish += OnBuildEnded;
        builder.OnSdkUploadError += OnBuildEnded;
        hookedBuilder = builder;
    }

    // 인스펙터의 재배정(↺) 버튼 등에서 호출한다. 빌더 종료 콜백이 누락되어 임시(__Temp)
    // 에셋이 아바타에 그대로 남은 경우, 영속 세션에 저장된 '진짜 원본'으로 강제 복원한다.
    // 복원할 세션이 없으면 아무 일도 하지 않으므로 항상 호출해도 안전하다.
    public static void ForceRestoreNow(string reason)
    {
        // 빌드가 비정상 종료되어 플래그가 묶여 있으면(이 상태가 복원을 막는다) 해제한다.
        BuildInProgress = false;

        try
        {
            RestoreAllSessions(reason);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Multi Dresser 수동 복원 중 예외: {e.Message}\n{e.StackTrace}");
        }
    }

    private static void OnBuildEnded(object sender, string _)
    {
        BuildInProgress = false;
        EditorApplication.delayCall += () => TryRestoreIfIdle("build/upload finished");
    }

    // SDK가 아바타 번들을 다 만든 직후(업로드 전) 호출된다. 번들은 이미 파일로 나왔으므로
    // 임시 에셋을 지워도 안전하다. 빌더 이벤트(OnSdkBuildFinish 등)를 못 받는 경우
    // (패널 밖에서 빌드가 시작됐거나 후킹 전에 빌드된 경우)에도 정리가 되도록 한 겹 더 둔다.
    internal static void OnAvatarBuildPostprocessed()
    {
        BuildInProgress = false;
        EditorApplication.delayCall += () => TryRestoreIfIdle("avatar build postprocess");
    }

    // A rejected preprocess callback may never reach SDK postprocess, and test
    // tools may not dispatch builder events. Clear the guard here and clean up
    // after the SDK has unwound; play-mode sessions still wait for edit mode.
    internal static void OnAvatarBuildFailed()
    {
        BuildInProgress = false;
        EditorApplication.delayCall += () => TryRestoreIfIdle("avatar preprocess failed");
    }

    // 더미는 (1) 빌드/업로드가 끝나고 (2) 플레이 모드도 아닐 때만 안전하게 제거할 수 있다.
    // 둘 중 하나라도 진행 중이면 그 작업이 더미를 참조하고 있을 수 있으므로 복원을 미룬다.
    // 미뤄진 복원은 나머지 조건이 풀리는 이벤트(빌드 종료 / 플레이 모드 종료)에서 다시 시도된다.
    private static void TryRestoreIfIdle(string reason)
    {
        if (BuildInProgress)
            return;

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        RestoreAllSessions(reason);
    }

    private static void ApplyAllDressersInScene()
    {
        RestoreAllSessions("refresh temporary session");

        var dressers = UnityEngine.Object.FindObjectsOfType<DiNeMultiDresser>()
            .Where(dresser => dresser != null && dresser.gameObject.activeInHierarchy)
            .ToArray();

        var smartToggles = UnityEngine.Object.FindObjectsOfType<DiNeSmartToggle>(true)
            .Where(toggle => toggle != null && toggle.enabled &&
                toggle.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(toggle))
            .ToArray();

        var lightingDesigners = UnityEngine.Object.FindObjectsOfType<DiNeLightingDesigner>(true)
            .Where(designer => designer != null && designer.enabled &&
                designer.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(designer))
            .ToArray();

        if (dressers.Length == 0 && smartToggles.Length == 0 && lightingDesigners.Length == 0)
            return;

        var groups = new Dictionary<VRCAvatarDescriptor, List<DiNeMultiDresser>>();
        foreach (var dresser in dressers)
        {
            dresser.TryAutoAssignFXController();

            var descriptor = FindDescriptor(dresser);
            if (descriptor == null)
            {
                Debug.LogWarning($"[DiNe] Skipping '{dresser.name}' because no VRCAvatarDescriptor was found.");
                continue;
            }

            if (!groups.TryGetValue(descriptor, out var groupedDressers))
            {
                groupedDressers = new List<DiNeMultiDresser>();
                groups.Add(descriptor, groupedDressers);
            }

            groupedDressers.Add(dresser);
        }

        var toggleGroups = new Dictionary<VRCAvatarDescriptor, List<DiNeSmartToggle>>();
        foreach (var smartToggle in smartToggles)
        {
            // GetComponentInParent()는 비활성 오브젝트(부모가 꺼진 경우 포함)에서 null을 돌려준다.
            // 위에서 비활성 토글도 수집했으므로 반드시 includeInactive로 찾아야 한다.
            var descriptor = smartToggle.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (descriptor == null)
            {
                Debug.LogWarning($"[DiNe] Skipping Smart Toggle '{smartToggle.name}' because no VRCAvatarDescriptor was found.");
                continue;
            }

            if (!toggleGroups.TryGetValue(descriptor, out var groupedToggles))
            {
                groupedToggles = new List<DiNeSmartToggle>();
                toggleGroups.Add(descriptor, groupedToggles);
            }
            groupedToggles.Add(smartToggle);
        }

        var lightingGroups = new Dictionary<VRCAvatarDescriptor, List<DiNeLightingDesigner>>();
        foreach (var designer in lightingDesigners)
        {
            // 비활성 오브젝트 아래에 있는 디자이너도 설치 대상이므로 includeInactive로 찾는다.
            var descriptor = designer.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (descriptor == null)
            {
                Debug.LogWarning($"[DiNe] Skipping Lighting Designer '{designer.name}' because no VRCAvatarDescriptor was found.");
                continue;
            }

            if (!lightingGroups.TryGetValue(descriptor, out var groupedDesigners))
            {
                groupedDesigners = new List<DiNeLightingDesigner>();
                lightingGroups.Add(descriptor, groupedDesigners);
            }
            groupedDesigners.Add(designer);
        }

        var descriptors = new HashSet<VRCAvatarDescriptor>(groups.Keys);
        descriptors.UnionWith(toggleGroups.Keys);
        descriptors.UnionWith(lightingGroups.Keys);
        foreach (var descriptor in descriptors)
        {
            groups.TryGetValue(descriptor, out var groupedDressers);
            toggleGroups.TryGetValue(descriptor, out var groupedToggles);
            lightingGroups.TryGetValue(descriptor, out var groupedDesigners);
            ApplyTemporarySession(
                descriptor,
                groupedDressers ?? new List<DiNeMultiDresser>(),
                groupedToggles ?? new List<DiNeSmartToggle>(),
                groupedDesigners ?? new List<DiNeLightingDesigner>());
        }
    }

    private static void ApplyDressersForAvatarRoot(GameObject avatarGameObject)
    {
        if (avatarGameObject == null)
            return;

        var descriptor = avatarGameObject.GetComponent<VRCAvatarDescriptor>()
            ?? avatarGameObject.GetComponentInChildren<VRCAvatarDescriptor>(true);
        if (descriptor == null)
            return;

        var dressers = avatarGameObject.GetComponentsInChildren<DiNeMultiDresser>(true)
            .Where(dresser => dresser != null && dresser.enabled)
            .ToList();
        var smartToggles = avatarGameObject.GetComponentsInChildren<DiNeSmartToggle>(true)
            .Where(toggle => toggle != null && toggle.enabled)
            .ToList();
        var lightingDesigners = avatarGameObject.GetComponentsInChildren<DiNeLightingDesigner>(true)
            .Where(designer => designer != null && designer.enabled)
            .ToList();
        if (dressers.Count == 0 && smartToggles.Count == 0 && lightingDesigners.Count == 0)
            return;

        Debug.Log($"[DiNe] Applying avatar tools to '{avatarGameObject.name}': {dressers.Count} dresser(s), {smartToggles.Count} smart toggle(s), {lightingDesigners.Count} lighting designer(s).");

        foreach (var dresser in dressers)
        {
            dresser.TryAutoAssignFXController();
        }

        ApplyTemporarySession(descriptor, dressers, smartToggles, lightingDesigners);
    }

    private static void ApplyTemporarySession(
        VRCAvatarDescriptor descriptor,
        List<DiNeMultiDresser> dressers,
        List<DiNeSmartToggle> smartToggles,
        List<DiNeLightingDesigner> lightingDesigners)
    {
        if (descriptor == null || dressers == null || smartToggles == null || lightingDesigners == null ||
            (dressers.Count == 0 && smartToggles.Count == 0 && lightingDesigners.Count == 0))
            return;

        // 플레이 모드 진입 등으로 도메인이 리로드되면 in-memory ActiveSessions가 비워진다.
        // 그 상태로 중복 적용 가드가 무력화되는 것을 막기 위해, 영속 세션을 먼저 복원한다.
        HydrateActiveSessionsFromPersisted();

        // 같은 디스크립터에 임시 세션이 이미 적용돼 있으면(OnBuildRequested에서 스왑 후
        // OnBuildPreprocess가 같은 씬 오브젝트로 다시 호출되는 경우) 중복 적용하지 않는다.
        // 그렇지 않으면 이미 교체된 '임시 FX/메뉴/파라미터'를 원본으로 잘못 캡처하고
        // 기존 세션(진짜 원본)을 덮어써, 복원 시 원래 데이터가 영구히 사라진다.
        if (ActiveSessions.TryGetValue(descriptor.GetInstanceID(), out var existing) && existing != null)
        {
            Debug.Log($"[DiNe] Multi Dresser: '{descriptor.name}'에 임시 세션이 이미 활성화돼 있어 중복 적용을 건너뜁니다.");
            return;
        }

        // 활성 세션은 못 찾았는데 디스크립터가 이미 더미(임시) 에셋을 가리키고 있으면
        // (클론으로 호출됐거나 영속 세션 복원이 실패한 경우 등), 그 더미를 '원본'으로
        // 캡처하면 안 된다. 캡처하면 복원 시 원본이 더미를 가리켜 missing이 된다.
        // 이미 적용된 상태이므로 그냥 건너뛴다(진짜 원본은 영속 세션이 보존).
        if (DescriptorPointsToTempAsset(descriptor))
        {
            // 다만 그 임시 폴더를 책임지는 세션이 아예 없으면(유니티 재시작/크래시로 세션 기록이
            // 날아간 '고아' 상태) 여기서 건너뛰는 순간 아바타는 영원히 더미를 가리킨 채 남고,
            // 업로드할 때마다 아무것도 설치되지 않는다. 임시 폴더에 남겨둔 매니페스트로 원본을
            // 되돌린 뒤 정상 경로로 계속 진행하고, 매니페스트조차 없으면(구버전이 남긴 폴더)
            // 지금 가리키는 에셋을 기준으로 진행한다. 진짜 세션이 살아 있을 때만 건너뛴다.
            if (!TryRecoverOrphanTempSession(descriptor))
            {
                Debug.LogWarning($"[DiNe] Multi Dresser: '{descriptor.name}'가 이미 임시(_DiNe) 에셋을 가리키고 있어 중복 적용을 건너뜁니다. 진짜 원본은 영속 세션에 보존됩니다.");
                return;
            }
        }

        var session = CreateTemporarySession(descriptor, dressers);
        if (session == null)
            return;

        ActiveSessions[descriptor.GetInstanceID()] = session;
        PersistSessions();

        for (int i = 0; i < session.Dressers.Count; i++)
        {
            var binding = session.Dressers[i];
            bool clearExistingGeneratedData = i == 0;
            GenerateDresser(binding.Dresser, session.TempFolderPath, clearExistingGeneratedData);
        }

        if (smartToggles.Count > 0)
        {
            DiNeSmartToggleGenerator.ApplyToTemporaryAvatar(
                descriptor,
                session.TempAnimatorController,
                session.TempExpressionsMenu,
                session.TempExpressionParameters,
                smartToggles,
                session.TempFolderPath + "/SmartToggle");
        }

        if (lightingDesigners.Count > 0)
        {
            DiNeLightingGenerator.ApplyToTemporaryAvatar(
                descriptor,
                session.TempAnimatorController,
                session.TempExpressionsMenu,
                session.TempExpressionParameters,
                lightingDesigners,
                session.TempFolderPath + "/LightingDesigner");
        }
    }

    // 디스크립터의 FX/메뉴/파라미터 중 하나라도 임시(__Temp) 에셋을 가리키면 true.
    // 이미 더미가 적용된 상태라는 뜻이므로 다시 캡처하면 안 된다.
    private static bool DescriptorPointsToTempAsset(VRCAvatarDescriptor descriptor)
    {
        if (descriptor == null)
            return false;

        return IsTempAsset(GetDescriptorFxController(descriptor))
            || IsTempAsset(descriptor.expressionsMenu)
            || IsTempAsset(descriptor.expressionParameters);
    }

    // 임시 세션 폴더에 함께 저장하는 원본 참조 매니페스트. SessionState는 유니티를 껐다 켜면
    // 사라지기 때문에, 그 상태에서 더미만 남으면 원본을 되돌릴 방법이 없어진다.
    private const string SessionManifestFileName = "__DiNeOriginals.json";

    private static string GetSessionManifestPath(string sessionFolder)
        => string.IsNullOrEmpty(sessionFolder) ? null : sessionFolder + "/" + SessionManifestFileName;

    private static void WriteSessionManifest(TemporarySession session)
    {
        if (session == null || string.IsNullOrEmpty(session.TempFolderPath))
            return;

        try
        {
            File.WriteAllText(GetSessionManifestPath(session.TempFolderPath),
                JsonUtility.ToJson(ToPersistedSession(session), true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DiNe] Multi Dresser: 임시 세션 매니페스트 기록 실패: {e.Message}");
        }
    }

    // 에셋 경로가 임시 폴더 안이면 그 세션 폴더(= __Temp/<세션>)를 돌려준다.
    private static string GetSessionFolderOf(UnityEngine.Object asset)
        => GetSessionFolderOfPath(AssetDatabase.GetAssetPath(asset));

    private static string GetSessionFolderOfPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        path = path.Replace('\\', '/');
        if (!path.StartsWith(TempRootFolder + "/", StringComparison.Ordinal))
            return null;

        string rest = path.Substring(TempRootFolder.Length + 1);
        int slash = rest.IndexOf('/');
        return slash < 0 ? null : TempRootFolder + "/" + rest.Substring(0, slash);
    }

    private enum OriginalKind { Fx, Menu, Parameters }

    private static bool IsTempPath(string path)
        => !string.IsNullOrEmpty(path) && path.Replace('\\', '/').StartsWith(TempRootFolder, StringComparison.Ordinal);

    /// <summary>
    /// 세션이 기록한 '원본' 경로가 사실은 이전 빌드의 더미(__Temp)라면, 그 더미 폴더의 매니페스트를
    /// 따라가며 진짜 원본을 찾는다. 매니페스트가 끊긴 구버전 폴더에 닿으면 파일명에서 _DiNe 접미사를
    /// 벗겨 프로젝트에서 같은 이름의 에셋을 찾아본다.
    /// 반환값: __Temp 밖의 경로, 원래부터 비어 있던 슬롯이면 빈 문자열, 못 찾으면 null.
    /// </summary>
    private static string ResolveTrueOriginalPath(string path, OriginalKind kind)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        string current = path.Replace('\\', '/');
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (IsTempPath(current))
        {
            if (!visited.Add(current))
                return null;

            string folder = GetSessionFolderOfPath(current);
            string manifestPath = GetSessionManifestPath(folder);
            PersistedSession manifest = null;
            if (!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath))
            {
                try { manifest = JsonUtility.FromJson<PersistedSession>(File.ReadAllText(manifestPath)); }
                catch { manifest = null; }
            }

            if (manifest == null)
                return GuessOriginalPathByName(current, kind);

            string next;
            switch (kind)
            {
                case OriginalKind.Fx: next = manifest.originalFxControllerPath; break;
                case OriginalKind.Menu: next = manifest.originalMenuPath; break;
                default: next = manifest.originalParametersPath; break;
            }

            // 매니페스트가 있는데 비어 있으면 그 슬롯은 원래 비어 있던 것이다.
            if (string.IsNullOrEmpty(next))
                return string.Empty;

            current = next.Replace('\\', '/');
        }

        return current;
    }

    // 더미는 '원본이름_DiNe(_DiNe...)( N)' 꼴로 이름 붙는다. 접미사를 벗겨 원본 후보를 찾는다.
    // 후보가 정확히 하나일 때만 인정한다(잘못된 에셋을 원본으로 박아 넣는 것이 더 큰 사고다).
    private static string GuessOriginalPathByName(string tempPath, OriginalKind kind)
    {
        string name = Path.GetFileNameWithoutExtension(tempPath);
        string extension = Path.GetExtension(tempPath);

        bool changed = true;
        while (changed)
        {
            changed = false;
            string trimmed = name.TrimEnd();
            int space = trimmed.LastIndexOf(' ');
            if (space > 0 && trimmed.Substring(space + 1).All(char.IsDigit))
            {
                name = trimmed.Substring(0, space);
                changed = true;
                continue;
            }
            if (name.EndsWith("_DiNe", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - 5);
                changed = true;
            }
        }

        // 원본이 없어서 fallback 이름으로 만들어진 더미면 원래 슬롯은 비어 있던 것이다.
        if (name == "FX" || name == "ExpressionsMenu" || name == "ExpressionParameters" || name.Length == 0)
            return string.Empty;

        string typeFilter;
        switch (kind)
        {
            case OriginalKind.Fx: typeFilter = "t:AnimatorController"; break;
            case OriginalKind.Menu: typeFilter = "t:VRCExpressionsMenu"; break;
            default: typeFilter = "t:VRCExpressionParameters"; break;
        }

        var candidates = AssetDatabase.FindAssets($"{name} {typeFilter}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(candidate => !string.IsNullOrEmpty(candidate) && !IsTempPath(candidate))
            .Where(candidate => string.Equals(Path.GetFileNameWithoutExtension(candidate), name, StringComparison.Ordinal))
            .Where(candidate => string.Equals(Path.GetExtension(candidate), extension, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        if (candidates.Count == 1)
        {
            Debug.LogWarning($"[DiNe] Multi Dresser: 더미 '{tempPath}'의 원본 기록이 없어 이름으로 추정한 원본을 씁니다: {candidates[0]}");
            return candidates[0];
        }

        Debug.LogError(
            $"[DiNe] Multi Dresser: 더미 '{tempPath}'의 진짜 원본을 찾지 못했습니다(이름 '{name}' 후보 {candidates.Count}개). " +
            "FX 컨트롤러 / Expressions 메뉴 / 파라미터를 직접 원본으로 지정하세요.");
        return null;
    }

    /// <summary>
    /// 원본 후보(오브젝트 또는 경로)를 진짜 원본으로 확정한다. 더미면 체인을 따라가 되돌린다.
    /// 반환값: 이 슬롯을 복원해도 되면 true(asset은 null일 수 있다 = 원래 빈 슬롯).
    /// </summary>
    private static bool TryResolveOriginal<T>(UnityEngine.Object current, string path, OriginalKind kind, out T asset)
        where T : UnityEngine.Object
    {
        asset = null;

        if (current != null && !IsTempAsset(current))
        {
            asset = current as T;
            return asset != null;
        }

        string source = current != null ? AssetDatabase.GetAssetPath(current) : path;
        if (string.IsNullOrEmpty(source))
            return true;   // 원래 비어 있던 슬롯.

        string resolved = ResolveTrueOriginalPath(source, kind);
        if (resolved == null)
            return false;
        if (resolved.Length == 0)
            return true;

        asset = AssetDatabase.LoadAssetAtPath<T>(resolved);
        return asset != null;
    }

    // 이 임시 폴더를 책임지는 세션(메모리/영속)이 하나도 없으면 고아다.
    private static bool IsOrphanTempFolder(string sessionFolder)
    {
        if (string.IsNullOrEmpty(sessionFolder))
            return false;

        if (ActiveSessions.Values.Any(item =>
                item != null && string.Equals(item.TempFolderPath, sessionFolder, StringComparison.Ordinal)))
            return false;

        var persisted = LoadPersistedState();
        if (persisted.sessions != null && persisted.sessions.Any(item =>
                item != null && string.Equals(item.tempFolderPath, sessionFolder, StringComparison.Ordinal)))
            return false;

        return true;
    }

    // 고아 임시 세션을 매니페스트로 되돌린다.
    // true를 돌려주면 정상 경로로 계속 진행해도 된다는 뜻이다:
    //  - 매니페스트로 원본을 되돌렸거나,
    //  - 되돌릴 기록이 없는 고아 폴더뿐이라 보호할 '진짜 세션'이 없는 경우.
    //    (이때는 지금 가리키는 에셋을 기준으로 진행한다. 건너뛰면 업로드마다 아무것도 설치되지 않는다.)
    // false는 살아 있는 세션이 그 폴더를 책임지고 있어 다시 캡처하면 원본이 덮어써지는 경우다.
    private static bool TryRecoverOrphanTempSession(VRCAvatarDescriptor descriptor)
    {
        if (descriptor == null)
            return false;

        var folders = new[]
            {
                GetSessionFolderOf(GetDescriptorFxController(descriptor)),
                GetSessionFolderOf(descriptor.expressionsMenu),
                GetSessionFolderOf(descriptor.expressionParameters)
            }
            .Where(folder => !string.IsNullOrEmpty(folder))
            .Distinct()
            .ToList();

        bool ownedByLiveSession = false;

        foreach (var folder in folders)
        {
            if (!IsOrphanTempFolder(folder))
            {
                ownedByLiveSession = true;
                continue;
            }

            string manifestPath = GetSessionManifestPath(folder);
            if (!File.Exists(manifestPath))
            {
                if (TryRestoreDescriptorByGuess(descriptor))
                {
                    Debug.Log($"[DiNe] Multi Dresser: 원본 기록이 없는 임시 폴더({folder})의 더미를 이름 추정으로 원본에 되돌렸습니다.");
                }
                else
                {
                    Debug.LogWarning(
                        $"[DiNe] Multi Dresser: '{descriptor.name}'가 남겨진 임시 폴더({folder})를 가리키는데 원본 기록이 없어 " +
                        "지금 가리키는 에셋을 기준으로 계속 진행합니다. 업로드는 정상 동작하지만, " +
                        "FX 컨트롤러 / Expressions 메뉴 / 파라미터를 직접 원본으로 되돌리는 것을 권장합니다.");
                }
                continue;
            }

            PersistedSession recovered = null;
            try
            {
                recovered = JsonUtility.FromJson<PersistedSession>(File.ReadAllText(manifestPath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[DiNe] Multi Dresser: 임시 세션 매니페스트를 읽지 못했습니다({manifestPath}): {e.Message}");
                continue;
            }

            if (recovered == null)
                continue;

            Debug.Log($"[DiNe] Multi Dresser: 남겨진 임시 세션을 발견해 원본으로 복구합니다: {folder}");
            RestorePersistedSession(recovered);
        }

        return !ownedByLiveSession;
    }

    // 디스크립터가 들고 있는 더미 참조를 더미 체인/이름 추정으로 진짜 원본에 되돌린다.
    // 세 슬롯 모두 확정됐을 때만 적용한다(반쯤 되돌리면 상태가 더 꼬인다).
    private static bool TryRestoreDescriptorByGuess(VRCAvatarDescriptor descriptor)
    {
        if (descriptor == null)
            return false;

        if (!TryResolveOriginal(GetDescriptorFxController(descriptor), null, OriginalKind.Fx, out RuntimeAnimatorController fx))
            return false;
        if (!TryResolveOriginal(descriptor.expressionsMenu, null, OriginalKind.Menu, out VRCExpressionsMenu menu))
            return false;
        if (!TryResolveOriginal(descriptor.expressionParameters, null, OriginalKind.Parameters, out VRCExpressionParameters parameters))
            return false;

        SetDescriptorFxController(descriptor, fx);
        descriptor.expressionsMenu = menu;
        descriptor.expressionParameters = parameters;
        EditorUtility.SetDirty(descriptor);
        MarkSceneDirty(descriptor);

        // 같은 더미를 드레서가 들고 있으면 함께 되돌린다.
        foreach (var dresser in descriptor.GetComponentsInChildren<DiNeMultiDresser>(true))
        {
            if (dresser == null) continue;
            bool dirty = false;
            if (IsTempAsset(dresser.animatorController) && fx is AnimatorController fxAc) { dresser.animatorController = fxAc; dirty = true; }
            if (IsTempAsset(dresser.expressionsMenu)) { dresser.expressionsMenu = menu; dirty = true; }
            if (dirty) EditorUtility.SetDirty(dresser);
        }

        return !DescriptorPointsToTempAsset(descriptor);
    }

    // 도메인 리로드로 비워진 ActiveSessions를, SessionState에 영속된 세션 정보로부터
    // 복원한다. 진짜 원본 참조(경로/GlobalObjectId)를 다시 로드하므로, 더미가 원본으로
    // 잘못 캡처되는 것을 막고 중복 적용 가드가 리로드 이후에도 동작하게 한다.
    private static void HydrateActiveSessionsFromPersisted()
    {
        if (ActiveSessions.Count > 0)
            return;

        var persisted = LoadPersistedState();
        if (persisted.sessions == null || persisted.sessions.Count == 0)
            return;

        foreach (var ps in persisted.sessions)
        {
            var descriptor = LoadSceneObject<VRCAvatarDescriptor>(ps.descriptorId);
            if (descriptor == null)
                continue;

            var session = new TemporarySession
            {
                DescriptorStateVersion = ps.descriptorStateVersion,
                Descriptor = descriptor,
                TempFolderPath = ps.tempFolderPath,
                OriginalFxController = LoadAssetByPath<RuntimeAnimatorController>(ps.originalFxControllerPath),
                OriginalMenu = LoadAssetByPath<VRCExpressionsMenu>(ps.originalMenuPath),
                OriginalParameters = LoadAssetByPath<VRCExpressionParameters>(ps.originalParametersPath),
                OriginalCustomizeAnimationLayers = ps.originalCustomizeAnimationLayers,
                OriginalFxLayerExisted = ps.originalFxLayerExisted,
                OriginalFxLayerWasInBase = ps.originalFxLayerWasInBase,
                OriginalFxLayerIndex = ps.originalFxLayerIndex,
                OriginalFxLayerIsDefault = ps.originalFxLayerIsDefault
            };

            if (ps.dressers != null)
            {
                foreach (var pb in ps.dressers)
                {
                    var dresser = LoadSceneObject<DiNeMultiDresser>(pb.dresserId);
                    if (dresser == null)
                        continue;

                    session.Dressers.Add(new DresserBinding
                    {
                        Dresser = dresser,
                        OriginalAnimatorController = LoadAssetByPath<AnimatorController>(pb.originalAnimatorControllerPath),
                        OriginalExpressionsMenu = LoadAssetByPath<VRCExpressionsMenu>(pb.originalExpressionsMenuPath)
                    });
                }
            }

            ActiveSessions[descriptor.GetInstanceID()] = session;
        }
    }

    private static TemporarySession CreateTemporarySession(VRCAvatarDescriptor descriptor, List<DiNeMultiDresser> dressers)
    {
        EnsureFolderExists(TempRootFolder);

        string sessionFolder = AssetDatabase.GenerateUniqueAssetPath(
            $"{TempRootFolder}/{GetSafeName(descriptor.gameObject.name)}_{Guid.NewGuid():N}");
        EnsureFolderExists(sessionFolder);

        var session = new TemporarySession
        {
            Descriptor = descriptor,
            TempFolderPath = sessionFolder,
            OriginalFxController = GetDescriptorFxController(descriptor),
            OriginalMenu = descriptor.expressionsMenu,
            OriginalParameters = descriptor.expressionParameters
        };
        CaptureDescriptorFxState(descriptor, session);

        // 캡처한 '원본'이 임시 폴더의 에셋이면 이전 빌드의 복원이 실패해 남은 잔여물이다.
        // 이대로 복원하면 삭제될 임시 에셋을 가리키게 되므로 명확히 경고한다.
        if (IsTempAsset(session.OriginalFxController) || IsTempAsset(session.OriginalMenu) || IsTempAsset(session.OriginalParameters))
        {
            Debug.LogWarning(
                $"[DiNe] Multi Dresser: '{descriptor.name}'의 현재 FX/메뉴/파라미터가 임시 빌드 에셋을 가리키고 있습니다. " +
                "이전 빌드의 복원이 실패한 상태일 수 있습니다. 멀티 드레서 인스펙터의 새로고침(↺) 버튼으로 원본을 다시 지정한 뒤 업로드하세요.");
        }

        var sourceFxController = session.OriginalFxController as AnimatorController;
        var sourceMenu = descriptor.expressionsMenu != null
            ? descriptor.expressionsMenu
            : dressers.Select(d => d.expressionsMenu).FirstOrDefault(menu => menu != null);

        session.TempAnimatorController = CloneOrCreateAnimatorController(sourceFxController, sessionFolder);
        session.TempExpressionsMenu = CloneOrCreateExpressionsMenu(sourceMenu, sessionFolder);
        session.TempExpressionParameters = CloneOrCreateExpressionParameters(descriptor.expressionParameters, sessionFolder);
        SanitizeAnimatorController(session.TempAnimatorController);

        SetDescriptorFxController(descriptor, session.TempAnimatorController, true);
        descriptor.expressionsMenu = session.TempExpressionsMenu;
        descriptor.expressionParameters = session.TempExpressionParameters;
        EditorUtility.SetDirty(descriptor);

        foreach (var dresser in dressers)
        {
            var binding = new DresserBinding
            {
                Dresser = dresser,
                OriginalAnimatorController = dresser.animatorController,
                OriginalExpressionsMenu = dresser.expressionsMenu
            };

            dresser.animatorController = session.TempAnimatorController;
            dresser.expressionsMenu = session.TempExpressionsMenu;
            EditorUtility.SetDirty(dresser);

            session.Dressers.Add(binding);
        }

        WriteSessionManifest(session);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return session;
    }

    private static void RestoreAllSessions(string reason)
    {
        var persisted = LoadPersistedState();
        bool hadInMemory = false;
        bool anyFailed = false;

        foreach (var session in ActiveSessions.Values.ToList())
        {
            hadInMemory = true;
            try
            {
                if (!RestoreSession(session))
                    anyFailed = true;
            }
            catch (Exception e)
            {
                anyFailed = true;
                Debug.LogError($"[DiNe] Multi Dresser 복원(in-memory) 중 예외: {e.Message}\n{e.StackTrace}");
            }
        }

        ActiveSessions.Clear();

        // 라이팅 디자이너가 빌드 클론용으로 구운 머티리얼/텍스처도 같이 정리한다.
        // 세션과 무관한 고정 폴더라서 복원 성공 여부와 상관없이 지워도 안전하다.
        try
        {
            DiNeLightingBaker.CleanupTempAssets();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DiNe] 라이팅 디자이너 임시 에셋 정리 실패: {e.Message}");
        }

        // 플레이 모드에서 도메인 리로드가 비활성화돼 있으면 ActiveSessions에는 파괴된
        // 플레이 클론이 남을 수 있다. 그 항목이 있었다는 이유로 영속 세션 복원을 건너뛰면,
        // 에디트 모드 씬은 임시 FX를 가리킨 채 남는다. 따라서 영속 세션도 항상 확인한다.
        if (persisted.sessions.Count > 0)
        {
            foreach (var session in persisted.sessions)
            {
                try
                {
                    if (!RestorePersistedSession(session))
                        anyFailed = true;
                }
                catch (Exception e)
                {
                    anyFailed = true;
                    Debug.LogError($"[DiNe] Multi Dresser 복원(persisted) 중 예외: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        // 복원이 전부 성공했을 때만 영속 원본 정보를 비운다. 실패가 하나라도 있었는데 지우면
        // 다음 기회(씬 재로드/다음 게임모드 종료)에 원본을 복구할 수 없어 영구 missing이 된다.
        if (!anyFailed)
        {
            SessionState.EraseString(SessionStateKey);
        }
        else
        {
            Debug.LogWarning("[DiNe] Multi Dresser: 일부 복원에 실패해 원본 정보를 보존합니다. 임시(_DiNe) 폴더도 남겨둡니다. " +
                "인스펙터의 새로고침(↺) 버튼으로 원본을 다시 지정하면 다음 복원에서 정리됩니다.");
        }

        bool swept = SweepOrphanTempFolders();

        if (hadInMemory || persisted.sessions.Count > 0 || swept)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DiNe] Multi Dresser 복원 완료 ({reason}). 실패 있음={anyFailed}");
        }
    }

    // __Temp 아래에 남았지만 어떤 세션도 책임지지 않고(메모리/영속 모두), 로드된 씬 오브젝트가
    // 하나도 가리키지 않는 폴더는 이전 빌드가 남긴 쓰레기다. 버려진 빌드 클론의 폴더가 대표적.
    // 반환값: 하나라도 지웠으면 true.
    private static bool SweepOrphanTempFolders()
    {
        if (!AssetDatabase.IsValidFolder(TempRootFolder))
            return false;

        bool deletedAny = false;
        foreach (var folder in AssetDatabase.GetSubFolders(TempRootFolder))
        {
            string normalized = folder.Replace('\\', '/');
            if (!IsOrphanTempFolder(normalized))
                continue;
            if (AnyLoadedObjectReferencesFolder(normalized))
                continue;

            try
            {
                DeleteTemporaryFolder(normalized);
                deletedAny = true;
                Debug.Log($"[DiNe] Multi Dresser: 남겨진 임시 폴더를 정리했습니다: {normalized}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DiNe] Multi Dresser: 임시 폴더 정리 실패({normalized}): {e.Message}");
            }
        }

        if (deletedAny &&
            AssetDatabase.GetSubFolders(TempRootFolder).Length == 0 &&
            AssetDatabase.FindAssets(string.Empty, new[] { TempRootFolder }).Length == 0)
        {
            DeleteTemporaryFolder(TempRootFolder);
        }

        return deletedAny;
    }

    // 반환값: 완전히 복원되어 임시 폴더를 정리해도 되면 true.
    private static bool RestoreSession(TemporarySession session)
    {
        if (session == null)
            return true;

        // 플레이 종료 시 이 참조는 파괴된 플레이 클론일 수 있다. 같은 임시 폴더를 가리키는
        // 에디트 모드 원본은 영속 세션으로 이어서 복원해야 하므로 여기서 폴더를 지우지 않는다.
        if (session.Descriptor == null)
            return true;

        // 원본이 임시(_DiNe) 에셋을 가리키면(이전 빌드 잔여물 위에 세션이 만들어진 경우) 그 값으로
        // 되돌리면 안 된다. 더미 체인을 따라 진짜 원본을 찾아 그것으로 되돌린다.
        // 빈 슬롯(null)은 원래 비어있던 정상 상태로 간주한다.
        bool fxOk = TryResolveOriginal(session.OriginalFxController, null, OriginalKind.Fx, out RuntimeAnimatorController resolvedFx);
        bool menuOk = TryResolveOriginal(session.OriginalMenu, null, OriginalKind.Menu, out VRCExpressionsMenu resolvedMenu);
        bool paramsOk = TryResolveOriginal(session.OriginalParameters, null, OriginalKind.Parameters, out VRCExpressionParameters resolvedParams);
        if (fxOk) session.OriginalFxController = resolvedFx;
        if (menuOk) session.OriginalMenu = resolvedMenu;
        if (paramsOk) session.OriginalParameters = resolvedParams;

        if (fxOk)
        {
            if (session.DescriptorStateVersion >= 1)
            {
                RestoreDescriptorFxState(
                    session.Descriptor,
                    session.OriginalFxController,
                    session.OriginalCustomizeAnimationLayers,
                    session.OriginalFxLayerExisted,
                    session.OriginalFxLayerWasInBase,
                    session.OriginalFxLayerIndex,
                    session.OriginalFxLayerIsDefault);
            }
            else
            {
                SetDescriptorFxController(session.Descriptor, session.OriginalFxController);
            }
        }
        if (menuOk) session.Descriptor.expressionsMenu = session.OriginalMenu;
        if (paramsOk) session.Descriptor.expressionParameters = session.OriginalParameters;
        EditorUtility.SetDirty(session.Descriptor);
        MarkSceneDirty(session.Descriptor);

        foreach (var binding in session.Dressers)
        {
            RestoreDresserBinding(binding);
        }

        bool allOk = fxOk && menuOk && paramsOk;
        if (!allOk)
        {
            Debug.LogError($"[DiNe] Multi Dresser: '{session.Descriptor.name}' 원본이 임시 에셋을 가리켜 일부 복원을 건너뜁니다 " +
                $"(fx={fxOk}, menu={menuOk}, params={paramsOk}). 임시 폴더를 보존합니다.");
            return false;
        }

        // 임시 폴더는 이를 참조하는 로드된 오브젝트가 하나도 없을 때만 삭제한다.
        if (AnyLoadedObjectReferencesFolder(session.TempFolderPath))
        {
            Debug.LogWarning($"[DiNe] Multi Dresser: 아직 임시 폴더를 참조하는 오브젝트가 있어 폴더를 보존합니다: {session.TempFolderPath}");
            return false;
        }

        DeleteTemporaryFolder(session.TempFolderPath);
        // AssetDatabase.Refresh가 삭제된 컨트롤러를 Unity의 fake-null 참조로 다시 남기는
        // 경우가 있어, 삭제 완료 뒤 직렬화 필드까지 한 번 더 원본 상태로 확정한다.
        if (fxOk && session.DescriptorStateVersion >= 1 && session.Descriptor != null)
        {
            RestoreDescriptorFxState(
                session.Descriptor,
                session.OriginalFxController,
                session.OriginalCustomizeAnimationLayers,
                session.OriginalFxLayerExisted,
                session.OriginalFxLayerWasInBase,
                session.OriginalFxLayerIndex,
                session.OriginalFxLayerIsDefault);
        }
        return true;
    }

    // 반환값: 완전히 복원되어 임시 폴더를 정리해도 되면 true.
    private static bool RestorePersistedSession(PersistedSession session)
    {
        if (session == null)
            return true;

        // 원본이 비었거나(경로 stale/삭제됨) 그 값으로 덮어쓰면 오히려 missing이 되므로 실패로 처리한다.
        // 원본이 임시(_DiNe) 에셋이면 더미 체인을 따라 진짜 원본을 찾는다.
        bool fxOk = TryResolveOriginal(null, session.originalFxControllerPath, OriginalKind.Fx, out RuntimeAnimatorController originalFx);
        bool menuOk = TryResolveOriginal(null, session.originalMenuPath, OriginalKind.Menu, out VRCExpressionsMenu originalMenu);
        bool paramsOk = TryResolveOriginal(null, session.originalParametersPath, OriginalKind.Parameters, out VRCExpressionParameters originalParams);

        // 복원 대상은 '이 세션의 임시 폴더를 실제로 가리키는' 디스크립터다. GlobalObjectId는
        // 게임모드/복제 후 빗나갈 수 있으므로(엉뚱한 인스턴스 복원 → 진짜 아바타는 더미 채로
        // 방치되어 missing), 씬을 직접 스캔해 더미를 들고 있는 진짜 오브젝트를 찾는다.
        var targetDescriptors = GetLoadedSceneDescriptors()
            .Where(d => DescriptorReferencesFolder(d, session.tempFolderPath))
            .ToList();

        // 보조: GlobalObjectId로 찾은 것이 폴더를 가리키면 함께 포함.
        var byId = LoadSceneObject<VRCAvatarDescriptor>(session.descriptorId);
        if (byId != null && DescriptorReferencesFolder(byId, session.tempFolderPath) && !targetDescriptors.Contains(byId))
            targetDescriptors.Add(byId);

        var originalFxAc = originalFx as AnimatorController;
        var targetDressers = GetLoadedSceneDressers()
            .Where(dr => DresserReferencesFolder(dr, session.tempFolderPath))
            .ToList();

        Debug.Log($"[DiNe][restore] tempFolder='{session.tempFolderPath}' 대상디스크립터={targetDescriptors.Count} 대상드레서={targetDressers.Count} " +
                  $"fxOk={fxOk}('{session.originalFxControllerPath}') menuOk={menuOk}('{session.originalMenuPath}') paramsOk={paramsOk}('{session.originalParametersPath}')");

        foreach (var d in targetDescriptors)
        {
            if (fxOk)
            {
                if (session.descriptorStateVersion >= 1)
                {
                    RestoreDescriptorFxState(
                        d,
                        originalFx,
                        session.originalCustomizeAnimationLayers,
                        session.originalFxLayerExisted,
                        session.originalFxLayerWasInBase,
                        session.originalFxLayerIndex,
                        session.originalFxLayerIsDefault);
                }
                else
                {
                    SetDescriptorFxController(d, originalFx);
                }
            }
            if (menuOk) d.expressionsMenu = originalMenu;
            if (paramsOk) d.expressionParameters = originalParams;
            EditorUtility.SetDirty(d);
            MarkSceneDirty(d);
        }

        foreach (var dr in targetDressers)
        {
            if (fxOk && originalFxAc != null) dr.animatorController = originalFxAc;
            if (menuOk) dr.expressionsMenu = originalMenu;
            EditorUtility.SetDirty(dr);
        }

        // 세션 바인딩에 더 정확한 드레서 원본이 있으면 덮어쓴다.
        if (session.dressers != null)
        {
            foreach (var binding in session.dressers)
            {
                var dresser = LoadSceneObject<DiNeMultiDresser>(binding.dresserId);
                if (dresser == null)
                    continue;

                if (TryResolveOriginal(null, binding.originalAnimatorControllerPath, OriginalKind.Fx, out AnimatorController dac))
                    dresser.animatorController = dac;
                if (TryResolveOriginal(null, binding.originalExpressionsMenuPath, OriginalKind.Menu, out VRCExpressionsMenu dmenu))
                    dresser.expressionsMenu = dmenu;
                EditorUtility.SetDirty(dresser);
            }
        }

        // NDMF Test Build는 업로드용 클론과 __Generated 에셋을 빌드 직후 파기한다.
        // 이 클론의 세션이 도메인 리로드를 넘어 남으면 원본 메뉴/파라미터 경로가 이미 사라져
        // 일반 플레이 모드 종료 때 복원 실패로 오인된다. 현재 로드된 씬에서 이 임시 폴더를
        // 참조하는 대상이 전혀 없다면 파괴된 클론의 잔여 세션이므로 안전하게 정리한다.
        if (targetDescriptors.Count == 0 &&
            targetDressers.Count == 0 &&
            !AnyLoadedObjectReferencesFolder(session.tempFolderPath))
        {
            DeleteTemporaryFolder(session.tempFolderPath);
            Debug.Log($"[DiNe] Multi Dresser: 파기된 빌드 클론의 잔여 세션을 정리했습니다: {session.tempFolderPath}");
            return true;
        }

        bool allOk = fxOk && menuOk && paramsOk;
        if (!allOk)
        {
            Debug.LogError($"[DiNe] Multi Dresser: 복원 시 원본 일부가 유효하지 않습니다 " +
                $"(fx={fxOk}:'{session.originalFxControllerPath}', menu={menuOk}:'{session.originalMenuPath}', params={paramsOk}:'{session.originalParametersPath}'). " +
                "임시 폴더를 보존하니, 멀티 드레서 인스펙터의 새로고침(↺)으로 원본을 다시 지정하세요.");
            return false;
        }

        // 임시 폴더는 '이 폴더를 가리키는 로드된 오브젝트가 하나도 없을 때'만 삭제한다.
        // 하나라도 남아있는데 지우면 그 참조가 missing이 된다.
        if (AnyLoadedObjectReferencesFolder(session.tempFolderPath))
        {
            Debug.LogWarning($"[DiNe] Multi Dresser: 아직 임시 폴더를 참조하는 오브젝트가 있어 폴더를 보존합니다: {session.tempFolderPath}");
            return false;
        }

        DeleteTemporaryFolder(session.tempFolderPath);
        if (fxOk && session.descriptorStateVersion >= 1)
        {
            foreach (var descriptor in targetDescriptors)
            {
                RestoreDescriptorFxState(
                    descriptor,
                    originalFx,
                    session.originalCustomizeAnimationLayers,
                    session.originalFxLayerExisted,
                    session.originalFxLayerWasInBase,
                    session.originalFxLayerIndex,
                    session.originalFxLayerIsDefault);
            }
        }
        return true;
    }

    // ── 씬 스캔 헬퍼: GlobalObjectId에 의존하지 않고 실제 오브젝트를 찾는다 ──

    private static IEnumerable<VRCAvatarDescriptor> GetLoadedSceneDescriptors()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
                foreach (var d in root.GetComponentsInChildren<VRCAvatarDescriptor>(true))
                    yield return d;
        }
    }

    private static IEnumerable<DiNeMultiDresser> GetLoadedSceneDressers()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
                foreach (var d in root.GetComponentsInChildren<DiNeMultiDresser>(true))
                    yield return d;
        }
    }

    private static bool PathIsUnderFolder(string assetPath, string folder)
    {
        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(folder))
            return false;

        string p = assetPath.Replace('\\', '/');
        string f = folder.Replace('\\', '/');
        return p == f || p.StartsWith(f + "/");
    }

    private static bool DescriptorReferencesFolder(VRCAvatarDescriptor descriptor, string folder)
    {
        if (descriptor == null)
            return false;

        return PathIsUnderFolder(AssetDatabase.GetAssetPath(GetDescriptorFxController(descriptor)), folder)
            || PathIsUnderFolder(AssetDatabase.GetAssetPath(descriptor.expressionsMenu), folder)
            || PathIsUnderFolder(AssetDatabase.GetAssetPath(descriptor.expressionParameters), folder);
    }

    private static bool DresserReferencesFolder(DiNeMultiDresser dresser, string folder)
    {
        if (dresser == null)
            return false;

        return PathIsUnderFolder(AssetDatabase.GetAssetPath(dresser.animatorController), folder)
            || PathIsUnderFolder(AssetDatabase.GetAssetPath(dresser.expressionsMenu), folder);
    }

    private static bool AnyLoadedObjectReferencesFolder(string folder)
    {
        foreach (var d in GetLoadedSceneDescriptors())
            if (DescriptorReferencesFolder(d, folder))
                return true;

        foreach (var dr in GetLoadedSceneDressers())
            if (DresserReferencesFolder(dr, folder))
                return true;

        return false;
    }

    // 복원 대상 원본이 실제로 쓸 수 있는지 검사한다.
    // - 경로가 비어있고 에셋도 null이면, 원래부터 비어있던 슬롯이므로 정상(true).
    // - 경로는 있는데 에셋이 null이면 stale/삭제됨 → 복원 불가(false).
    // - 임시(__Temp) 에셋이면 복원하면 안 됨(false).

    private static void RestoreDresserBinding(DresserBinding binding)
    {
        if (binding == null || binding.Dresser == null)
            return;

        binding.Dresser.animatorController = binding.OriginalAnimatorController;
        binding.Dresser.expressionsMenu = binding.OriginalExpressionsMenu;
        EditorUtility.SetDirty(binding.Dresser);
    }

    private static void PersistSessions()
    {
        var state = new PersistedState();

        foreach (var session in ActiveSessions.Values)
        {
            if (session == null || session.Descriptor == null)
                continue;

            state.sessions.Add(ToPersistedSession(session));
        }

        if (state.sessions.Count == 0)
        {
            SessionState.EraseString(SessionStateKey);
            return;
        }

        SessionState.SetString(SessionStateKey, JsonUtility.ToJson(state));
    }

    private static PersistedSession ToPersistedSession(TemporarySession session)
    {
        var persistedSession = new PersistedSession
        {
            descriptorStateVersion = session.DescriptorStateVersion,
            descriptorId = ToGlobalObjectId(session.Descriptor),
            tempFolderPath = session.TempFolderPath,
            originalFxControllerPath = AssetDatabase.GetAssetPath(session.OriginalFxController),
            originalMenuPath = AssetDatabase.GetAssetPath(session.OriginalMenu),
            originalParametersPath = AssetDatabase.GetAssetPath(session.OriginalParameters),
            originalCustomizeAnimationLayers = session.OriginalCustomizeAnimationLayers,
            originalFxLayerExisted = session.OriginalFxLayerExisted,
            originalFxLayerWasInBase = session.OriginalFxLayerWasInBase,
            originalFxLayerIndex = session.OriginalFxLayerIndex,
            originalFxLayerIsDefault = session.OriginalFxLayerIsDefault
        };

        foreach (var binding in session.Dressers)
        {
            if (binding?.Dresser == null)
                continue;

            persistedSession.dressers.Add(new PersistedDresserBinding
            {
                dresserId = ToGlobalObjectId(binding.Dresser),
                originalAnimatorControllerPath = AssetDatabase.GetAssetPath(binding.OriginalAnimatorController),
                originalExpressionsMenuPath = AssetDatabase.GetAssetPath(binding.OriginalExpressionsMenu)
            });
        }

        return persistedSession;
    }

    private static PersistedState LoadPersistedState()
    {
        string json = SessionState.GetString(SessionStateKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new PersistedState();

        return JsonUtility.FromJson<PersistedState>(json) ?? new PersistedState();
    }

    private static VRCAvatarDescriptor FindDescriptor(DiNeMultiDresser dresser)
    {
        if (dresser == null)
            return null;

        return dresser.GetComponentInParent<VRCAvatarDescriptor>(true)
            ?? dresser.GetComponentInChildren<VRCAvatarDescriptor>(true);
    }

    private static RuntimeAnimatorController GetDescriptorFxController(VRCAvatarDescriptor descriptor)
    {
        if (descriptor == null)
            return null;

        if (descriptor.baseAnimationLayers != null)
        {
            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    return layer.animatorController;
            }
        }

        if (descriptor.specialAnimationLayers != null)
        {
            foreach (var layer in descriptor.specialAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    return layer.animatorController;
            }
        }

        return null;
    }

    private static void CaptureDescriptorFxState(VRCAvatarDescriptor descriptor, TemporarySession session)
    {
        if (descriptor == null || session == null)
            return;

        session.OriginalCustomizeAnimationLayers = descriptor.customizeAnimationLayers;
        session.DescriptorStateVersion = 1;
        session.OriginalFxLayerExisted = TryFindDescriptorFxLayer(
            descriptor,
            out session.OriginalFxLayerWasInBase,
            out session.OriginalFxLayerIndex,
            out var layer);
        session.OriginalFxLayerIsDefault = session.OriginalFxLayerExisted && layer.isDefault;
    }

    private static bool TryFindDescriptorFxLayer(
        VRCAvatarDescriptor descriptor,
        out bool inBaseLayers,
        out int index,
        out VRCAvatarDescriptor.CustomAnimLayer result)
    {
        inBaseLayers = true;
        index = -1;
        result = default;
        if (descriptor == null)
            return false;

        if (descriptor.baseAnimationLayers != null)
        {
            for (int i = 0; i < descriptor.baseAnimationLayers.Length; i++)
            {
                if (descriptor.baseAnimationLayers[i].type != VRCAvatarDescriptor.AnimLayerType.FX)
                    continue;

                index = i;
                result = descriptor.baseAnimationLayers[i];
                return true;
            }
        }

        if (descriptor.specialAnimationLayers != null)
        {
            for (int i = 0; i < descriptor.specialAnimationLayers.Length; i++)
            {
                if (descriptor.specialAnimationLayers[i].type != VRCAvatarDescriptor.AnimLayerType.FX)
                    continue;

                inBaseLayers = false;
                index = i;
                result = descriptor.specialAnimationLayers[i];
                return true;
            }
        }

        return false;
    }

    private static void SetDescriptorFxController(
        VRCAvatarDescriptor descriptor,
        RuntimeAnimatorController controller,
        bool makeCustom = false)
    {
        if (descriptor == null)
            return;

        if (TryFindDescriptorFxLayer(descriptor, out bool inBaseLayers, out int index, out var layer))
        {
            layer.animatorController = controller;
            if (makeCustom)
                layer.isDefault = false;

            if (inBaseLayers)
            {
                descriptor.baseAnimationLayers[index] = layer;
            }
            else
            {
                descriptor.specialAnimationLayers[index] = layer;
            }
        }
        else if (makeCustom)
        {
            // VRCSDK는 Descriptor 인스펙터의 Playable Layers가 한 번도 펼쳐지지 않은
            // 아바타에서 배열을 아직 만들지 않을 수 있다. 이 경우 임시 FX 슬롯을 추가한다.
            var layers = descriptor.baseAnimationLayers != null
                ? descriptor.baseAnimationLayers.ToList()
                : new List<VRCAvatarDescriptor.CustomAnimLayer>();
            layers.Add(new VRCAvatarDescriptor.CustomAnimLayer
            {
                type = VRCAvatarDescriptor.AnimLayerType.FX,
                isDefault = false,
                animatorController = controller
            });
            descriptor.baseAnimationLayers = layers.ToArray();
        }

        if (makeCustom)
            descriptor.customizeAnimationLayers = true;

        EditorUtility.SetDirty(descriptor);
    }

    private static void RestoreDescriptorFxState(
        VRCAvatarDescriptor descriptor,
        RuntimeAnimatorController originalController,
        bool originalCustomizeAnimationLayers,
        bool originalLayerExisted,
        bool originalLayerWasInBase,
        int originalLayerIndex,
        bool originalLayerIsDefault)
    {
        if (descriptor == null)
            return;

        if (!originalLayerExisted)
        {
            // 이 세션에서 추가한 FX 슬롯만 제거한다. 원래 FX 슬롯이 없었으므로
            // 임시 컨트롤러를 가리키는 FX 항목을 정리하면 원본 배열 상태가 복원된다.
            if (descriptor.baseAnimationLayers != null)
            {
                descriptor.baseAnimationLayers = descriptor.baseAnimationLayers
                    .Where(layer => layer.type != VRCAvatarDescriptor.AnimLayerType.FX ||
                        layer.animatorController != originalController)
                    .ToArray();

                // originalController가 null인 경우 위 조건으로는 임시 FX가 남으므로,
                // 원래 FX가 전혀 없었다는 캡처 정보를 기준으로 남은 FX도 제거한다.
                descriptor.baseAnimationLayers = descriptor.baseAnimationLayers
                    .Where(layer => layer.type != VRCAvatarDescriptor.AnimLayerType.FX)
                    .ToArray();
            }

            if (descriptor.specialAnimationLayers != null)
            {
                descriptor.specialAnimationLayers = descriptor.specialAnimationLayers
                    .Where(layer => layer.type != VRCAvatarDescriptor.AnimLayerType.FX)
                    .ToArray();
            }
        }
        else
        {
            var layers = originalLayerWasInBase
                ? descriptor.baseAnimationLayers
                : descriptor.specialAnimationLayers;
            var list = layers != null
                ? layers.ToList()
                : new List<VRCAvatarDescriptor.CustomAnimLayer>();

            int targetIndex = originalLayerIndex >= 0 && originalLayerIndex < list.Count &&
                list[originalLayerIndex].type == VRCAvatarDescriptor.AnimLayerType.FX
                ? originalLayerIndex
                : list.FindIndex(layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX);

            var restoredLayer = new VRCAvatarDescriptor.CustomAnimLayer
            {
                type = VRCAvatarDescriptor.AnimLayerType.FX,
                isDefault = originalLayerIsDefault,
                animatorController = originalController
            };

            if (targetIndex >= 0)
            {
                // 기존 임시 레이어 인스턴스를 재사용하면 에셋 삭제 뒤 컨트롤러가 Unity의
                // fake-null(Missing) 참조로 남을 수 있다. 새 값 객체로 교체하되 마스크는 보존한다.
                restoredLayer.mask = list[targetIndex].mask;
                list[targetIndex] = restoredLayer;
            }
            else
            {
                int insertIndex = Mathf.Clamp(originalLayerIndex, 0, list.Count);
                list.Insert(insertIndex, restoredLayer);
            }

            if (originalLayerWasInBase)
                descriptor.baseAnimationLayers = list.ToArray();
            else
                descriptor.specialAnimationLayers = list.ToArray();
        }

        descriptor.customizeAnimationLayers = originalCustomizeAnimationLayers;
        ApplySerializedDescriptorFxState(
            descriptor,
            originalController,
            originalCustomizeAnimationLayers,
            originalLayerExisted,
            originalLayerWasInBase,
            originalLayerIndex,
            originalLayerIsDefault);
        EditorUtility.SetDirty(descriptor);
    }

    private static void ApplySerializedDescriptorFxState(
        VRCAvatarDescriptor descriptor,
        RuntimeAnimatorController originalController,
        bool originalCustomizeAnimationLayers,
        bool originalLayerExisted,
        bool originalLayerWasInBase,
        int originalLayerIndex,
        bool originalLayerIsDefault)
    {
        var serialized = new SerializedObject(descriptor);
        serialized.Update();

        var customize = serialized.FindProperty("customizeAnimationLayers");
        if (customize != null)
            customize.boolValue = originalCustomizeAnimationLayers;

        if (originalLayerExisted)
        {
            var layers = serialized.FindProperty(
                originalLayerWasInBase ? "baseAnimationLayers" : "specialAnimationLayers");
            if (layers != null && layers.isArray)
            {
                int targetIndex = -1;
                if (originalLayerIndex >= 0 && originalLayerIndex < layers.arraySize)
                {
                    var candidateType = layers.GetArrayElementAtIndex(originalLayerIndex)
                        .FindPropertyRelative("type");
                    if (candidateType != null &&
                        candidateType.enumValueIndex == (int)VRCAvatarDescriptor.AnimLayerType.FX)
                        targetIndex = originalLayerIndex;
                }

                if (targetIndex < 0)
                {
                    for (int i = 0; i < layers.arraySize; i++)
                    {
                        var type = layers.GetArrayElementAtIndex(i).FindPropertyRelative("type");
                        if (type == null || type.enumValueIndex != (int)VRCAvatarDescriptor.AnimLayerType.FX)
                            continue;

                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex >= 0)
                {
                    var element = layers.GetArrayElementAtIndex(targetIndex);
                    var isDefault = element.FindPropertyRelative("isDefault");
                    var controller = element.FindPropertyRelative("animatorController");
                    if (isDefault != null) isDefault.boolValue = originalLayerIsDefault;
                    if (controller != null) controller.objectReferenceValue = originalController;
                }
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AnimatorController CloneOrCreateAnimatorController(AnimatorController source, string folder)
    {
        string tempName = GetTempAssetName(source, "FX");
        if (source != null)
        {
            var clone = CloneAsset<AnimatorController>(source, folder, tempName);
            if (clone != null)
                return clone;
        }

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{tempName}.controller");
        return AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    // 더미(임시) 에셋의 이름을 '원본 파일명_DiNe' 형태로 만든다.
    // 원본이 없으면 fallback(FX/ExpressionsMenu/ExpressionParameters)_DiNe 을 쓴다.
    private static string GetTempAssetName(UnityEngine.Object source, string fallback)
    {
        string baseName = fallback;
        if (source != null)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(sourcePath))
                baseName = Path.GetFileNameWithoutExtension(sourcePath);
        }

        return $"{baseName}_DiNe";
    }

    private static void SanitizeAnimatorController(AnimatorController controller)
    {
        if (controller == null)
            return;

        var parameterMap = new Dictionary<string, AnimatorControllerParameterType>();
        foreach (var parameter in controller.parameters)
        {
            if (!string.IsNullOrEmpty(parameter.name))
                parameterMap[parameter.name] = parameter.type;
        }

        int removedTransitions = 0;
        int addedParameters = 0;

        foreach (var layer in controller.layers)
        {
            if (layer.stateMachine == null)
                continue;

            removedTransitions += SanitizeStateMachine(layer.stateMachine, controller, parameterMap, ref addedParameters);
        }

        if (removedTransitions > 0 || addedParameters > 0)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DiNe] Sanitized temporary FX controller '{controller.name}': removed {removedTransitions} invalid transition(s), added {addedParameters} missing parameter(s).");
        }
    }

    private static int SanitizeStateMachine(
        AnimatorStateMachine stateMachine,
        AnimatorController controller,
        Dictionary<string, AnimatorControllerParameterType> parameterMap,
        ref int addedParameters)
    {
        if (stateMachine == null)
            return 0;

        int removedTransitions = 0;

        foreach (var transition in stateMachine.entryTransitions.ToArray())
        {
            if (IsInvalidTransition(transition))
            {
                stateMachine.RemoveEntryTransition(transition);
                removedTransitions++;
                continue;
            }

            EnsureTransitionParameters(transition, controller, parameterMap, ref addedParameters);
        }

        foreach (var transition in stateMachine.anyStateTransitions.ToArray())
        {
            if (IsInvalidTransition(transition))
            {
                stateMachine.RemoveAnyStateTransition(transition);
                removedTransitions++;
                continue;
            }

            EnsureTransitionParameters(transition, controller, parameterMap, ref addedParameters);
        }

        foreach (var childState in stateMachine.states)
        {
            var state = childState.state;
            if (state == null)
                continue;

            foreach (var transition in state.transitions.ToArray())
            {
                if (IsInvalidTransition(transition))
                {
                    state.RemoveTransition(transition);
                    removedTransitions++;
                    continue;
                }

                EnsureTransitionParameters(transition, controller, parameterMap, ref addedParameters);
            }
        }

        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            if (childStateMachine.stateMachine == null)
                continue;

            removedTransitions += SanitizeStateMachine(childStateMachine.stateMachine, controller, parameterMap, ref addedParameters);
        }

        return removedTransitions;
    }

    private static bool IsInvalidTransition(AnimatorTransitionBase transition)
    {
        if (transition == null)
            return true;

        if (transition is AnimatorStateTransition stateTransition)
            return !stateTransition.isExit
                && stateTransition.destinationState == null
                && stateTransition.destinationStateMachine == null;

        if (transition is AnimatorTransition entryTransition)
            return entryTransition.destinationState == null
                && entryTransition.destinationStateMachine == null;

        return false;
    }

    private static void EnsureTransitionParameters(
        AnimatorTransitionBase transition,
        AnimatorController controller,
        Dictionary<string, AnimatorControllerParameterType> parameterMap,
        ref int addedParameters)
    {
        if (transition == null)
            return;

        foreach (var condition in transition.conditions)
        {
            if (string.IsNullOrEmpty(condition.parameter) || parameterMap.ContainsKey(condition.parameter))
                continue;

            var parameterType = InferParameterType(condition.parameter, condition.mode);
            controller.AddParameter(condition.parameter, parameterType);
            parameterMap[condition.parameter] = parameterType;
            addedParameters++;
        }
    }

    private static AnimatorControllerParameterType InferParameterType(string parameterName, AnimatorConditionMode mode)
    {
        if (KnownAnimatorParameters.TryGetValue(parameterName, out var knownType))
            return knownType;

        switch (mode)
        {
            case AnimatorConditionMode.If:
            case AnimatorConditionMode.IfNot:
                return AnimatorControllerParameterType.Bool;
            case AnimatorConditionMode.Equals:
            case AnimatorConditionMode.NotEqual:
                return AnimatorControllerParameterType.Int;
            default:
                return AnimatorControllerParameterType.Float;
        }
    }

    private static VRCExpressionsMenu CloneOrCreateExpressionsMenu(VRCExpressionsMenu source, string folder)
    {
        string tempName = GetTempAssetName(source, "ExpressionsMenu");
        if (source != null)
        {
            var clone = CloneAsset<VRCExpressionsMenu>(source, folder, tempName);
            if (clone != null)
                return clone;
        }

        var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{tempName}.asset");
        AssetDatabase.CreateAsset(menu, path);
        return menu;
    }

    private static VRCExpressionParameters CloneOrCreateExpressionParameters(VRCExpressionParameters source, string folder)
    {
        string tempName = GetTempAssetName(source, "ExpressionParameters");
        if (source != null)
        {
            var clone = CloneAsset<VRCExpressionParameters>(source, folder, tempName);
            if (clone != null)
                return clone;
        }

        var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
        parameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{tempName}.asset");
        AssetDatabase.CreateAsset(parameters, path);
        return parameters;
    }

    private static T CloneAsset<T>(T source, string folder, string fileNameWithoutExtension) where T : UnityEngine.Object
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath))
            return null;

        string extension = Path.GetExtension(sourcePath);
        string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileNameWithoutExtension}{extension}");

        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<T>(targetPath);
    }

    private static T LoadAssetByPath<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
            return null;

        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static bool IsTempAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            return false;

        return path.Replace('\\', '/').StartsWith(TempRootFolder);
    }

    private static T LoadSceneObject<T>(string globalObjectId) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(globalObjectId))
            return null;

        if (!GlobalObjectId.TryParse(globalObjectId, out var parsedId))
            return null;

        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsedId) as T;
    }

    private static string ToGlobalObjectId(UnityEngine.Object target)
    {
        if (target == null)
            return string.Empty;

        return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
    }

    // 복원으로 바뀐 씬 오브젝트의 참조가 디스크에 남도록 해당 씬을 dirty 표시한다.
    // 이렇게 해야 도메인 리로드/씬 재로드 후에도 원본 FX/메뉴/파라미터 할당이 유지된다.
    private static void MarkSceneDirty(UnityEngine.Object sceneObject)
    {
        if (sceneObject is Component component && component != null)
        {
            var scene = component.gameObject.scene;
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static void DeleteTemporaryFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.DeleteAsset(folderPath);
        }
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        Directory.CreateDirectory(folderPath);
        AssetDatabase.Refresh();
    }

    private static void GenerateDresser(
        DiNeMultiDresser dresser,
        string generatedRootFolder,
        bool clearExistingGeneratedData)
    {
        try
        {
            Debug.Log($"[DiNe] Generating temporary Multi Dresser data: {dresser.name}");
            DiNeMultiIconGenerator.GenerateIcons(dresser);
            dresser.Generate(
                generatedRootFolder,
                clearExistingGeneratedData,
                !clearExistingGeneratedData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Failed to generate '{dresser.name}': {e.Message}\n{e.StackTrace}");
        }
    }

    private static string GetSafeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Avatar";

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Replace("\\", "_").Replace("/", "_");
    }
}
/// <summary>
/// 빌드 클론에 Multi Dresser / Smart Toggle / Lighting Designer를 설치하는 훅.
/// NDMF 본 처리(-11000)·VRCFury(-10000)보다 먼저 돈다. NDMF가 EditorOnly 태그 오브젝트
/// (= Multi Dresser 오브젝트)를 지우기 전에 생성을 끝내야 하기 때문이다.
/// </summary>
internal sealed class DiNeAvatarToolsBuildApplyHook : IVRCSDKPreprocessAvatarCallback
{
    public int callbackOrder => -12000;

    public bool OnPreprocessAvatar(GameObject avatarGameObject)
    {
        DiNeMultiDresserAutoApply.ApplyToBuildAvatar(avatarGameObject);
        return true;
    }
}

/// <summary>아바타 번들 빌드가 끝난 직후 임시 에셋 정리를 예약하는 훅.</summary>
internal sealed class DiNeAvatarToolsBuildPostprocessHook : IVRCSDKPostprocessAvatarCallback
{
    public int callbackOrder => 0;

    public void OnPostprocessAvatar()
    {
        DiNeMultiDresserAutoApply.OnAvatarBuildPostprocessed();
    }
}
#endif
