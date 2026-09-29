using System;
using System.Collections.Generic;
using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// The authored data for one effect: four arrays of layers, one per family.
    ///
    /// A profile is immutable at runtime — no runtime code writes into a settings object, so one
    /// profile can back several cameras at once. <see cref="Sanitize"/> runs from
    /// <c>OnValidate</c> only.
    /// </summary>
    [CreateAssetMenu(menuName = "Rain Drop Effect/Rain Profile", fileName = "RainProfile")]
    public sealed class RainProfile : ScriptableObject
    {
        public int Version = 1;

        public StaticLayerSettings[] StaticLayers = Array.Empty<StaticLayerSettings>();
        public SimpleLayerSettings[] SimpleLayers = Array.Empty<SimpleLayerSettings>();
        public FlowLayerSettings[] FlowLayers = Array.Empty<FlowLayerSettings>();
        public FrictionLayerSettings[] FrictionLayers = Array.Empty<FrictionLayerSettings>();

        // Unity zero-initializes an element added with the default inspector's '+' button: field
        // initializers do not run for a plain [Serializable] class grown through SerializedProperty.
        // The declared defaults below therefore only apply on the C# path (new SimpleLayerSettings(),
        // which is what the tests and the Task 26 converter use). The Task 17 inspector must add
        // layers through 'new', not through arraySize/InsertArrayElementAtIndex.
        public int LayerCount =>
            StaticLayers.Length + SimpleLayers.Length + FlowLayers.Length + FrictionLayers.Length;

        /// <summary>Scratch list of original indices, reused so CollectOrdered stays allocation-free.</summary>
        readonly List<int> order = new List<int>(16);

        /// <summary>
        /// Fills <paramref name="into"/> with the enabled layers ordered by
        /// (Depth asc, FamilyOrder asc, array index asc). The first entry draws first, at the bottom.
        /// Stable, and allocation-free beyond growing <paramref name="into"/>.
        /// </summary>
        public void CollectOrdered(List<RainLayerSettings> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            order.Clear();

            AddAll(into, StaticLayers);
            AddAll(into, SimpleLayers);
            AddAll(into, FlowLayers);
            AddAll(into, FrictionLayers);

            InsertionSort(into);
        }

        void AddAll<T>(List<RainLayerSettings> into, T[] layers) where T : RainLayerSettings
        {
            if (layers == null)
            {
                return;
            }

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null || !layer.Enabled)
                {
                    continue;
                }

                into.Add(layer);
                order.Add(i);
            }
        }

        /// <summary>
        /// Insertion sort, because <see cref="List{T}.Sort()"/> is not stable and the tie-break is
        /// the original array index. Layer counts are small (tens), so O(n^2) is free here.
        /// </summary>
        void InsertionSort(List<RainLayerSettings> layers)
        {
            for (var i = 1; i < layers.Count; i++)
            {
                var layer = layers[i];
                var index = order[i];
                var j = i - 1;

                while (j >= 0 && Compare(layers[j], order[j], layer, index) > 0)
                {
                    layers[j + 1] = layers[j];
                    order[j + 1] = order[j];
                    j--;
                }

                layers[j + 1] = layer;
                order[j + 1] = index;
            }
        }

        static int Compare(RainLayerSettings a, int indexA, RainLayerSettings b, int indexB)
        {
            if (a.Depth != b.Depth)
            {
                return a.Depth.CompareTo(b.Depth);
            }

            if (a.FamilyOrder != b.FamilyOrder)
            {
                return a.FamilyOrder.CompareTo(b.FamilyOrder);
            }

            return indexA.CompareTo(indexB);
        }

        /// <summary>
        /// Repairs authoring mistakes in place, the way the legacy <c>InitParams</c> did: inverted
        /// ranges are swapped, counts and durations clamped to something a simulation can run.
        /// Silent by design, except a clamped <c>MaxCount</c>, which loses drops the author asked for.
        /// </summary>
        public void Sanitize()
        {
            foreach (var layer in StaticLayers)
            {
                if (layer == null) continue;
                layer.FadeTime = Mathf.Max(0f, layer.FadeTime);
                layer.Size = new Vector2(Mathf.Max(0f, layer.Size.x), Mathf.Max(0f, layer.Size.y));
            }

            foreach (var layer in SimpleLayers)
            {
                if (layer == null) continue;
                SanitizeEmitter(layer);
                SortComponents(ref layer.SizeMin, ref layer.SizeMax);
            }

            foreach (var layer in FlowLayers)
            {
                if (layer == null) continue;
                SanitizeEmitter(layer);
                SortRange(ref layer.WidthRange);
                SortRange(ref layer.FluctuationRateRange);
                SortRange(ref layer.AccelerationRange);
                layer.MaxPoints = Mathf.Max(4, layer.MaxPoints);
            }

            foreach (var layer in FrictionLayers)
            {
                if (layer == null) continue;
                SanitizeEmitter(layer);
                SortRange(ref layer.WidthRange);
                SortRange(ref layer.AccelerationRange);
                layer.MaxPoints = Mathf.Max(4, layer.MaxPoints);
            }
        }

        void SanitizeEmitter(EmitterLayerSettings layer)
        {
            layer.Duration = Mathf.Max(0f, layer.Duration);
            layer.Delay = Mathf.Max(0f, layer.Delay);

            layer.LifetimeRange = new Vector2(Mathf.Max(0f, layer.LifetimeRange.x), Mathf.Max(0f, layer.LifetimeRange.y));
            SortRange(ref layer.LifetimeRange);

            layer.EmissionRateRange = new Vector2Int(
                Mathf.Max(0, layer.EmissionRateRange.x),
                Mathf.Max(0, layer.EmissionRateRange.y));
            if (layer.EmissionRateRange.x > layer.EmissionRateRange.y)
            {
                layer.EmissionRateRange = new Vector2Int(layer.EmissionRateRange.y, layer.EmissionRateRange.x);
            }

            if (layer.MaxCount > 256)
            {
                RainLog.Warn($"{layer.Name}: MaxCount clamped to 256");
            }

            layer.MaxCount = Mathf.Clamp(layer.MaxCount, 1, 256);
        }

        static void SortRange(ref Vector2 range)
        {
            if (range.x > range.y)
            {
                range = new Vector2(range.y, range.x);
            }
        }

        /// <summary>Swaps per component, so a min of (0.3, 0.1) against a max of (0.2, 0.4) fixes both axes.</summary>
        static void SortComponents(ref Vector2 min, ref Vector2 max)
        {
            if (min.x > max.x)
            {
                (min.x, max.x) = (max.x, min.x);
            }

            if (min.y > max.y)
            {
                (min.y, max.y) = (max.y, min.y);
            }
        }

        void OnValidate() => Sanitize();
    }
}
