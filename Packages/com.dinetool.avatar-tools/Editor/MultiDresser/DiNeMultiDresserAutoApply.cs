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
        public string descriptorId;
        public string tempFolderPath;
        public string originalFxControllerPath;
        public string originalMenuPath;
        public string originalParametersPath;
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
        public VRCAvatarDescriptor Descriptor;
        public string TempFolderPath;
        public RuntimeAnimatorController OriginalFxController;
        public VRCExpressionsMenu OriginalMenu;
        public VRCExpressionParameters OriginalParameters;
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
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += () => TryRestoreIfIdle("play mode ended");
        }
    }

    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType != VRCSDKRequestedBuildType.Avatar)
            return true;

        try
        {
            BuildInProgress = true;
            EnsureBuilderHooks();
            ApplyAllDressersInScene();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Failed to prepare temporary Multi Dresser data for build: {e.Message}\n{e.StackTrace}");
        }

        return true;
    }

    public bool OnPreprocessAvatar(GameObject avatarGameObject)
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

            ApplyDressersForAvatarRoot(avatarGameObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Failed to apply temporary Multi Dresser data to build avatar: {e.Message}\n{e.StackTrace}");
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

        if (dressers.Length == 0 && smartToggles.Length == 0)
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
            var descriptor = smartToggle.GetComponentInParent<VRCAvatarDescriptor>();
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

        var descriptors = new HashSet<VRCAvatarDescriptor>(groups.Keys);
        descriptors.UnionWith(toggleGroups.Keys);
        foreach (var descriptor in descriptors)
        {
            groups.TryGetValue(descriptor, out var groupedDressers);
            toggleGroups.TryGetValue(descriptor, out var groupedToggles);
            ApplyTemporarySession(
                descriptor,
                groupedDressers ?? new List<DiNeMultiDresser>(),
                groupedToggles ?? new List<DiNeSmartToggle>());
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
        if (dressers.Count == 0 && smartToggles.Count == 0)
            return;

        Debug.Log($"[DiNe] Applying avatar tools to '{avatarGameObject.name}': {dressers.Count} dresser(s), {smartToggles.Count} smart toggle(s).");

        foreach (var dresser in dressers)
        {
            dresser.TryAutoAssignFXController();
        }

        ApplyTemporarySession(descriptor, dressers, smartToggles);
    }

    private static void ApplyTemporarySession(
        VRCAvatarDescriptor descriptor,
        List<DiNeMultiDresser> dressers,
        List<DiNeSmartToggle> smartToggles)
    {
        if (descriptor == null || dressers == null || smartToggles == null ||
            (dressers.Count == 0 && smartToggles.Count == 0))
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
            Debug.LogWarning($"[DiNe] Multi Dresser: '{descriptor.name}'가 이미 임시(_DiNe) 에셋을 가리키고 있어 중복 적용을 건너뜁니다. 진짜 원본은 영속 세션에 보존됩니다.");
            return;
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
                Descriptor = descriptor,
                TempFolderPath = ps.tempFolderPath,
                OriginalFxController = LoadAssetByPath<RuntimeAnimatorController>(ps.originalFxControllerPath),
                OriginalMenu = LoadAssetByPath<VRCExpressionsMenu>(ps.originalMenuPath),
                OriginalParameters = LoadAssetByPath<VRCExpressionParameters>(ps.originalParametersPath)
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

        SetDescriptorFxController(descriptor, session.TempAnimatorController);
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

        if (!hadInMemory && persisted.sessions.Count > 0)
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

        if (hadInMemory || persisted.sessions.Count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DiNe] Multi Dresser 복원 완료 ({reason}). 실패 있음={anyFailed}");
        }
    }

    // 반환값: 완전히 복원되어 임시 폴더를 정리해도 되면 true.
    private static bool RestoreSession(TemporarySession session)
    {
        if (session == null)
            return true;

        // 디스크립터가 파괴됐으면(null) 더 이상 참조가 없으므로 임시 폴더 삭제 안전.
        if (session.Descriptor == null)
        {
            DeleteTemporaryFolder(session.TempFolderPath);
            return true;
        }

        // 원본이 임시(_DiNe) 에셋을 가리키면 그 값으로 되돌리면 안 된다(미싱/오염 유발).
        // 빈 슬롯(null)은 원래 비어있던 정상 상태로 간주한다.
        bool fxOk = !IsTempAsset(session.OriginalFxController);
        bool menuOk = !IsTempAsset(session.OriginalMenu);
        bool paramsOk = !IsTempAsset(session.OriginalParameters);

        if (fxOk) SetDescriptorFxController(session.Descriptor, session.OriginalFxController);
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
        return true;
    }

    // 반환값: 완전히 복원되어 임시 폴더를 정리해도 되면 true.
    private static bool RestorePersistedSession(PersistedSession session)
    {
        if (session == null)
            return true;

        var originalFx = LoadAssetByPath<RuntimeAnimatorController>(session.originalFxControllerPath);
        var originalMenu = LoadAssetByPath<VRCExpressionsMenu>(session.originalMenuPath);
        var originalParams = LoadAssetByPath<VRCExpressionParameters>(session.originalParametersPath);

        // 원본이 비었거나(경로 stale/삭제됨) 임시(_DiNe) 에셋을 가리키면, 그 값으로 덮어쓰면
        // 오히려 missing/오염이 된다. 그런 필드는 건드리지 않고 실패로 처리한다.
        bool fxOk = IsValidOriginal(originalFx, session.originalFxControllerPath);
        bool menuOk = IsValidOriginal(originalMenu, session.originalMenuPath);
        bool paramsOk = IsValidOriginal(originalParams, session.originalParametersPath);

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
            if (fxOk) SetDescriptorFxController(d, originalFx);
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

                var dac = LoadAssetByPath<AnimatorController>(binding.originalAnimatorControllerPath);
                var dmenu = LoadAssetByPath<VRCExpressionsMenu>(binding.originalExpressionsMenuPath);
                if (IsValidOriginal(dac, binding.originalAnimatorControllerPath)) dresser.animatorController = dac;
                if (IsValidOriginal(dmenu, binding.originalExpressionsMenuPath)) dresser.expressionsMenu = dmenu;
                EditorUtility.SetDirty(dresser);
            }
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
    private static bool IsValidOriginal(UnityEngine.Object asset, string path)
    {
        if (asset == null)
            return string.IsNullOrEmpty(path);

        return !IsTempAsset(asset);
    }

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

            var persistedSession = new PersistedSession
            {
                descriptorId = ToGlobalObjectId(session.Descriptor),
                tempFolderPath = session.TempFolderPath,
                originalFxControllerPath = AssetDatabase.GetAssetPath(session.OriginalFxController),
                originalMenuPath = AssetDatabase.GetAssetPath(session.OriginalMenu),
                originalParametersPath = AssetDatabase.GetAssetPath(session.OriginalParameters)
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

            state.sessions.Add(persistedSession);
        }

        if (state.sessions.Count == 0)
        {
            SessionState.EraseString(SessionStateKey);
            return;
        }

        SessionState.SetString(SessionStateKey, JsonUtility.ToJson(state));
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

        return dresser.GetComponentInParent<VRCAvatarDescriptor>()
            ?? dresser.GetComponentInChildren<VRCAvatarDescriptor>();
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

    private static void SetDescriptorFxController(VRCAvatarDescriptor descriptor, RuntimeAnimatorController controller)
    {
        if (descriptor == null)
            return;

        bool updated = false;

        if (descriptor.baseAnimationLayers != null)
        {
            for (int i = 0; i < descriptor.baseAnimationLayers.Length; i++)
            {
                if (descriptor.baseAnimationLayers[i].type != VRCAvatarDescriptor.AnimLayerType.FX)
                    continue;

                var layer = descriptor.baseAnimationLayers[i];
                layer.animatorController = controller;
                descriptor.baseAnimationLayers[i] = layer;
                updated = true;
            }
        }

        if (descriptor.specialAnimationLayers != null)
        {
            for (int i = 0; i < descriptor.specialAnimationLayers.Length; i++)
            {
                if (descriptor.specialAnimationLayers[i].type != VRCAvatarDescriptor.AnimLayerType.FX)
                    continue;

                var layer = descriptor.specialAnimationLayers[i];
                layer.animatorController = controller;
                descriptor.specialAnimationLayers[i] = layer;
                updated = true;
            }
        }

        if (updated)
        {
            EditorUtility.SetDirty(descriptor);
        }
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
#endif
