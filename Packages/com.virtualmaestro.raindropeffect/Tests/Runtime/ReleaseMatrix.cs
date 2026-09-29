using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Collects the executed rows of the Task 30 release matrix and writes them to
    /// <c>Logs/release-matrix.json</c>, which <c>docs/release-report.md</c> quotes.
    ///
    /// A row is recorded at the end of its test, so a row that is missing from the JSON is a row
    /// whose assertions failed — the test run reports which. This keeps the report honest without a
    /// second bookkeeping mechanism that could disagree with the test result.
    /// </summary>
    internal static class ReleaseMatrix
    {
        const string OutputPath = "Logs/release-matrix.json";

        internal readonly struct Row
        {
            public readonly string Section;
            public readonly string Case;
            public readonly string Method;
            public readonly string Evidence;

            public Row(string section, string caseName, string method, string evidence)
            {
                Section = section;
                Case = caseName;
                Method = method;
                Evidence = evidence;
            }
        }

        static readonly List<Row> Rows = new List<Row>(64);

        public static void Record(string section, string caseName, string method, string evidence)
        {
            Rows.Add(new Row(section, caseName, method, evidence));
            Flush();
        }

        /// <summary>
        /// Written after every row rather than once at the end: a run that dies halfway still leaves
        /// the rows it did prove.
        /// </summary>
        static void Flush()
        {
            var json = new StringBuilder(4096);
            json.Append("{\n  \"rows\": [\n");

            for (var i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                json.Append("    {");
                json.Append($"\"section\": \"{Escape(row.Section)}\", ");
                json.Append($"\"case\": \"{Escape(row.Case)}\", ");
                json.Append($"\"method\": \"{Escape(row.Method)}\", ");
                json.Append($"\"result\": \"PASS\", ");
                json.Append($"\"evidence\": \"{Escape(row.Evidence)}\"");
                json.Append("}");
                json.Append(i < Rows.Count - 1 ? ",\n" : "\n");
            }

            json.Append("  ]\n}\n");

            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText(OutputPath, json.ToString());
            }
            catch (IOException e)
            {
                // A player has no project Logs folder; the rows are still in the test output.
                Debug.LogWarning($"[ReleaseMatrix] could not write {OutputPath}: {e.Message}");
            }
        }

        static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
