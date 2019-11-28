using EnvDTE;
using EnvDTE80;
using FilesAutoLoadApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace ProjectsFilesAutoLoader
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ProjectsFilesAutoLoaderPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    //https://github.com/madskristensen/SolutionLoadSample
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class ProjectsFilesAutoLoaderPackage : AsyncPackage
    {
        /// <summary>
        /// ProjectsFilesAutoLoaderPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "0100e2bc-fa8a-43dd-90ce-7882b87fed42";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await DirectoryWatcher.InitializeAsync(this);
            await ProjectFileLoaderToolCommand.InitializeAsync(this);

            if (await IsSolutionLoadedAsync())
            {
                InitializeFunctionalApi();
            }

            //
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += InitializeFunctionalApi;
        }

        private async Task<bool> IsSolutionLoadedAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            var solService = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;

            ErrorHandler.ThrowOnFailure(solService.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out object value));

            return value is bool isSolOpen && isSolOpen;
        }



        private void InitializeFunctionalApi(object sender = null, EventArgs e = null)
        {
            DTE2 dte2 = Package.GetGlobalService(typeof(DTE)) as DTE2;
            var sol = dte2.Solution;

            SolutionManager.Initialize(sol);
            Debug.WriteLine("> --- File watch Initilized!");
        }

        #endregion
    }
}
