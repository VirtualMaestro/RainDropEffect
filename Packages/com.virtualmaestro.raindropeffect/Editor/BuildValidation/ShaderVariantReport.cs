using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace RainDropEffect.Editor
{
    /// <summary>
    /// Counts the shader variants the package contributes to a build (Task 24).
    ///
    /// Variant counts are the usual way a small effect quietly adds minutes to a build and megabytes
    /// to a player. The package declares exactly one multi_compile (<c>_RAIN_BLUR</c>, local and
    /// fragment-only), and this report is what proves it stays that way.
    /// </summary>
    class ShaderVariantReport : IPreprocessShaders
    {
        const string ShaderPrefix = "Hidden/RainDropEffect/";
        const string OutputPath = "Logs/shader-variants.json";

        /// <summary>Counts survive across callbacks within one build; a new build starts fresh.</summary>
        static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();

        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || !shader.name.StartsWith(ShaderPrefix, System.StringComparison.Ordinal))
            {
                return;
            }

            var key = $"{shader.name}|{snippet.passName}|{snippet.shaderType}";
            Counts.TryGetValue(key, out var previous);
            Counts[key] = previous + data.Count;

            Write();
        }

        /// <summary>
        /// Rewritten after every snippet rather than at the end of the build: there is no reliable
        /// "shaders are done" callback, and the file is small.
        /// </summary>
        static void Write()
        {
            var json = new StringBuilder();
            json.Append("{\n  \"variants\": [\n");

            var i = 0;
            foreach (var pair in Counts)
            {
                var parts = pair.Key.Split('|');
                json.Append($"    {{ \"shader\": \"{parts[0]}\", \"pass\": \"{parts[1]}\", " +
                            $"\"stage\": \"{parts[2]}\", \"variantCount\": {pair.Value} }}");
                json.Append(++i < Counts.Count ? ",\n" : "\n");
            }

            json.Append("  ]\n}\n");

            Directory.CreateDirectory("Logs");
            File.WriteAllText(OutputPath, json.ToString());
        }

        [MenuItem("Rain Drop Effect/Validation/Reset Shader Variant Report")]
        static void Reset()
        {
            Counts.Clear();
            RainLog.Verbose("shader variant counters reset");
        }
    }
}
