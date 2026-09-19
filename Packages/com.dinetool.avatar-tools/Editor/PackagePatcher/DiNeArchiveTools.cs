using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using Debug = UnityEngine.Debug;

/// <summary>
/// .rar / .7z 처럼 .NET 이 직접 열지 못하는 압축 파일을 외부 CLI(7-Zip, UnRAR,
/// Bandizip 등)로 풀어주는 헬퍼. 설치된 도구가 하나도 없으면 실패를 알린다.
/// </summary>
internal static class DiNeArchiveTools
{
    /// <summary>외부 도구가 필요한 확장자인지.</summary>
    public static bool NeedsExternalTool(string ext)
    {
        switch (ext)
        {
            case ".rar":
            case ".7z":
                return true;
            default:
                return false;
        }
    }

    /// <summary>패키지 패쳐가 안을 들여다보는 압축 확장자인지 (중첩 압축 탐색용).</summary>
    public static bool IsArchive(string ext)
    {
        switch (ext)
        {
            case ".zip":
            case ".rar":
            case ".7z":
                return true;
            default:
                return false;
        }
    }

    private class Tool
    {
        public string Path;
        public bool   IsUnrar;   // UnRAR/Rar/WinRAR 계열 (인자 형식이 다름)
        public bool   IsBandizip; // bz.exe (필터 인자 미지원 → 전체 추출)
    }

    private static List<Tool> cachedTools;

    private static List<Tool> EnumerateTools()
    {
        if (cachedTools != null) return cachedTools;

        var list = new List<Tool>();
        void AddSevenZip(string p) { if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(new Tool { Path = p }); }
        void AddUnrar(string p)    { if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(new Tool { Path = p, IsUnrar = true }); }
        void AddBandizip(string p) { if (!string.IsNullOrEmpty(p) && File.Exists(p)) list.Add(new Tool { Path = p, IsBandizip = true }); }

        // 1) 정품 7-Zip (RAR 도 읽을 수 있는 유일한 단일 도구)
        foreach (var root in ProgramFilesRoots())
        {
            AddSevenZip(Path.Combine(root, "7-Zip", "7z.exe"));
            AddSevenZip(Path.Combine(root, "NanaZip", "NanaZipC.exe"));
        }

        // 1-b) 사용자 단위 설치 경로
        try
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localApp))
            {
                AddSevenZip(Path.Combine(localApp, "Programs", "7-Zip", "7z.exe"));
                AddSevenZip(Path.Combine(localApp, "Microsoft", "WinGet", "Links", "7z.exe"));
            }
        }
        catch { }

        // 2) WinRAR (rar 전용이지만 가장 확실하다)
        foreach (var root in ProgramFilesRoots())
        {
            AddUnrar(Path.Combine(root, "WinRAR", "UnRAR.exe"));
            AddUnrar(Path.Combine(root, "WinRAR", "Rar.exe"));
        }

        // 3) Bandizip CLI
        foreach (var root in ProgramFilesRoots())
            AddBandizip(Path.Combine(root, "Bandizip", "bz.exe"));

        // 4) PATH / mac·linux 관례 경로
        foreach (var exe in new[] { "7z", "7zz", "7za", "7z.exe", "7za.exe" })
            AddSevenZip(FindOnPath(exe));
        foreach (var exe in new[] { "unrar", "unrar.exe" })
            AddUnrar(FindOnPath(exe));
        foreach (var p in new[] { "/usr/local/bin/7z", "/opt/homebrew/bin/7z", "/usr/bin/7z", "/usr/local/bin/7zz", "/opt/homebrew/bin/7zz" })
            AddSevenZip(p);
        foreach (var p in new[] { "/usr/local/bin/unrar", "/opt/homebrew/bin/unrar", "/usr/bin/unrar" })
            AddUnrar(p);

        // 5) 마지막 보루: Unity 번들 7z (.7z 는 확실히 되고 .rar 은 빌드에 따라 다름)
        try
        {
            string tools = Path.Combine(EditorApplication.applicationContentsPath, "Tools");
            AddSevenZip(Path.Combine(tools, "7z.exe"));
            AddSevenZip(Path.Combine(tools, "7za"));
            AddSevenZip(Path.Combine(tools, "7z"));
        }
        catch { }

        // 중복 경로 제거 (등록 순서 유지)
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        cachedTools = list.Where(t => seen.Add(t.Path)).ToList();
        return cachedTools;
    }

    private static IEnumerable<string> ProgramFilesRoots()
    {
        foreach (var v in new[] { "ProgramW6432", "ProgramFiles", "ProgramFiles(x86)" })
        {
            string p = null;
            try { p = Environment.GetEnvironmentVariable(v); } catch { }
            if (!string.IsNullOrEmpty(p)) yield return p;
        }
    }

    private static string FindOnPath(string exeName)
    {
        try
        {
            string pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) return null;
            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                string candidate;
                try { candidate = Path.Combine(dir.Trim(), exeName); } catch { continue; }
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch { }
        return null;
    }

    /// <summary>rar/7z 를 풀 수 있는 도구가 하나라도 있는지.</summary>
    public static bool HasAnyTool() => EnumerateTools().Count > 0;

    /// <summary>
    /// 압축 파일 안의 .unitypackage 를 전부 destDir 로 추출한다.
    /// includeNestedArchives 가 켜져 있으면 안쪽의 .zip/.rar/.7z 도 함께 꺼내서
    /// 호출 측이 재귀적으로 들여다볼 수 있게 한다.
    /// 반환값은 추출된 파일의 절대 경로 목록(비어 있을 수 있음).
    /// </summary>
    public static List<string> ExtractUnityPackages(string archivePath, string destDir, bool includeNestedArchives, out string error)
    {
        error = null;
        var tools = EnumerateTools();
        if (tools.Count == 0)
        {
            error = "7-Zip / WinRAR / Bandizip 중 하나가 설치되어 있어야 .rar · .7z 를 열 수 있습니다.";
            return new List<string>();
        }

        var failures = new List<string>();
        foreach (var tool in tools)
        {
            string attemptDir = Path.Combine(destDir, Guid.NewGuid().ToString("N").Substring(0, 8));
            try { Directory.CreateDirectory(attemptDir); }
            catch (Exception e) { error = e.Message; return new List<string>(); }

            string args = BuildArgs(tool, archivePath, attemptDir, includeNestedArchives);
            string toolError;
            bool ran = RunProcess(tool.Path, args, out toolError);

            var extracted = new List<string>();
            try
            {
                extracted = Directory.GetFiles(attemptDir, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        string e = Path.GetExtension(f).ToLowerInvariant();
                        return e == ".unitypackage" || (includeNestedArchives && IsArchive(e));
                    })
                    .ToList();
            }
            catch { }

            // 종료 코드가 나빠도 파일이 나왔으면 성공으로 본다(경고 코드 1 등).
            if (extracted.Count > 0) return extracted;

            try { Directory.Delete(attemptDir, true); } catch { }

            failures.Add($"{Path.GetFileName(tool.Path)}: {(ran ? "unitypackage 없음" : toolError)}");
        }

        error = string.Join(" / ", failures);
        return new List<string>();
    }

    private static string BuildArgs(Tool tool, string archivePath, string destDir, bool includeNestedArchives)
    {
        string masks = includeNestedArchives
            ? "\"*.unitypackage\" \"*.zip\" \"*.rar\" \"*.7z\""
            : "\"*.unitypackage\"";

        if (tool.IsBandizip)
            // bz 는 마스크 필터가 없어 통째로 푼 뒤 스캔한다.
            return $"x -y -o:\"{destDir}\" \"{archivePath}\"";

        if (tool.IsUnrar)
            return $"x -y -idq \"{archivePath}\" {masks} \"{destDir}{Path.DirectorySeparatorChar}\"";

        // 7-Zip: 하위 경로 포함 재귀 매칭
        return $"x -y -bd -r -o\"{destDir}\" \"{archivePath}\" {masks}";
    }

    private static bool RunProcess(string exe, string args, out string error)
    {
        error = null;
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            using (var proc = Process.Start(psi))
            {
                if (proc == null) { error = "프로세스를 시작할 수 없습니다."; return false; }

                // 출력 버퍼가 차서 멈추지 않도록 비동기로 흘려보낸다.
                proc.OutputDataReceived += (s, e) => { };
                proc.ErrorDataReceived  += (s, e) => { };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                if (!proc.WaitForExit(10 * 60 * 1000))
                {
                    try { proc.Kill(); } catch { }
                    error = "압축 해제 시간 초과 (10분)";
                    return false;
                }
                if (proc.ExitCode != 0) error = $"exit {proc.ExitCode}";
                return proc.ExitCode == 0;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}
