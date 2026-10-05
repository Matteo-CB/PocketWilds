using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.Profiling;

namespace PocketWilds.Editor.Performance
{
    /// <summary>
    /// Finds who allocates managed memory in Play Mode: records the profiler for a while, then walks each
    /// frame's hierarchy and sums every GC.Alloc sample by its marker path. Editor-side work (EditorLoop)
    /// is ignored, so the result is the game's own steady-state allocations (T22 GC budget: 0 B/frame).
    /// </summary>
    public static class AllocationProbe
    {
        public static void Begin()
        {
            ProfilerDriver.ClearAllFrames();
            Profiler.enabled = true;
            ProfilerDriver.enabled = true;
        }

        public static string Report(int topCount = 25)
        {
            ProfilerDriver.enabled = false;
            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;
            var bytesByPath = new Dictionary<string, long>();
            var framesByPath = new Dictionary<string, int>();
            int frames = 0;
            int framesWithGameAlloc = 0;
            for (int frame = first; frame <= last && frame >= 0; frame++)
            {
                using (var view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.Default,
                           HierarchyFrameDataView.columnGcMemory, false))
                {
                    if (view == null || !view.valid)
                        continue;
                    frames++;
                    var seen = new HashSet<string>();
                    Walk(view, view.GetRootItemID(), "", bytesByPath, seen);
                    if (seen.Count > 0)
                        framesWithGameAlloc++;
                    foreach (var path in seen)
                    {
                        framesByPath.TryGetValue(path, out int count);
                        framesByPath[path] = count + 1;
                    }
                }
            }

            var builder = new StringBuilder();
            builder.AppendLine($"frames {frames}, frames with game allocations {framesWithGameAlloc}");
            foreach (var pair in bytesByPath.OrderByDescending(p => p.Value).Take(topCount))
                builder.AppendLine($"{pair.Value,9} B in {framesByPath[pair.Key],4} frames  {pair.Key}");
            return builder.ToString();
        }

        private static void Walk(HierarchyFrameDataView view, int item, string path, Dictionary<string, long> bytes, HashSet<string> seen)
        {
            var children = new List<int>();
            view.GetItemChildren(item, children);
            foreach (int child in children)
            {
                string name = view.GetItemName(child);
                if (path.Length == 0 && name == "EditorLoop")
                    continue;
                if (name == "GC.Alloc")
                {
                    long size = (long)view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory);
                    bytes.TryGetValue(path, out long total);
                    bytes[path] = total + size;
                    seen.Add(path);
                    continue;
                }
                Walk(view, child, path.Length == 0 ? name : path + " > " + name, bytes, seen);
            }
        }
    }
}
