using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RainDropEffect;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;

/// <summary>
/// Host-only performance driver for Task 25 (scenario sweep) and Task 31 (sustained run).
///
/// Editor Play Mode is not a valid GPU measurement: run this from a Development build of
/// <c>Assets/Host/Benchmark.unity</c>. The driver writes one JSON file and quits, so a build can be
/// launched from a script and its output collected afterwards.
///
/// Never referenced by package code; it lives in the host assembly and ships with no sample.
/// </summary>
public sealed class BenchmarkDriver : MonoBehaviour
{
    private const string LogPrefix = "[RainBenchmark]";

    /// <summary>The camera effect under test. Its profile and quality are driven per scenario.</summary>
    public RainEffect Effect;

    /// <summary>Benchmark profiles, in the order they are measured.</summary>
    public RainProfile[] Profiles = Array.Empty<RainProfile>();

    /// <summary>Frames rendered before the first scenario, to settle shader compilation and pools.</summary>
    public int WarmupFrames = 120;

    /// <summary>Frames sampled per scenario.</summary>
    public int SampleFrames = 600;

    /// <summary>Frames rendered between scenarios, discarded (profile rebuild, first emission).</summary>
    public int SettleFrames = 60;

    /// <summary>Task 31: run one long <c>Bench_Combined</c>/<c>Balanced</c> pass instead of the sweep.</summary>
    public bool SustainedMode;

    /// <summary>Sustained-run length in seconds (12 minutes by default).</summary>
    public float SustainedSeconds = 720f;

    /// <summary>Sustained-run sampling bucket in seconds.</summary>
    public float SustainedBucketSeconds = 10f;

    /// <summary>Quit the player when the run finished. Off in the editor.</summary>
    public bool QuitWhenDone = true;

    /// <summary>Absolute output directory. Empty resolves per platform (see <see cref="ResolveOutputDir"/>).</summary>
    public string OutputDir = string.Empty;

    /// <summary>Label written into the JSON and the file name. Empty uses <see cref="Application.platform"/>.</summary>
    public string PlatformLabel = string.Empty;

    private readonly List<double> frameMs = new List<double>(1024);
    private readonly List<double> cpuMs = new List<double>(1024);
    private readonly List<double> gpuMs = new List<double>(1024);
    private FrameTiming[] timings = new FrameTiming[1];

    private IEnumerator Start()
    {
        if (Effect == null)
        {
            Debug.LogError($"{LogPrefix} no RainEffect assigned; aborting.");
            yield break;
        }

        // The same player serves both runs; the harness picks one on the command line.
        SustainedMode = SustainedMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-sustained") >= 0;

        var dir = ResolveOutputDir();
        Directory.CreateDirectory(dir);
        RainLog.Verbose($"{LogPrefix} output directory: {dir}");
        Debug.Log($"{LogPrefix} output directory: {dir}");

        // The rain must not be running while the warm-up settles the renderer.
        Effect.Profile = null;
        Effect.Rebuild();

        for (var i = 0; i < WarmupFrames; i++)
        {
            yield return null;
        }

        string path;
        if (SustainedMode)
        {
            path = Path.Combine(dir, $"sustained-{Label()}.json");
            yield return RunSustained(path);
        }
        else
        {
            path = Path.Combine(dir, $"benchmark-{Label()}-{Screen.width}x{Screen.height}.json");
            yield return RunSweep(path);
        }

        Debug.Log($"{LogPrefix} wrote {path}");

        // A browser has no file to fetch afterwards: persistentDataPath is IndexedDB. The document
        // goes to the console so it can be copied out of DevTools, and — because reading a console
        // needs an attached debugger — it is also sent to the serving host as a query string, which
        // any static file server records in its request log.
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            var json = File.ReadAllText(path);
            Debug.Log($"{LogPrefix} BEGIN-JSON\n{json}\n{LogPrefix} END-JSON");
            yield return Report(json);
        }

        // Quitting a WebGL player tears the canvas down before the console can be read.
        if (QuitWhenDone && !Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer)
        {
            yield return null;
            Application.Quit(0);
        }
    }

    // ---------------------------------------------------------------- scenario sweep (Task 25)

    private IEnumerator RunSweep(string path)
    {
        var json = new StringBuilder(4096);
        json.Append("{\n");
        AppendEnvironment(json);
        json.Append("  \"scenarios\": [\n");

        var first = true;

        // "none" measures the frame without any rain, once: the delta against it is the effect cost.
        yield return Measure(json, "none", null, RainQuality.Balanced, ref first);

        foreach (var profile in Profiles)
        {
            if (profile == null)
            {
                continue;
            }

            foreach (RainQuality quality in Enum.GetValues(typeof(RainQuality)))
            {
                yield return Measure(json, profile.name, profile, quality, ref first);
            }
        }

        json.Append("\n  ]\n}\n");
        File.WriteAllText(path, json.ToString());
    }

    /// <summary>
    /// Coroutines cannot take a <c>ref</c> parameter, so the comma bookkeeping is done by the caller
    /// through this wrapper: it runs the measurement and appends the record.
    /// </summary>
    private IEnumerator Measure(StringBuilder json, string name, RainProfile profile, RainQuality quality, ref bool first)
    {
        if (!first)
        {
            json.Append(",\n");
        }

        first = false;
        return MeasureRoutine(json, name, profile, quality);
    }

    private IEnumerator MeasureRoutine(StringBuilder json, string name, RainProfile profile, RainQuality quality)
    {
        Effect.Profile = profile;
        Effect.Quality = quality;
        Effect.Rebuild();

        if (profile != null)
        {
            Effect.Play();
        }

        for (var i = 0; i < SettleFrames; i++)
        {
            yield return null;
        }

        frameMs.Clear();
        cpuMs.Clear();
        gpuMs.Clear();

        var memoryBefore = Profiler.GetTotalAllocatedMemoryLong();
        var gcBefore = GC.CollectionCount(0);

        for (var i = 0; i < SampleFrames; i++)
        {
            yield return null;
            Sample();
        }

        var memoryAfter = Profiler.GetTotalAllocatedMemoryLong();
        var gcAfter = GC.CollectionCount(0);

        AppendScenario(json, name, quality, memoryAfter - memoryBefore, gcAfter - gcBefore);

        RainLog.Verbose(
            $"{LogPrefix} {name}/{quality}: frame p50 {Percentile(frameMs, 50):F3} ms, p95 {Percentile(frameMs, 95):F3} ms, " +
            $"gc {gcAfter - gcBefore}, memory delta {memoryAfter - memoryBefore} B");
    }

    // ---------------------------------------------------------------- sustained run (Task 31)

    private IEnumerator RunSustained(string path)
    {
        var profile = Profiles.Length > 0 ? Profiles[Profiles.Length - 1] : null;
        if (profile == null)
        {
            Debug.LogError($"{LogPrefix} sustained mode needs at least one profile; aborting.");
            yield break;
        }

        Effect.Profile = profile;
        Effect.Quality = RainQuality.Balanced;
        Effect.Rebuild();
        Effect.Play();

        for (var i = 0; i < SettleFrames; i++)
        {
            yield return null;
        }

        var json = new StringBuilder(8192);
        json.Append("{\n");
        AppendEnvironment(json);
        json.Append($"  \"profile\": \"{Escape(profile.name)}\",\n");
        json.Append("  \"quality\": \"Balanced\",\n");
        json.Append("  \"buckets\": [\n");

        var elapsed = 0f;
        var bucket = 0;
        var focused = Application.isFocused;
        var focusChanges = 0;
        var firstBucket = true;

        frameMs.Clear();
        cpuMs.Clear();
        gpuMs.Clear();

        var bucketElapsed = 0f;

        while (elapsed < SustainedSeconds)
        {
            yield return null;
            Sample();

            var dt = Time.unscaledDeltaTime;
            elapsed += dt;
            bucketElapsed += dt;

            if (Application.isFocused != focused)
            {
                focused = Application.isFocused;
                focusChanges++;
            }

            if (bucketElapsed < SustainedBucketSeconds)
            {
                continue;
            }

            if (!firstBucket)
            {
                json.Append(",\n");
            }

            firstBucket = false;
            AppendBucket(json, bucket, elapsed, focusChanges);
            bucket++;
            bucketElapsed = 0f;
            frameMs.Clear();
            cpuMs.Clear();
            gpuMs.Clear();
        }

        json.Append("\n  ]\n}\n");
        File.WriteAllText(path, json.ToString());
    }

    /// <summary>
    /// Sends the result to the page's own origin as <c>benchmark-result?json=…</c>. The response is
    /// expected to be a 404: the point is the line the server writes into its request log, which is
    /// the only channel out of a browser that needs no debugger attached.
    /// </summary>
    private IEnumerator Report(string json)
    {
        var url = "benchmark-result?json=" + UnityWebRequest.EscapeURL(json);

        using (var request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            Debug.Log($"{LogPrefix} reported result to the host ({request.responseCode})");
        }
    }

    // ---------------------------------------------------------------- sampling helpers

    private void Sample()
    {
        frameMs.Add(Time.unscaledDeltaTime * 1000.0);

        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, timings) == 0)
        {
            return;
        }

        // Zero means "this platform reported nothing", not "it was free".
        if (timings[0].cpuFrameTime > 0.0)
        {
            cpuMs.Add(timings[0].cpuFrameTime);
        }

        if (timings[0].gpuFrameTime > 0.0)
        {
            gpuMs.Add(timings[0].gpuFrameTime);
        }
    }

    private void AppendScenario(StringBuilder json, string name, RainQuality quality, long memoryDelta, int gcDelta)
    {
        json.Append("    {");
        json.Append($"\"scenario\": \"{Escape(name)}\", ");
        json.Append($"\"quality\": \"{quality}\", ");
        json.Append($"\"frames\": {frameMs.Count}, ");
        AppendStat(json, "frameTime", frameMs);
        json.Append(", ");
        AppendStat(json, "cpuFrameTime", cpuMs);
        json.Append(", ");
        AppendStat(json, "gpuFrameTime", gpuMs);
        json.Append($", \"gcDelta\": {gcDelta}, \"allocatedDeltaBytes\": {memoryDelta}");

        if (gpuMs.Count == 0)
        {
            json.Append(", \"note\": \"GPU timing unavailable on this platform\"");
        }

        json.Append("}");
    }

    private void AppendBucket(StringBuilder json, int index, float elapsed, int focusChanges)
    {
        json.Append("    {");
        json.Append($"\"bucket\": {index}, ");
        json.Append($"\"elapsedSeconds\": {elapsed.ToString("F1", CultureInfo.InvariantCulture)}, ");
        json.Append($"\"frames\": {frameMs.Count}, ");
        AppendStat(json, "frameTime", frameMs);
        json.Append(", ");
        AppendStat(json, "gpuFrameTime", gpuMs);
        json.Append($", \"batteryLevel\": {SystemInfo.batteryLevel.ToString("F2", CultureInfo.InvariantCulture)}");
        json.Append($", \"focusChanges\": {focusChanges}");
        json.Append("}");
    }

    private static void AppendStat(StringBuilder json, string name, List<double> samples)
    {
        if (samples.Count == 0)
        {
            json.Append($"\"{name}P50\": null, \"{name}P95\": null");
            return;
        }

        json.Append($"\"{name}P50\": {Percentile(samples, 50).ToString("F3", CultureInfo.InvariantCulture)}, ");
        json.Append($"\"{name}P95\": {Percentile(samples, 95).ToString("F3", CultureInfo.InvariantCulture)}");
    }

    private void AppendEnvironment(StringBuilder json)
    {
        json.Append($"  \"platform\": \"{Escape(Label())}\",\n");
        json.Append($"  \"unity\": \"{Escape(Application.unityVersion)}\",\n");
        json.Append($"  \"graphicsDevice\": \"{Escape(SystemInfo.graphicsDeviceName)}\",\n");
        json.Append($"  \"graphicsApi\": \"{Escape(SystemInfo.graphicsDeviceType.ToString())}\",\n");
        json.Append($"  \"graphicsVersion\": \"{Escape(SystemInfo.graphicsDeviceVersion)}\",\n");
        json.Append($"  \"processor\": \"{Escape(SystemInfo.processorType)}\",\n");
        json.Append($"  \"os\": \"{Escape(SystemInfo.operatingSystem)}\",\n");
        json.Append($"  \"resolution\": \"{Screen.width}x{Screen.height}\",\n");
        json.Append($"  \"targetFrameRate\": {Application.targetFrameRate}, \"vSyncCount\": {QualitySettings.vSyncCount},\n");
    }

    /// <summary>Nearest-rank percentile over a copy-free sort of the sample list.</summary>
    private static double Percentile(List<double> samples, int percentile)
    {
        if (samples.Count == 0)
        {
            return 0.0;
        }

        samples.Sort();
        var rank = Mathf.CeilToInt(percentile / 100f * samples.Count) - 1;
        return samples[Mathf.Clamp(rank, 0, samples.Count - 1)];
    }

    private string Label()
    {
        return string.IsNullOrEmpty(PlatformLabel) ? Application.platform.ToString() : PlatformLabel;
    }

    /// <summary>
    /// Editor and desktop players write into the project's <c>Logs/</c> when it is reachable;
    /// everything else (WebGL, Android, iOS) writes into <see cref="Application.persistentDataPath"/>,
    /// whose absolute path is logged so the file can be pulled off the device.
    /// A <c>-benchmarkOut &lt;dir&gt;</c> command-line argument overrides both.
    /// </summary>
    private string ResolveOutputDir()
    {
        if (!string.IsNullOrEmpty(OutputDir))
        {
            return OutputDir;
        }

        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-benchmarkOut")
            {
                return args[i + 1];
            }
        }

        if (Application.isEditor)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        }

        return Application.persistentDataPath;
    }

    private static string Escape(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
