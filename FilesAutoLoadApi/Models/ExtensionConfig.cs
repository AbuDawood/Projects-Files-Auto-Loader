using EnvDTE;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FilesAutoLoadApi.Models
{
    internal class ExtensionConfig : IDisposable
    {
        public const string configFileName = "projectFilesLoader.json";

        [JsonIgnore]
        public Project Project { get; private set; }

        private FileSystemWatcher jsonConfigWatcher;

        /// <summary>
        /// Try find config file to load as obj, if not, create new default
        /// </summary>
        /// <param name="project">target project</param>
        public static ExtensionConfig Initialize(Project project)
        {
            var fullName = GetFullName(project);

            ExtensionConfig o;

            if (!File.Exists(fullName))
            {
                o = GenerateDefaultConfig(fullName);

                File.WriteAllText(fullName, Newtonsoft.Json.JsonConvert.SerializeObject(o), Encoding.UTF8);

                project.ProjectItems.AddFromFile(fullName);

                project.Save();

            }
            else
                o = GetFromJson(fullName);

            o.Project = project;

            var path = Path.GetDirectoryName(fullName);

            o.jsonConfigWatcher = new FileSystemWatcher(path)
            {
                Filter = Path.GetFileName(fullName),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastAccess,
                EnableRaisingEvents = true
            };

            //https://stackoverflow.com/questions/680698/why-doesnt-filesystemwatcher-detect-changes-from-visual-studio/681078

            o.jsonConfigWatcher.Changed += o.OnChanged;
            o.jsonConfigWatcher.Created += o.OnChanged;
            o.jsonConfigWatcher.Renamed += o.OnChanged;
            o.jsonConfigWatcher.Deleted += o.OnChanged;

            return o;
        }

        private void OnChanged(object obj, FileSystemEventArgs e)
        {
            ExternalOnChangedHandler?.Invoke(obj, e);
        }


        [JsonIgnore]
        public Action<object, FileSystemEventArgs> ExternalOnChangedHandler { get; set; }

        [JsonIgnore]
        public Action<object, FileSystemEventArgs> ExternalOnCreatedHandler { get; set; }



        //----------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// load config file that must exists, exception thrown if not exists
        /// </summary>
        public static ExtensionConfig GetFromJson(string fullName)
        {
            if (!File.Exists(fullName))
                throw new ArgumentException("config file not exists");

            var configTxt = File.ReadAllText(fullName, Encoding.UTF8);

            var configObj = Newtonsoft.Json.JsonConvert.DeserializeObject<ExtensionConfig>(configTxt);

            configObj.FullName = fullName;

            return configObj;
        }

        /// <summary>
        /// get config file full name
        /// </summary>
        /// <param name="project">target project</param>
        public static string GetFullName(Project project)
        {
            if (!File.Exists(project.FullName))
                throw new ArgumentException("project is not valid");

            var dir = LoaderHelper.GetProjectDir(project);

            var configFullPath = Path.Combine(dir, configFileName);

            return configFullPath;
        }

        private static ExtensionConfig GenerateDefaultConfig(string fullName)
        {
            return new ExtensionConfig
            {
                FullName = fullName,
                TargetDirectoriesPaths = new List<string>(),
                TargetFilesExtensions = new List<string> { "dir", ".cs", ".js", ".ts", ".html" },
            };

        }

        public IReadOnlyCollection<string> GetTargetDirectoriesFullName()
        {
            var lst = new List<string>();

            foreach (var semDir in TargetDirectoriesPaths)
                lst.Add(LoaderHelper.GetFileFullName(Project, semDir));

            return lst;
        }

        public void UpdateDirectoriesPaths(IEnumerable<string> newFullNames)
        {
            var lst = new List<string>();
            foreach (var fn in newFullNames)
                lst.Add(LoaderHelper.ExtractSemiPath(Project, fn));

            this.TargetDirectoriesPaths = lst;

            jsonConfigWatcher.Changed -= OnChanged;

            File.WriteAllText(GetFullName(Project), Newtonsoft.Json.JsonConvert.SerializeObject(this));

            jsonConfigWatcher.Changed += OnChanged;


        }

        //--[PROPERTIES]-------------------------------------------------------------------------------------------------------------------------

        [JsonProperty("paths")]
        public IReadOnlyCollection<string> TargetDirectoriesPaths { get; set; }

        [JsonProperty("extensions")]
        public IReadOnlyCollection<string> TargetFilesExtensions { get; set; }

        [JsonIgnore]
        public string FullName { get; set; }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    jsonConfigWatcher.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ExtensionConfig()
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
