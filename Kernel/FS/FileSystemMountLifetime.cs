namespace guideXOS.FS {
    internal enum FileSystemMountAcquireResult {
        Acquired,
        Unavailable
    }

    /// <summary>
    /// A retained reference to one concrete filesystem mount. Replacing the
    /// active mount retires this identity from new acquisitions but does not
    /// invalidate existing leases. Disposal is deferred until the last lease
    /// is released.
    /// </summary>
    internal sealed class FileSystemMountLease {
        private readonly FileSystem _fileSystem;
        private readonly ulong _generation;
        private bool _released;

        internal FileSystemMountLease(FileSystem fileSystem,
                                      ulong generation) {
            _fileSystem = fileSystem;
            _generation = generation;
        }

        internal FileSystem FileSystem {
            get { return _fileSystem; }
        }

        internal ulong Generation { get { return _generation; } }

        internal bool IsValid {
            get { return FileSystemMountLifetime.IsLeaseValid(this); }
        }

        internal bool IsCurrentMount {
            get { return FileSystemMountLifetime.IsLeaseCurrent(this); }
        }

        internal bool TryClone(out FileSystemMountLease clone) {
            return FileSystemMountLifetime.TryClone(this, out clone);
        }

#if GUIDEXOS_HOSTED_TESTS
        internal void Dispose() {
#else
        public override void Dispose() {
#endif
            FileSystemMountLifetime.Release(this);
        }

        internal FileSystem Resource { get { return _fileSystem; } }
        internal bool Released { get { return _released; } set { _released = value; } }
    }

    /// <summary>
    /// Serializes only mount identity changes, lease accounting, and disposal
    /// state transitions. Filesystem I/O is always performed outside the lock.
    /// </summary>
    internal static class FileSystemMountLifetime {
        private static readonly object _sync = new object();
        private static FileSystem _active;
        private static ulong _generation;
        private static bool _generationExhausted;

        internal static FileSystem ActiveFileSystem {
            get {
                lock (_sync) {
                    return _active as FileSystem;
                }
            }
        }

        internal static void SetActiveFileSystem(FileSystem fileSystem) {
            lock (_sync) {
                if (_generationExhausted) {
                    _active = null;
                    return;
                }
                FileSystem resource = fileSystem;
                if (_active == resource) return;
                if (_generation == 0xffffffffffffffffUL) {
                    // Never wrap a mount identity and let an old generation
                    // become current again. Exhaustion fails closed.
                    _active = null;
                    _generationExhausted = true;
                    return;
                }
                _generation++;
                _active = resource;
            }
        }

        internal static FileSystemMountAcquireResult TryAcquireActive(
                out FileSystemMountLease lease) {
            lease = null;
            lock (_sync) {
                if (_generationExhausted || _active == null ||
                        !_active.CanAcquireMountLease ||
                        !_active.TryAddMountLease()) {
                    return FileSystemMountAcquireResult.Unavailable;
                }
                lease = new FileSystemMountLease(_active, _generation);
                return FileSystemMountAcquireResult.Acquired;
            }
        }

        internal static bool IsLeaseValid(FileSystemMountLease lease) {
            if (lease == null) return false;
            lock (_sync) {
                return !lease.Released && lease.Resource != null &&
                    lease.Resource.IsMountResourceAlive;
            }
        }

        internal static bool IsLeaseCurrent(FileSystemMountLease lease) {
            if (lease == null) return false;
            lock (_sync) {
                return !lease.Released && lease.Resource == _active &&
                    lease.Generation == _generation && !_generationExhausted;
            }
        }

        internal static bool TryClone(FileSystemMountLease lease,
                                      out FileSystemMountLease clone) {
            clone = null;
            if (lease == null) return false;
            lock (_sync) {
                FileSystem resource = lease.Resource;
                if (lease.Released || resource == null ||
                        !resource.IsMountResourceAlive ||
                        !resource.TryAddRetainedMountLease()) return false;
                clone = new FileSystemMountLease(resource, lease.Generation);
                return true;
            }
        }

        internal static void Release(FileSystemMountLease lease) {
            if (lease == null) return;
            FileSystem resource;
            bool disposeResource;
            lock (_sync) {
                if (lease.Released) return;
                lease.Released = true;
                resource = lease.Resource;
                disposeResource = resource != null &&
                    resource.ReleaseMountLease();
            }
            if (disposeResource) resource.DisposeMountResource();
        }

        internal static void RequestDisposal(FileSystem resource) {
            if (resource == null) return;
            bool disposeResource;
            lock (_sync) {
                disposeResource = resource.RequestMountDisposal();
            }
            if (disposeResource) resource.DisposeMountResource();
        }
    }
}
