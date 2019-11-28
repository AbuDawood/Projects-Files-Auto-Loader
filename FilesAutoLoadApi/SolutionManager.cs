using EnvDTE;
using FilesAutoLoadApi.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesAutoLoadApi
{
    public static class SolutionManager
    {
        public static Solution Solution { get; private set; }

        public static readonly List<ProjectManager> ProjectManagers = new List<ProjectManager>();

        public static void Initialize(Solution solution)
        {
            Solution = solution;

            ProjectManagers.ForEach(e => e.Dispose());

            ProjectManagers.Clear();

            foreach (Project project in solution)
            {
                if (File.Exists(project.FullName))
                {
                    var pMngr = new ProjectManager(project);

                    ProjectManagers.Add(pMngr);
                }
            }
        }

        private static ProjectManager GetSuitable(string fullPath)
        {
            return ProjectManagers.FirstOrDefault(e => e.IsPathManager(fullPath));
        }

        public static bool IsUnderWatch(string fullPath)
        {
            var target = GetSuitable(fullPath);

            return target.IsInUnderWatch(fullPath);
        }

        public static (string msg, bool succeeded) CreateWatch(string fullPath)
        {
            var target = GetSuitable(fullPath);

            return target.CreateWatch(fullPath);
        }

        public static (string msg, bool succeeded) DropWatch(string fullPath)
        {
            var target = GetSuitable(fullPath);

            return target.DropWatch(fullPath);
        }
    }

}
