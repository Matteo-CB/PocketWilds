#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Simulation;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace PocketWilds.Bootstrap
{
    /// <summary>
    /// T49: the events' availability hook (decision 5 of T48). An event's own assets live in the Addressables group
    /// « LiveOps_&lt;Name&gt; » under the label « liveops.&lt;name&gt; » (<see cref="EventContentGroups"/>). The group is present when its
    /// label has nothing left to download; an event with no labelled content is bundled (the test event, an event whose group
    /// is not built). The size check runs once in the background (never per frame); until it answers, the content counts as
    /// present (the group is local in this build; T55 makes it remote). « Télécharger » fetches the group's bundles.
    /// </summary>
    public sealed class AddressablesEventContent : IEventContentDownload
    {
        private sealed class Group
        {
            public bool Checking;
            public bool Known;
            public long Bytes;
            public bool Downloading;
        }

        private readonly Dictionary<string, Group> _groups = new Dictionary<string, Group>(StringComparer.Ordinal);

        public bool IsPresent(EventDefinition definition)
        {
            var group = Get(definition);
            return !group.Known || group.Bytes <= 0;
        }

        public long DownloadBytes(EventDefinition definition)
        {
            var group = Get(definition);
            return group.Known ? group.Bytes : 0L;
        }

        public bool IsDownloading(EventDefinition definition) => Get(definition).Downloading;

        public void Download(EventDefinition definition, Action<bool>? done)
        {
            var group = Get(definition);
            if (group.Downloading)
                return;
            group.Downloading = true;
            string label = EventContentGroups.LabelOf(definition.Key);
            AsyncOperationHandle handle;
            try
            {
                handle = Addressables.DownloadDependenciesAsync(label, true);
            }
            catch (Exception)
            {
                group.Downloading = false;
                done?.Invoke(false);
                return;
            }
            handle.Completed += op =>
            {
                group.Downloading = false;
                bool ok = op.Status == AsyncOperationStatus.Succeeded;
                if (ok)
                {
                    group.Known = true;
                    group.Bytes = 0;
                }
                done?.Invoke(ok);
            };
        }

        private Group Get(EventDefinition definition)
        {
            string key = definition != null ? definition.Key : string.Empty;
            if (!_groups.TryGetValue(key, out var group))
            {
                group = new Group();
                _groups.Add(key, group);
            }
            if (!group.Known && !group.Checking && key.Length > 0)
                Check(EventContentGroups.LabelOf(key), group);
            return group;
        }

        /// <summary>Locations of the label, then its download size (both asynchronous; Addressables initialize on the first call).</summary>
        private static void Check(string label, Group group)
        {
            group.Checking = true;
            AsyncOperationHandle<IList<IResourceLocation>> locate;
            try
            {
                locate = Addressables.LoadResourceLocationsAsync(label);
            }
            catch (Exception)
            {
                group.Checking = false;
                group.Known = true;
                return;
            }
            locate.Completed += found =>
            {
                bool any = found.Status == AsyncOperationStatus.Succeeded && found.Result != null && found.Result.Count > 0;
                Addressables.Release(found);
                if (!any)
                {
                    // No labelled content: the event is bundled with the player.
                    group.Checking = false;
                    group.Known = true;
                    group.Bytes = 0;
                    return;
                }
                var size = Addressables.GetDownloadSizeAsync(label);
                size.Completed += measured =>
                {
                    group.Checking = false;
                    group.Known = true;
                    group.Bytes = measured.Status == AsyncOperationStatus.Succeeded ? Math.Max(0L, measured.Result) : 0L;
                    Addressables.Release(measured);
                };
            };
        }
    }
}
