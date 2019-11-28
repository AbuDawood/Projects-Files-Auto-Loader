using EnvDTE;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace FilesAutoLoadApi
{
    internal class FileIoHelper
    {
        private readonly string[] projectsDir;

        public static readonly IReadOnlyCollection<string> AllowedExt = new List<string> { "dir", ".cs", ".xml", ".js", ".ts", ".txt", ".css", ".html" };


        public FileIoHelper(string[] projectsDir)
        {
            this.projectsDir = projectsDir;
        }

        public static bool IsValidExtension(string ex)
        {

            if (AllowedExt.Any(e => e.ToLower() == ex.ToLower()))
                return true;

            return false;
        }



        /// <summary>
        /// is directory or file
        /// </summary>
        /// <returns></returns>
        public static bool PathIsDirectory(string path)
        {
            FileAttributes attr = File.GetAttributes(path);

            if (attr == FileAttributes.Directory)
                return true;

            return false;
        }

        public static (string msg, bool succeeded) IsValidDirectoryToWatch(Project project, string fullPath)
        {
            if (!File.Exists(project.FullName))
                throw new ArgumentException("Project is unvalid");

            var prjDir = Path.GetDirectoryName(project.FullName);

            // check if has correct syntax
            FileAttributes attr = File.GetAttributes(fullPath);

            if (!PathIsDirectory(fullPath))
            {
                Console.WriteLine("Syntax Error | Chosen path does not directing to a directory");
                return ("Syntax Error | Chosen path does not directing to a directory", false);
            }

            // check if folder exists

            if (!Directory.Exists(fullPath))
            {
                Console.WriteLine("Sem Error | Chosen path does exist");
                return ("Sem Error | Chosen path does exist", false);
            }

            // Get the Sol
            if (!IsSubDir(fullPath, prjDir))
            {
                Console.WriteLine("Sem Error | Dir path must be part of one of solution projects");
                return ("Sem Error | Dir path must be part of one of solution projects", false);
            }

            return ("", true);
        }

        public static bool Equals(string path1, string path2)
        {
            if (path1[path1.Length - 1] == '\\')
                path2 += '\\';
            else if (path2[path2.Length - 1] == '\\')
                path1 += '\\';

            return path1 == path2;

        }

        public static bool IsSubDir(string path, string parentPath)
        {
            DirectoryInfo pathInfo = new DirectoryInfo(path);
            DirectoryInfo parentPathInfo = new DirectoryInfo(parentPath);

            while (pathInfo.Parent != null)
            {
                if (Equals(pathInfo.Parent.FullName, parentPathInfo.FullName))
                    return true;

                else pathInfo = pathInfo.Parent;
            }

            return false;
        }
    }


}
