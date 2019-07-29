using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolutionMerger.Parsers;
using SolutionMerger.Utils;

namespace SolutionMerger.Models
{
    public class Project : BaseProject
    {
        public override string Location { get { return AbsolutePath; } }

        public string AbsolutePath { get; set; }

        public static void GenerateProjectDirs(NestedProjectsInfo nestedSection, List<BaseProject> projects, Dictionary<string, Dictionary<string, string>> allNestedProjects)
        {
            Func<BaseProject, string> getActualSolutionName = p =>
                p is ProjectDirectory || p.Location.IsWebSiteUrl() || p.Location.StartsWith(p.SolutionDir)//Means it is a project that is located inside solution base folder or a project directory or its a website
                    ? p.SolutionName
                    : PathHelpers.GetDirName(Path.GetDirectoryName(Path.GetDirectoryName(p.Location)));

            var groupedSolutions = projects.ToArray().GroupBy(getActualSolutionName);
            foreach (var group in groupedSolutions)
            {
                var dir = new ProjectDirectory(group.Key);
                projects.Add(dir);

                dir.NestedProjectsInfo = nestedSection;
                nestedSection.Dirs.Add(dir);
                dir.NestedProjects.AddRange(group.Select(pr =>
                {
                    if (allNestedProjects.ContainsKey(group.Key) &&
                        allNestedProjects[group.Key].TryGetValue(pr.Guid, out var value))
                    {
                        // Use original subfolder/project association.
                        return new ProjectRelationInfo(pr, new ProjectDirectory(group.Key, value));
                    }

                    // Use updated subfolder/project association - we create a top level folder for each imported
                    // solution, for example, MySolution.sln creates a MySolution folder under the root (e.g. All.sln).
                    // This means that any previous subfolders/projects that used to be directly under the solution now
                    // has the new parent folder. This path handles such cases.
                    return new ProjectRelationInfo(pr, dir);
                }));
            }
        }
    }
}