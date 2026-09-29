using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Guards the package boundary declared in plan decision D5: the runtime assembly must stay
    /// free of editor references and must build for every platform.
    /// </summary>
    public class PackageLayoutTests
    {
        const string RuntimeAsmdef =
            "Packages/com.virtualmaestro.raindropeffect/Runtime/RainDropEffect.Runtime.asmdef";

        [System.Serializable]
        class AsmdefModel
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
        }

        [Test]
        public void RuntimeAssemblyHasNoEditorReference()
        {
            var path = Path.GetFullPath(RuntimeAsmdef);
            Assert.IsTrue(File.Exists(path), $"Runtime asmdef not found at {path}");

            var asmdef = JsonUtility.FromJson<AsmdefModel>(File.ReadAllText(path));
            Assert.AreEqual("RainDropEffect.Runtime", asmdef.name);

            var editorReferences = (asmdef.references ?? new string[0])
                .Where(r => r.Contains("Editor"))
                .ToArray();
            Assert.IsEmpty(editorReferences,
                "Runtime assembly references editor assemblies: " + string.Join(", ", editorReferences));

            Assert.IsEmpty(asmdef.includePlatforms ?? new string[0],
                "Runtime assembly must build for every platform (includePlatforms must be empty).");
        }
    }
}
