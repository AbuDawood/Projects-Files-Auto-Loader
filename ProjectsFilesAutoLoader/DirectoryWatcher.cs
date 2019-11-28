using FilesAutoLoadApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Task = System.Threading.Tasks.Task;

namespace ProjectsFilesAutoLoader
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class DirectoryWatcher
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("7d1b5d9f-b3f7-4a42-bd43-54b8ddb16b50");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryWatcher"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private DirectoryWatcher(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);

            // var menuItem = new MenuCommand(this.Execute, menuCommandID);

            // AND REPLACE IT WITH A DIFFERENT TYPE
            var menuItem = new OleMenuCommand(Execute, menuCommandID);
            menuItem.BeforeQueryStatus += MenuCommand_BeforeQueryStatus;

            commandService.AddCommand(menuItem);

        }

        private void MenuCommand_BeforeQueryStatus(object sender, EventArgs e)
        {
            //// get the menu that fired the event
            //if (sender is OleMenuCommand menuCommand)
            //{
            //    // start by assuming that the menu will not be shown
            //    menuCommand.Visible = false;
            //    menuCommand.Enabled = false;

            //    IsSingleProjectItemSelection(out IVsHierarchy hierarchy, out uint itemid);

            //    // Get the file path
            //    ((IVsProject)hierarchy).GetMkDocument(itemid, out string itemFullPath);
            //    var transformFileInfo = new FileInfo(itemFullPath);

            //    menuCommand.Visible = true;
            //    menuCommand.Enabled = true;

            //    menuCommand. Text= "lskflsdkfldf";
            //}
        }


        /// <summary>
        /// https://dougrathbone.com/blog/2014/02/18/who-said-building-visual-studio-extensions-was-hard
        /// </summary>
        /// <param name="hierarchy"></param>
        /// <param name="itemid"></param>
        /// <returns></returns>
        public static bool IsSingleProjectItemSelection(out IVsHierarchy hierarchy, out uint itemid)
        {
            hierarchy = null;
            itemid = VSConstants.VSITEMID_NIL;
            int hr = VSConstants.S_OK;

            var monitorSelection = Package.GetGlobalService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            var solution = Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution;
            if (monitorSelection == null || solution == null)
            {
                return false;
            }

            IVsMultiItemSelect multiItemSelect = null;
            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr selectionContainerPtr = IntPtr.Zero;

            try
            {
                hr = monitorSelection.GetCurrentSelection(out hierarchyPtr, out itemid, out multiItemSelect, out selectionContainerPtr);

                if (ErrorHandler.Failed(hr) || hierarchyPtr == IntPtr.Zero || itemid == VSConstants.VSITEMID_NIL)
                {
                    // there is no selection
                    return false;
                }

                // multiple items are selected
                if (multiItemSelect != null) return false;

                // there is a hierarchy root node selected, thus it is not a single item inside a project

                if (itemid == VSConstants.VSITEMID_ROOT) return false;

                hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;
                if (hierarchy == null) return false;

                Guid guidProjectID = Guid.Empty;

                if (ErrorHandler.Failed(solution.GetGuidOfProject(hierarchy, out guidProjectID)))
                {
                    return false; // hierarchy is not a project inside the Solution if it does not have a ProjectID Guid
                }

                // if we got this far then there is a single project item selected
                return true;
            }
            finally
            {
                if (selectionContainerPtr != IntPtr.Zero)
                {
                    Marshal.Release(selectionContainerPtr);
                }

                if (hierarchyPtr != IntPtr.Zero)
                {
                    Marshal.Release(hierarchyPtr);
                }
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static DirectoryWatcher Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in DirectoryWatcher's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new DirectoryWatcher(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string title = "File Load Watch";
            string message;

            //----
            IsSingleProjectItemSelection(out IVsHierarchy hierarchy, out uint itemid);

            // Get the file path
            ((IVsProject)hierarchy).GetMkDocument(itemid, out string itemFullPath);

            if (SolutionManager.IsUnderWatch(itemFullPath))
                message = SolutionManager.DropWatch(itemFullPath).msg;
            else
                message = SolutionManager.CreateWatch(itemFullPath).msg;

            //----

            VsShellUtilities.ShowMessageBox(
           this.package,
           message,
           title,
           OLEMSGICON.OLEMSGICON_INFO,
           OLEMSGBUTTON.OLEMSGBUTTON_OK,
           OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
