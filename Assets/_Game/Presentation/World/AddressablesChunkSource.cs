using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Chunks from Addressables (group N-GRP-BIOME-GRASSLAND, one bundle per chunk): async asset load, then
    /// Object.InstantiateAsync under the streamer's inactive staging root, integrated in time slices over several
    /// frames. Release destroys the instance and releases the handle, which unloads the chunk bundle (and the
    /// meshes only that chunk uses) once nothing else holds it.
    /// </summary>
    public sealed class AddressablesChunkSource : IChunkSource
    {
        public IChunkRequest Request(in WorldChunkSet.Entry entry, Transform stagingParent)
        {
            object key = entry.prefab != null && entry.prefab.RuntimeKeyIsValid() ? entry.prefab.RuntimeKey : entry.id;
            return new ChunkLoad(key, entry.origin, stagingParent);
        }

        private sealed class ChunkLoad : IChunkRequest
        {
            private readonly Vector3 _origin;
            private readonly Transform _parent;
            private AsyncOperationHandle<GameObject> _handle;
            private AsyncInstantiateOperation<GameObject> _instantiate;
            private ChunkRequestStatus _status;
            private WorldChunk _chunk;
            private string _error;
            private bool _released;

            public ChunkLoad(object key, Vector3 origin, Transform parent)
            {
                _origin = origin;
                _parent = parent;
                _handle = Addressables.LoadAssetAsync<GameObject>(key);
            }

            public ChunkRequestStatus Status
            {
                get
                {
                    Advance();
                    return _status;
                }
            }

            public WorldChunk Chunk => _chunk;
            public string Error => _error;

            private void Advance()
            {
                if (_status != ChunkRequestStatus.Pending || _released)
                    return;
                if (_instantiate == null)
                {
                    if (!_handle.IsDone)
                        return;
                    if (_handle.Status != AsyncOperationStatus.Succeeded || _handle.Result == null)
                    {
                        Fail(_handle.OperationException != null ? _handle.OperationException.Message : "asset load failed");
                        return;
                    }
                    _instantiate = Object.InstantiateAsync(_handle.Result, _parent, _origin, Quaternion.identity);
                    return;
                }
                if (!_instantiate.isDone)
                    return;
                var result = _instantiate.Result;
                if (result == null || result.Length == 0 || result[0] == null)
                {
                    Fail("instantiation failed");
                    return;
                }
                _chunk = result[0].GetComponent<WorldChunk>();
                if (_chunk == null)
                {
                    Fail("chunk prefab has no WorldChunk");
                    return;
                }
                _status = ChunkRequestStatus.Ready;
            }

            private void Fail(string message)
            {
                _error = message;
                _status = ChunkRequestStatus.Failed;
            }

            public void Release()
            {
                if (_released)
                    return;
                _released = true;
                if (_instantiate != null)
                {
                    if (!_instantiate.isDone)
                    {
                        _instantiate.Cancel();
                    }
                    else if (_instantiate.Result != null)
                    {
                        foreach (var instance in _instantiate.Result)
                        {
                            if (instance != null)
                                WorldStreamer.DestroySafe(instance);
                        }
                    }
                }
                if (_handle.IsValid())
                    Addressables.Release(_handle);
                _chunk = null;
            }
        }
    }
}
