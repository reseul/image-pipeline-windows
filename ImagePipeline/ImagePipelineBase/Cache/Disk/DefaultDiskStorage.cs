﻿using BinaryResource;
using Cache.Common;
using FBCore.Common.File;
using FBCore.Common.File.Extensions;
using FBCore.Common.Internal;
using FBCore.Common.Time;
using FBCore.Common.Util;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;

namespace Cache.Disk
{
    /// <summary>
    /// The default disk storage implementation. Subsumes both 'simple' and
    /// 'sharded' implementations via a new SubdirectorySupplier.
    /// </summary>
    public class DefaultDiskStorage : IDiskStorage
    {
        private const string CONTENT_FILE_EXTENSION = ".cnt";
        private const string TEMP_FILE_EXTENSION = ".tmp";

        private const string DEFAULT_DISK_STORAGE_VERSION_PREFIX = "v2";

        /// <summary>
        /// We use sharding to avoid Samsung's RFS problem, and to avoid having
        /// one big directory containing thousands of files.
        /// This number of directories is large enough based on the following
        /// reasoning:
        /// - high usage: 150 photos per day.
        /// - such usage will hit Samsung's 6,500 photos cap in 43 days.
        /// - 100 buckets will extend that period to 4,300 days which is 11.78
        ///   years.
        /// </summary>
        private const int SHARDING_BUCKET_COUNT = 100;

        /// <summary>
        /// We will allow purging of any temp files older than this.
        /// </summary>
        internal static readonly long TEMP_FILE_LIFETIME_MS = 
            (long)TimeSpan.FromMinutes(30).TotalMilliseconds;

        private static readonly Random _random = new Random();

        /// <summary>
        /// The base directory used for the cache.
        /// </summary>
        private readonly FileSystemInfo _rootDirectory;

        /// <summary>
       /// True if cache is external.
       /// </summary>
        private readonly bool _isExternal;

        /// <summary>
        /// All the sharding occurs inside a version-directory. That allows 
        /// for easy version upgrade. When we find a base directory with no
        /// version-directory in it, it means that it's a different version
        /// and we should delete the whole directory (including itself) for
        /// both reasons:
        /// 1) Clear all unusable files.
        /// 2) Avoid Samsung RFS problem that was hit with old implementations
        /// of DiskStorage which used a single directory for all the files.
        /// </summary>
        private readonly FileSystemInfo _versionDirectory;

        private readonly ICacheErrorLogger _cacheErrorLogger;
        
        // For unit tests.
        private readonly Clock _clock;

        /// <summary>
        /// Instantiates a ShardedDiskStorage that will use the directory to
        /// save a map between keys and files. The version is very important
        /// if clients change the format saved in those files.
        /// ShardedDiskStorage will assure that files saved with different
        /// version will be never used and eventually removed.
        /// </summary>
        /// <param name="rootDirectory">
        /// Root directory to create all content under.
        /// </param>
        /// <param name="version">
        /// Version of the format used in the files. If passed a different
        /// version files saved with the previous value will not be read and
        /// will be purged eventually.
        /// </param>
        /// <param name="cacheErrorLogger">Logger for various events.</param>
        /// <param name="clock">Optional parameter for unit test.</param>
        public DefaultDiskStorage(
            FileSystemInfo rootDirectory,
            int version,
            ICacheErrorLogger cacheErrorLogger,
            Clock clock = null)
        {
            Preconditions.CheckNotNull(rootDirectory);

            _rootDirectory = rootDirectory;

            // Phong Cao: Checking external storage requires 'Removable devices'
            // permission in the app manifest, skip it for now.
            _isExternal = false; // CheckExternal(rootDirectory, cacheErrorLogger);

            // _versionDirectory's name identifies:
            // - the cache structure's version (sharded)
            // - the content's version (version value)
            // if structure changes, prefix will change... if content changes version
            // will be different the ideal would be asking _sharding its name, but it's
            // created receiving the directory
            _versionDirectory = new DirectoryInfo(Path.Combine(_rootDirectory.FullName, GetVersionSubdirectoryName(version)));
            _cacheErrorLogger = cacheErrorLogger;
            RecreateDirectoryIfVersionChanges();
            _clock = clock ?? SystemClock.Get();
        }

        private static bool CheckExternal(FileSystemInfo directory, ICacheErrorLogger cacheErrorLogger)
        {
            try
            {
                // Get the logical root folder for all external storage devices.
                StorageFolder externalDevices = KnownFolders.RemovableDevices;

                if (directory.FullName.Contains(externalDevices.Path))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                cacheErrorLogger.LogError(
                    CacheErrorCategory.OTHER,
                    typeof(DefaultDiskStorage),
                    "Failed to read folder to check if external: " + directory.Name);
            }

            return false;
        }

        internal static string GetVersionSubdirectoryName(int version)
        {
            return string.Format(
                "{0}.ols{1}.{2}",
                DEFAULT_DISK_STORAGE_VERSION_PREFIX,
                SHARDING_BUCKET_COUNT,
                version);
        }

        /// <summary>
        /// Is this storage enabled?
        /// </summary>
        /// <returns>true, if enabled.</returns>
        public bool IsEnabled
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Is this storage external?
        /// </summary>
        /// <returns>true, if external.</returns>
        public bool IsExternal
        {
            get
            {
                return _isExternal;
            }
        }

        /// <summary>
        /// Get the storage's name, which should be unique.
        /// </summary>
        /// <returns>Name of the this storage.</returns>
        public string StorageName
        {
            get
            {
                string directoryName = _rootDirectory.FullName;
                return "_" + directoryName.Substring(
                    directoryName.LastIndexOf('/') + 1, directoryName.Length)
                    + "_" + directoryName.GetHashCode();
            }
        }

        /// <summary>
        /// Checks if we have to recreate rootDirectory.
        /// This is needed because old versions of this storage created too
        /// much different files in the same dir, and Samsung's RFS has a bug
        /// that after the 13.000th creation fails. So if cache is not already
        /// in expected version let's destroy everything (if not in expected
        /// version... there's nothing to reuse here anyway).
        /// </summary>
        private void RecreateDirectoryIfVersionChanges()
        {
            bool recreateBase = false;
            if (!_rootDirectory.Exists)
            {
                recreateBase = true;
            }
            else if (!_versionDirectory.Exists)
            {
                recreateBase = true;
                FileTree.DeleteRecursively(_rootDirectory);
            }

            if (recreateBase)
            {
                try
                {
                    FileUtils.Mkdirs(_versionDirectory);
                }
                catch (CreateDirectoryException)
                {
                    // Not the end of the world, when saving files we will try to
                    // create missing parent dirs
                    _cacheErrorLogger.LogError(
                        CacheErrorCategory.WRITE_CREATE_DIR,
                        typeof(DefaultDiskStorage),
                        "version directory could not be created: " + _versionDirectory);
                }
            }
        }

        class IncompleteFileException : IOException
        {
            public long Expected { get; }

            public long Actual { get; }

            public IncompleteFileException(long expected, long actual) : 
                base("File was not written completely. Expected: " + expected + ", found: " + actual)
            {
                Expected = expected;
                Actual = actual;
            }
        }

        /// <summary>
        /// Calculates which should be the CONTENT file for the given key.
        /// </summary>
        internal FileSystemInfo GetContentFileFor(string resourceId)
        {
            return new FileInfo(GetFilename(resourceId));
        }

        /// <summary>
        /// Gets the directory to use to store the given key.
        /// </summary>
        /// <param name="resourceId">
        /// The id of the file we're going to store.
        /// </param>
        /// <returns>The directory to store the file in.</returns>
        private string GetSubdirectoryPath(string resourceId)
        {
            int hashCode = HashCodeUtil.HashCode(resourceId);
            string subdirectory = Math.Abs(hashCode % SHARDING_BUCKET_COUNT).ToString();
            return Path.Combine(_versionDirectory.FullName, subdirectory);
        }

        /// <summary>
        /// Gets the directory to use to store the given key.
        /// </summary>
        /// <param name="resourceId">
        /// The id of the file we're going to store.
        /// </param>
        /// <returns>The directory to store the file in.</returns>
        private DirectoryInfo GetSubdirectory(string resourceId)
        {
            return new DirectoryInfo(GetSubdirectoryPath(resourceId));
        }

        /// <summary>
        /// Implementation of <see cref="IFileTreeVisitor"/> to iterate over
        /// all the sharded files and collect those valid content files.
        /// It's used in entriesIterator method.
        /// </summary>
        class EntriesCollector : IFileTreeVisitor
        {
            private readonly List<IEntry> result = new List<IEntry>();
            private readonly DefaultDiskStorage _parent;

            public EntriesCollector(DefaultDiskStorage parent)
            {
                _parent = parent;
            }

            public void PreVisitDirectory(FileSystemInfo directory)
            {
            }

            public void VisitFile(FileSystemInfo file)
            {
                StorageFileInfo info = _parent.GetShardFileInfo(file);
                if (info != null && info.Type.Value == FileType.CONTENT)
                {
                    result.Add(new EntryImpl(info.ResourceId, (FileInfo)file));
                }
            }

            public void PostVisitDirectory(FileSystemInfo directory)
            {
            }

            /// <summary>
            /// Returns an immutable list of the entries.
            /// </summary>
            public IList<IEntry> GetEntries()
            {
                return result.AsReadOnly();
            }
        }

        /// <summary>
        /// This implements a <see cref="IFileTreeVisitor"/> to iterate over all the
        /// files in _directory and delete any unexpected file or directory. It also
        /// gets rid of any empty directory in the shard.
        /// As a shortcut it checks that things are inside (current) _versionDirectory.
        /// If it's not then it's directly deleted. If it's inside then it checks if
        /// it's a recognized file and if it's in the correct shard according to its
        /// name (checkShard method). If it's unexpected file is deleted.
        /// </summary>
        class PurgingVisitor : IFileTreeVisitor
        {
            private bool insideBaseDirectory;
            private readonly DefaultDiskStorage _parent;

            public PurgingVisitor(DefaultDiskStorage parent)
            {
                _parent = parent;
            }

            public void PreVisitDirectory(FileSystemInfo directory)
            {
                if (!insideBaseDirectory && directory.FullName.Equals(_parent._versionDirectory.FullName))
                {
                    // if we enter version-directory turn flag on
                    insideBaseDirectory = true;
                }
            }

            public void VisitFile(FileSystemInfo file)
            {
                if (!insideBaseDirectory || !IsExpectedFile(file))
                {
                    file.Delete();
                }
            }

            public void PostVisitDirectory(FileSystemInfo directory)
            {
                if (!_parent._rootDirectory.FullName.Equals(directory.FullName))
                { // if it's root directory we must not touch it
                    if (!insideBaseDirectory)
                    {
                        // if not in version-directory then it's unexpected!
                        directory.Delete();
                    }
                }

                if (insideBaseDirectory && directory.FullName.Equals(_parent._versionDirectory.FullName))
                {
                    // if we just finished visiting version-directory turn flag off
                    insideBaseDirectory = false;
                }
            }

            private bool IsExpectedFile(FileSystemInfo file)
            {
                StorageFileInfo info = _parent.GetShardFileInfo(file);
                if (info == null)
                {
                    return false;
                }

                if (info.Type.Value == FileType.TEMP)
                {
                    return IsRecentFile(file);
                }

                Preconditions.CheckState(info.Type.Value == FileType.CONTENT);
                return true;
            }

            /// <summary>
            /// Checks if and only if the file is not old enough to be considered
            /// an old temp file.
            /// </summary>
            /// <returns>
            /// true if and only if the file is not old enough to be considered an
            /// old temp file.
            /// </returns>
            private bool IsRecentFile(FileSystemInfo file)
            {
                return file.LastWriteTime > (
                    _parent._clock.Now.Subtract(TimeSpan.FromMilliseconds(TEMP_FILE_LIFETIME_MS)));
            }
        };

        /// <summary>
        /// Purge unexpected resources.
        /// </summary>
        public void PurgeUnexpectedResources()
        {
            FileTree.WalkFileTree(_rootDirectory, new PurgingVisitor(this));
        }

        /// <summary>
        /// Creates the directory (and its parents, if necessary).
        /// In case of an exception, log an error message with the relevant
        /// parameters.
        /// </summary>
        /// <param name="directory">The directory to create.</param>
        /// <param name="message">Message to use.</param>
        /// <exception cref="CreateDirectoryException">
        /// Could not create the directory.
        /// </exception>
        private void Mkdirs(FileSystemInfo directory, string message)
        {
            try
            {
                FileUtils.Mkdirs(directory);
            }
            catch (CreateDirectoryException)
            {
                _cacheErrorLogger.LogError(
                    CacheErrorCategory.WRITE_CREATE_DIR,
                    typeof(DefaultDiskStorage),
                    message);

                throw;
            }
        }

        /// <summary>
        /// Creates a temporary resource for writing content. Split from Commit()
        /// in order to allow concurrent writing of cache entries.
        /// This entry will not be available to cache clients until Commit() is
        /// called passing in the resource returned from this method.
        /// </summary>
        /// <param name="resourceId">Id of the resource.</param>
        /// <param name="debugInfo">Helper object for debugging.</param>
        /// <returns>
        /// The Inserter object with methods to write data, commit or cancel the
        /// insertion.
        /// </returns>
        /// <exception cref="IOException">
        /// On errors during this operation.
        /// </exception>
        public IInserter Insert(string resourceId, object debugInfo)
        {
            // Ensure that the parent directory exists
            StorageFileInfo info = new StorageFileInfo(new FileType(TEMP_FILE_EXTENSION), resourceId);
            DirectoryInfo parent = GetSubdirectory(info.ResourceId);
            if (!parent.Exists)
            {
                Mkdirs(parent, "insert");
            }

            try
            {
                FileInfo file = info.CreateTempFile(parent);
                return new InserterImpl(this, resourceId, file);
            }
            catch (IOException)
            {
                _cacheErrorLogger.LogError(
                    CacheErrorCategory.WRITE_CREATE_TEMPFILE,
                  typeof(DefaultDiskStorage),
                  "insert");

                throw;
            }
        }

        /// <summary>
        /// Get the resource with the specified name.
        /// </summary>
        /// <param name="resourceId">Id of the resource.</param>
        /// <param name="debugInfo">Helper object for debugging.</param>
        /// <returns>
        /// The resource with the specified name. NULL if not found.
        /// </returns>
        /// <exception cref="IOException">
        /// For unexpected behavior.
        /// </exception>
        public IBinaryResource GetResource(string resourceId, object debugInfo)
        {
            FileSystemInfo file = GetContentFileFor(resourceId);
            if (file.Exists)
            {
                file.LastWriteTime = _clock.Now;
                return FileBinaryResource.CreateOrNull((FileInfo)file);
            }

            return null;
        }

        private string GetFilename(string resourceId)
        {
            StorageFileInfo fileInfo = new StorageFileInfo(
                new FileType(CONTENT_FILE_EXTENSION), resourceId);

            string path = GetSubdirectoryPath(fileInfo.ResourceId);
            return fileInfo.ToPath(path);
        }

        /// <summary>
        /// Does a resource with this name exist?
        /// </summary>
        /// <param name="resourceId">Id of the resource.</param>
        /// <param name="debugInfo">Helper object for debugging.</param>
        /// <returns>
        /// true, if the resource is present in the storage, false otherwise.
        /// </returns>
        public bool Contains(string resourceId, object debugInfo)
        {
            return Query(resourceId, false);
        }

        /// <summary>
        /// Does a resource with this name exist? If so, update the last-accessed
        /// time for the resource.
        /// </summary>
        /// <param name="resourceId">Id of the resource.</param>
        /// <param name="debugInfo">Helper object for debugging.</param>
        /// <returns>
        /// true, if the resource is present in the storage, false otherwise.
        /// </returns>
        public bool Touch(string resourceId, object debugInfo)
        {
            return Query(resourceId, true);
        }

        private bool Query(string resourceId, bool touch)
        {
            FileSystemInfo contentFile = GetContentFileFor(resourceId);
            bool exists = contentFile.Exists;
            if (touch && exists)
            {
                contentFile.LastWriteTime = _clock.Now;
            }

            return exists;
        }

        /// <summary>
        /// Remove the resource represented by the entry.
        /// <param name="entry">Entry of the resource to delete.</param>
        /// </summary>
        /// <returns>
        /// Size of deleted file if successfully deleted, -1 otherwise.
        /// </returns>
        public long Remove(IEntry entry)
        {
            // It should be one entry return by us :)
            EntryImpl entryImpl = (EntryImpl)entry;
            FileBinaryResource resource = (FileBinaryResource)entryImpl.Resource;
            return DoRemove(resource.File);
        }

        /// <summary>
        /// Remove the resource with specified id.
        /// </summary>
        /// <param name="resourceId">The resource Id.</param>
        /// <returns>
        /// Size of deleted file if successfully deleted, -1 otherwise.
        /// </returns>
        public long Remove(string resourceId)
        {
            return DoRemove((FileInfo)GetContentFileFor(resourceId));
        }

        private long DoRemove(FileInfo contentFile)
        {
            if (!contentFile.Exists)
            {
                return 0;
            }

            long fileSize = contentFile.Length;

            try
            {
                contentFile.Delete();
            }
            catch (Exception)
            {
                return -1;
            }

            return fileSize;
        }

        /// <summary>
        /// Clear all contents of the storage.
        /// </summary>
        public void ClearAll()
        {
            FileTree.DeleteContents(_rootDirectory);
        }

        /// <summary>
        /// Gets the disk dump info.
        /// </summary>
        public DiskDumpInfo GetDumpInfo()
        {
            ICollection<IEntry> entries = GetEntries();

            DiskDumpInfo dumpInfo = new DiskDumpInfo();
            foreach (IEntry entry in entries)
            {
                DiskDumpInfoEntry infoEntry = DumpCacheEntry(entry);
                string type = infoEntry.Type;
                if (!dumpInfo.TypeCounts.ContainsKey(type))
                {
                    dumpInfo.TypeCounts.Add(type, 0);
                }

                dumpInfo.TypeCounts.Add(type, dumpInfo.TypeCounts[type] + 1);
                dumpInfo.Entries.Add(infoEntry);
            }

            return dumpInfo;
        }

        private DiskDumpInfoEntry DumpCacheEntry(IEntry entry)
        {
            EntryImpl entryImpl = (EntryImpl)entry;
            string firstBits = "";
            byte[] bytes = entryImpl.Resource.Read();
            string type = TypeOfBytes(bytes);
            if (type.Equals("undefined") && bytes.Length >= 4)
            {
                firstBits = string.Format(
                    "0x{0:X} 0x{1:X} 0x{2:X} 0x{3:X}", bytes[0], bytes[1], bytes[2], bytes[3]);
            }

            string path = ((FileBinaryResource)entryImpl.Resource).File.FullName;
            return new DiskDumpInfoEntry(path, type, entryImpl.GetSize(), firstBits);
        }

        private string TypeOfBytes(byte[] bytes)
        {
            if (bytes.Length >= 2)
            {
                if (bytes[0] == 0xFF && bytes[1] == 0xD8)
                {
                    return "jpg";
                }
                else if (bytes[0] == 0x89 && bytes[1] == 0x50)
                {
                    return "png";
                }
                else if (bytes[0] == 0x52 && bytes[1] == 0x49)
                {
                    return "webp";
                }
                else if (bytes[0] == 0x47 && bytes[1] == 0x49)
                {
                    return "gif";
                }
            }

            return "undefined";
        }

        /// <summary>
        /// Returns a list of entries.
        ///
        /// This list is immutable.
        /// </summary>
        public ICollection<IEntry> GetEntries()
        {
            EntriesCollector collector = new EntriesCollector(this);
            _versionDirectory.Refresh();
            FileTree.WalkFileTree(_versionDirectory, collector);
            return collector.GetEntries();
        }

        /// <summary>
        /// Implementation of Entry listed by entriesIterator.
        /// </summary>
        internal class EntryImpl : IEntry
        {
            private readonly string _id;
            private readonly FileBinaryResource _resource;
            private long _size;
            private DateTime _timestamp;

            public EntryImpl(string id, FileInfo cachedFile)
            {
                Preconditions.CheckNotNull(cachedFile);
                _id = Preconditions.CheckNotNull(id);
                _resource = FileBinaryResource.CreateOrNull(cachedFile);
                _size = -1;
                _timestamp = default(DateTime);
            }

            public string Id
            {
                get
                {
                    return _id;
                }
            }

            public DateTime Timestamp
            {
                get
                {
                    if (_timestamp == default(DateTime))
                    {
                        FileInfo cachedFile = _resource.File;

                        try
                        {
                            _timestamp = cachedFile.LastWriteTime;
                        }
                        catch (IOException)
                        {
                            _timestamp = default(DateTime);
                        }
                    }

                    return _timestamp;
                }
            }

            public IBinaryResource Resource
            {
                get
                {
                    return _resource;
                }
            }

            public long GetSize()
            {
                if (_size < 0)
                {
                    _size = _resource.GetSize();
                }

                return _size;
            }
        }

        /// <summary>
        /// Checks that the file is placed in the correct shard according to its
        /// filename (and hence the represented key). If it's correct its
        /// StorageFileInfo is returned.
        /// </summary>
        /// <param name="file">The file to check.</param>
        /// <returns>
        /// The corresponding FileInfo object if shard is correct, null otherwise.
        /// </returns>
        private StorageFileInfo GetShardFileInfo(FileSystemInfo file)
        {
            StorageFileInfo info = StorageFileInfo.FromFile(file);
            if (info == null)
            {
                return null; // file with incorrect name/extension
            }

            DirectoryInfo expectedDirectory = GetSubdirectory(info.ResourceId);
            bool isCorrect = expectedDirectory.FullName.Equals(file.GetParent().FullName);
            return isCorrect ? info : null;
        }

        /// <summary>
        /// Categories for the different internal files a ShardedDiskStorage
        /// maintains.
        /// CONTENT: the file that has the content.
        /// TEMP: temporal files, used to write the content until they are
        /// switched to CONTENT files.
        /// </summary>
        class FileType
        {
            public const int CONTENT = 0;
            public const int TEMP = 1;

            public string Extension { get; }
            public int Value { get; }

            public FileType(string extension)
            {
                Extension = extension;
                Value = FromExtension(extension).Value;
            }

            public static int? FromExtension(string extension)
            {
                if (CONTENT_FILE_EXTENSION.Equals(extension))
                {
                    return CONTENT;
                }
                else if (TEMP_FILE_EXTENSION.Equals(extension))
                {
                    return TEMP;
                }

                return null;
            }
        }

        /// <summary>
        /// Holds information about the different files this storage uses
        /// (content, tmp).
        /// All file name parsing should be done through here.
        /// Temp files creation is also handled here, to encapsulate naming.
        /// </summary>
        class StorageFileInfo
        {
            public FileType Type { get; }

            public string ResourceId { get; }

            public StorageFileInfo(FileType type, string resourceId)
            {
                Type = type;
                ResourceId = resourceId;
            }

            public override string ToString()
            {
                return Type + "(" + ResourceId + ")";
            }

            public string ToPath(string parentPath)
            {
                return Path.Combine(parentPath, ResourceId + Type.Extension);
            }

            public FileInfo CreateTempFile(FileSystemInfo parent)
            {
                string path = Path.Combine(parent.FullName, 
                    ResourceId + "." + _random.Next() + TEMP_FILE_EXTENSION);
                FileInfo file = new FileInfo(path);
                file.CreateEmpty();
                return file;
            }

            public static StorageFileInfo FromFile(FileSystemInfo file)
            {
                string name = file.Name;
                int pos = name.LastIndexOf('.');
                if (pos <= 0)
                {
                    return null; // no name part
                }

                string ext = name.Substring(pos);
                int? type = FileType.FromExtension(ext);
                if (type == null)
                {
                    return null; // unknown!
                }

                string resourceId = name.Substring(0, pos);
                if (type == FileType.TEMP)
                {
                    int numPos = resourceId.LastIndexOf('.');
                    if (numPos <= 0)
                    {
                        return null; // no resourceId.number
                    }

                    resourceId = resourceId.Substring(0, numPos);
                }

                return new StorageFileInfo(new FileType(ext), resourceId);
            }
        }

        internal class InserterImpl : IInserter
        {
            private readonly DefaultDiskStorage _parent;
            private readonly string _resourceId;

            internal readonly FileInfo _temporaryFile;

            public InserterImpl(DefaultDiskStorage parent, string resourceId, FileInfo temporaryFile)
            {
                _parent = parent;
                _resourceId = resourceId;
                _temporaryFile = temporaryFile;
            }

            /// <summary>
            /// Update the contents of the resource to be inserted. Executes
            /// outside the session lock. The writer callback will be provided
            /// with an output Stream to write to. For high efficiency client
            /// should make sure that data is written in big chunks (for example
            /// by employing BufferedInputStream or writing all data at once).
            /// </summary>
            /// <param name="callback">The write callback.</param>
            /// <param name="debugInfo">Helper object for debugging.</param>
            public void WriteData(IWriterCallback callback, object debugInfo)
            {
                FileStream fileStream;

                try
                {
                    fileStream = _temporaryFile.Create();
                }
                catch (Exception)
                {
                    _parent._cacheErrorLogger.LogError(
                    CacheErrorCategory.WRITE_UPDATE_FILE_NOT_FOUND,
                    typeof(DefaultDiskStorage),
                    "updateResource");

                    throw;
                }

                long length;
                try
                {
                    callback.Write(fileStream);

                    // Just in case underlying stream's close method doesn't flush:
                    // we flush it manually and inside the try/catch
                    fileStream.Flush();
                    length = fileStream.Position;
                }
                finally
                {
                    // If it fails to close (or write the last piece) we really want
                    // to know. Normally we would want this to be quiet because a
                    // closing exception would hide one inside the try, but now we
                    // really want to know if something fails at Flush or Close
                    fileStream.Dispose();
                }

                // This code should never throw, but if filesystem doesn't fail on a
                // failing /uncomplete close we want to know and manually fail
                if (_temporaryFile.Length != length)
                {
                    throw new IncompleteFileException(length, _temporaryFile.Length);
                }
            }

            /// <summary>
            /// Commits the insertion into the cache. Once this is called the entry
            /// will be available to clients of the cache.
            /// </summary>
            /// <param name="debugInfo">Debug object for debugging.</param>
            /// <returns>The final resource created.</returns>
            /// <exception cref="IOException">
            /// On errors during the commit.
            /// </exception>
            public IBinaryResource Commit(object debugInfo)
            {
                // The temp resource must be ours!
                FileInfo targetFile = (FileInfo)_parent.GetContentFileFor(_resourceId);

                try
                {
                    FileUtils.Rename(_temporaryFile, targetFile);
                }
                catch (RenameException)
                {
                    CacheErrorCategory category = CacheErrorCategory.WRITE_RENAME_FILE_OTHER;
                    _parent._cacheErrorLogger.LogError(
                        category,
                        typeof(DefaultDiskStorage),
                        "commit");

                    throw;
                }

                if (targetFile.Exists)
                {
                    targetFile.LastWriteTime = _parent._clock.Now;
                }

                return FileBinaryResource.CreateOrNull(targetFile);
            }

            /// <summary>
            /// Discards the insertion process.
            /// If resource was already committed the call is ignored.
            /// </summary>
            /// <returns>
            /// true if cleanUp is successful(or noop), false if something
            /// couldn't be dealt with.
            /// </returns>
            public bool CleanUp()
            {
                if (_temporaryFile.Exists)
                {
                    try
                    {
                        _temporaryFile.Delete();
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
