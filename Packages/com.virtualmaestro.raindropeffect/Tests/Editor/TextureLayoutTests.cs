using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Guards the Task 9 contract: the retained textures live in the package and kept the GUIDs the
    /// legacy prefabs reference. A failure here means the converter (Task 26) will resolve nothing.
    /// </summary>
    public class TextureLayoutTests
    {
        const string TextureRoot = "Packages/com.virtualmaestro.raindropeffect/Runtime/Textures";

        /// <summary>Path to GUID as captured before the move.</summary>
        static readonly (string Path, string Guid)[] Expected =
        {
            (TextureRoot + "/Blood/bloodframe.tga", "4997870a5ee7d0741911049125941c2d"),
            (TextureRoot + "/Blood/bloodframe_normal.tga", "64926e2d4f32ac94c95c940d9b5b8908"),
            (TextureRoot + "/Blood/bloodsplatter.tga", "77223c4c944f4e242b4a1188b352ce09"),
            (TextureRoot + "/Blood/bloodsplatter_normal.tga", "51b21dda0698f6d43a51dc2caac913ff"),
            (TextureRoot + "/Frozen/frozenfill.tga", "10feca35f0493ce4e80de83132590881"),
            (TextureRoot + "/Frozen/frozenfill_normal.tga", "6de9a03e47562f74c8412ff380beebad"),
            (TextureRoot + "/Frozen/frozenframe.tga", "3824f004bac15f046b0ad166bf26d59d"),
            (TextureRoot + "/Frozen/frozenframe_normal.tga", "6ad3cdd20666ff74d8f5116b00173f35"),
            (TextureRoot + "/Rain/bubble.tga", "e0b27b588f0e8154285d0c086c9f8202"),
            (TextureRoot + "/Rain/bubble_normal.tga", "febb8fdab5bd7bc4d80feb54b6e2bbdf"),
            (TextureRoot + "/Rain/flow.tga", "d4dda2720c28b0e48a8c9087bab93444"),
            (TextureRoot + "/Rain/flow_normal.tga", "7c66e71cc2774e14a91043b23acd51c1"),
            (TextureRoot + "/Rain/friction_map.tga", "b78240cfb5d9b854ea43a15802177238"),
            (TextureRoot + "/Rain/rain_drop.tga", "6d402e4b65ce1df4086b265a91f6e3d7"),
            (TextureRoot + "/Rain/rain_drop_normal.tga", "8c654113bbefdd047b971b5fbd179807"),
            (TextureRoot + "/Rain/rain_frame.tga", "2afc50fce21c085488d01c6cba5b6131"),
            (TextureRoot + "/Rain/rain_frame_normal.tga", "7b7918192e1f2c945a5d3e9702d07218"),
            (TextureRoot + "/Rain/rain_white.tga", "cea358a05d4440244aa3f2735e2d6e31"),
            (TextureRoot + "/Rain/rain_white_normal.tga", "55c60230f90625342bbf05812c71acd2"),
            (TextureRoot + "/Rain/wash.tga", "c6b05867ecbc3d4449bf472ca695ed8c"),
            (TextureRoot + "/Rain/wash_normal.tga", "6bfe3f8e61aa9cd45a3c63bd619f1559"),
        };

        [Test]
        public void AllPackageTexturesHaveExpectedGuids()
        {
            foreach (var (path, guid) in Expected)
            {
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path), $"GUID changed for {path}");
            }
        }

        [Test]
        public void LegacyTextureFolderIsGone()
        {
            Assert.IsFalse(
                AssetDatabase.IsValidFolder("Assets/RainDropEffect2/Textures"),
                "Legacy texture folder still exists; textures must live only in the package.");
        }

        [Test]
        public void NormalMapsAreImportedAsNormalMaps()
        {
            foreach (var (path, _) in Expected)
            {
                if (!Path.GetFileNameWithoutExtension(path).EndsWith("_normal"))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"No texture importer for {path}");
                Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType, $"Wrong texture type for {path}");
            }
        }
    }
}
