using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesAutoLoadApi
{
    public class LoaderHelper
    {
        public static string GetProjectDir(Project project)
        {
            try
            {
                var dir = Path.GetDirectoryName(project.FullName);
                return dir;
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public static string GetFileFullName(Project project, string semiPath)
        {
            try
            {
                var prjDir = GetProjectDir(project);

                if (semiPath[0] == '\\')
                    semiPath = semiPath.Substring(1);

                var fullName = Path.Combine(prjDir, semiPath);

                return fullName;
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public static string ExtractSemiPath(Project project, string fullPath)
        {
            var prjDir = GetProjectDir(project);

            if (!FileIoHelper.IsSubDir(fullPath, prjDir))
                throw new ArgumentException("full path must be  a sub directory of project");

            return fullPath.Replace(prjDir, "");
        }
    }
}
