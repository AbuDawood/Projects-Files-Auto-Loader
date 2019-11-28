using EnvDTE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesAutoLoadApi.Models
{
    public enum TargetEventHandler
    {
        Created,
        Changed,
        Deleted
    }

    public class ProjectManager : IDisposable
    {

        public ProjectManager(Project project)
        {
            WatchedDirectories = new List<FileSystemWatcher>();

            Project = project;

            Initialize();
        }

        private void LoaderConfig_Changed(object sender, FileSystemEventArgs e)
        {
            Initialize();
        }

        //--------------------------------------------------------------------------------------------------------------------------

        public void Initialize()
        {
            this.DisposeAllWatchers();

            disposedValue = false;

            LoaderConfig = ExtensionConfig.Initialize(Project);

            LoaderConfig.ExternalOnChangedHandler = LoaderConfig_Changed;

            var targetDirs = LoaderConfig.GetTargetDirectoriesFullName();

            foreach (var dir in targetDirs)
            {
                foreach (var ext in LoaderConfig.TargetFilesExtensions)
                {
                    WatchedDirectories.Add(Watch(dir, ext, TargetEventHandler.Created));

                    // currentWatchers.Add(Watch(dic, ext, TargetEventHandler.Deleted));

                    // currentWatchers.Add(Watch(dic, ext, TargetEventHandler.Changed));

                    // TODO Think about adding them
                }
            }
        }

        /// <summary>
        /// Attach new folder to the watching list
        /// </summary>
        /// <param name="fullPath">must be a folder path</param>
        public (string msg, bool succeeded) CreateWatch(string fullPath)
        {
            try
            {
                var r = FileIoHelper.IsValidDirectoryToWatch(Project, fullPath);

                if (!r.succeeded)
                    return r;

                // has watched parent
                var parentPath = GetWatchedParent(fullPath);

                if (parentPath != null)
                    return ($"path is under watch as an included sub-directory, parent dir is: {parentPath}", false);

                // remove sub-watched
                var duplicatedWtch = WatchedDirectories.Where(e => FileIoHelper.IsSubDir(e.Path, fullPath)).ToList();

                duplicatedWtch.ForEach(w =>
                {
                    w.Dispose();
                    WatchedDirectories.Remove(w);
                });

                // add watcher
                foreach (var ext in LoaderConfig.TargetFilesExtensions)
                    WatchedDirectories.Add(Watch(fullPath, ext, TargetEventHandler.Created));

                LoaderConfig.UpdateDirectoriesPaths(WatchedDirectories.Select(e => e.Path).Distinct());

                return ($"Subdirectories and Files in '{fullPath}' are under watch ", true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Join(Environment.NewLine, ex.Message, ex.InnerException?.Message, ex.InnerException?.InnerException?.Message));
                Debug.WriteLine(string.Join(Environment.NewLine, ex.Message, ex.InnerException?.Message, ex.InnerException?.InnerException?.Message));

                return ("exception occured during trying to add new watch, check the log", false);
            }
        }

        /// <summary>
        /// Dittach a watched folder from the watching list
        /// </summary>
        /// <param name="fullPath">must be a folder path</param>
        public (string msg, bool succeeded) DropWatch(string fullPath)
        {
            try
            {
                // list bcs targets multiple extensions
                var wtchLst = WatchedDirectories.Where(e => e.Path == fullPath).ToList();

                if (wtchLst == null || !wtchLst.Any())
                    return ("path not found in the current watched list", false);

                wtchLst.ForEach(w =>
                {
                    w.Dispose();
                    WatchedDirectories.Remove(w);
                });

                // update json config file and object without firing init
                LoaderConfig.UpdateDirectoriesPaths(WatchedDirectories.Select(e => e.Path).Distinct());

                return ($"Subdirectories and Files in '{fullPath}' are free from watch ", true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Join(Environment.NewLine, ex.Message, ex.InnerException?.Message, ex.InnerException?.InnerException?.Message));
                Debug.WriteLine(string.Join(Environment.NewLine, ex.Message, ex.InnerException?.Message, ex.InnerException?.InnerException?.Message));

                return ("exception occured during trying to remove existing watch, check the log", false);
                throw;
            }
        }

        public bool IsInUnderWatch(string path)
        {
            if (WatchedDirectories.Any(e => e.Path == path))
                return true;
            return false;
        }

        public bool IsPathManager(string fullPath)
        {
            return FileIoHelper.IsSubDir(fullPath, GetProjectDirectory());
        }

        //--------------------------------------------------------------------------------------------------------------------------

        public string GetProjectDirectory()
        {
            return Path.GetDirectoryName(Project.FullName);
        }

        public string GetWatchedParent(string fullPath)
        {
            var parent = WatchedDirectories.FirstOrDefault(e => FileIoHelper.IsSubDir(fullPath, e.Path));

            if (parent != null)
                return parent.Path;

            return null;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/15017506/using-filesystemwatcher-to-monitor-a-directory
        /// </summary>
        private FileSystemWatcher Watch(string path, string extension, TargetEventHandler targetEventHandler)
        {
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = path,

                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime,

                Filter = extension == string.Empty ? extension : $"*{extension}",

                // TODO: Make this configurable for each path
                // TODO: remove paths that has a watch parent
                IncludeSubdirectories = true,
            };

            switch (targetEventHandler)
            {
                case TargetEventHandler.Created:
                    watcher.Created += new FileSystemEventHandler((obj, evnt) => OnCreated(obj, extension, evnt));
                    break;
                case TargetEventHandler.Deleted:
                    watcher.Deleted += new FileSystemEventHandler((obj, evnt) => OnDeleted(obj, extension, evnt));
                    break;
                case TargetEventHandler.Changed:
                    watcher.Changed += new FileSystemEventHandler((obj, evnt) => OnChanged(obj, extension, evnt));
                    break;
            }

            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        private void DisposeAllWatchers()
        {
            foreach (var wtch in WatchedDirectories)
                wtch.Dispose();

            WatchedDirectories.Clear();

            LoaderConfig?.Dispose();
        }

        public async Task LoadFile(string path)
        {
            Console.WriteLine($"## loading-- '{path}'");

            Debug.WriteLine($"## loading-- '{path}'");


            if (FileIoHelper.PathIsDirectory(path))
                Project.ProjectItems.AddFolder(path);
            else
                Project.ProjectItems.AddFromFile(path);

            await Task.Delay(700);
            await Task.Run(() => Project.Save());
        }

        public async Task UnLoadFile(string path)
        {
            throw new NotImplementedException();
        }

        private void OnCreated(object source, string filter, FileSystemEventArgs e)
        {
            // retrict to directories when filter is empty
            if (filter == string.Empty && !FileIoHelper.PathIsDirectory(e.FullPath))
                return;

            LoadFile(e.FullPath).ConfigureAwait(true).GetAwaiter();
        }

        private void OnDeleted(object source, string filter, FileSystemEventArgs e)
        {
            // retrict to directories when filter is empty
            if (filter == string.Empty && !FileIoHelper.PathIsDirectory(e.FullPath))
                return;

            UnLoadFile(e.FullPath).ConfigureAwait(true).GetAwaiter();
        }

        private static void OnChanged(object source, string filter, FileSystemEventArgs e)
        {
            // retrict to directories when filter is empty
            if (filter == string.Empty && !FileIoHelper.PathIsDirectory(e.FullPath))
                return;

            throw new NotImplementedException();
        }

        //--------------------------------------------------------------------------------------------------------------------------

        public Project Project { get; private set; }

        internal ExtensionConfig LoaderConfig { get; set; }

        public List<FileSystemWatcher> WatchedDirectories { get; set; }

        //--------------------------------------------------------------------------------------------------------------------------

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    DisposeAllWatchers();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ProjectManager()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion





    }
}
