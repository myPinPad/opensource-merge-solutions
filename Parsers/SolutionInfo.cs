﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SolutionMerger.Models;
using SolutionMerger.Utils;

namespace SolutionMerger.Parsers
{
    public class SolutionInfo
    {
        public string Name { get; private set; }
        public string BaseDir { get; private set; }
        public string Text { get; private set; }

        public static SolutionInfo Parse(string slnPath)
        {
            var slnText = File.ReadAllText(slnPath);
            var path = Path.GetFullPath(slnPath);
            var slnBaseDir = Path.GetDirectoryName(path);
            var props = SolutionPropertiesInfo.Parse(slnText);

            var sln =  new SolutionInfo(Path.GetFileNameWithoutExtension(path), slnBaseDir, props, new NestedProjectsInfo()) { Text = slnText };
            sln.Projects = ProjectInfo.Parse(sln);

            return sln;
        }

        private SolutionInfo CreateNestedDirs(Dictionary<string, Dictionary<string, string>> allNestedProjects)
        {
            Project.GenerateProjectDirs(NestedSection, Projects, allNestedProjects);
            return this;
        }

        public void Save()
        {
            File.WriteAllText(Path.Combine(BaseDir, Name + ".sln"), ToString());
        }

        private SolutionInfo(string name, string baseDir, SolutionPropertiesInfo propsSection, NestedProjectsInfo nestedSection)
        {
            Name = name;
            BaseDir = Path.GetFullPath(baseDir);
            NestedSection = nestedSection;
            PropsSection = propsSection;
        }

        public override string ToString()
        {
            return string.Format(
                @"Microsoft Visual Studio Solution File, Format Version 11.00
# Visual Studio 2010{0}
Global
{1}
{2}
EndGlobal
", string.Concat(Projects.Select(p => p.ProjectInfo)), PropsSection, NestedSection);
        }

        public List<BaseProject> Projects { get; private set; }
        private SolutionPropertiesInfo PropsSection { get; set; }
        private NestedProjectsInfo NestedSection { get; set; }

        public static SolutionInfo MergeSolutions(string newName, string baseDir, out string warnings, params SolutionInfo[] solutions)
        {
            var allProjects = solutions.SelectMany(s => s.Projects).Distinct(BaseProject.ProjectGuidLocationComparer).ToList();

            warnings = SolutionDiagnostics.DiagnoseDupeGuids(solutions);

            var mergedSln = new SolutionInfo(newName, baseDir, solutions[0].PropsSection, new NestedProjectsInfo()) { Projects = allProjects };

            // Parse the following section in each .sln file:
            //
            //     GlobalSection(NestedProjects) = preSolution
            //         {GUID A} = {GUID B}
            //         {GUID C} = {GUID D}
            //         ...
            //     EndGlobalSection
            //
            // and determine its original location associations (e.g. A belongs to B, C belongs to D).
            char[] charsToTrim = { ' ', '\t' };
            var nested =
                new Regex(@"GlobalSection\(NestedProjects\)\s=\spreSolution(?<Section>[\s\S]*?)EndGlobalSection",
                    RegexOptions.Multiline | RegexOptions.Compiled);
            var allNestedProjects = new Dictionary<string, Dictionary<string, string>>();
            foreach (var solution in solutions)
            {
                var found = nested.Match(solution.Text).Groups["Section"].Value;
                if (string.IsNullOrEmpty(found))
                    continue;

                var value = found
                    .Trim(charsToTrim)
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Split('='))
                    .ToDictionary(
                        split => split[0].Trim(charsToTrim),  // GUID of this project.
                        split => split[1].Trim(charsToTrim)); // GUID of location where this project belongs.
                allNestedProjects.Add(solution.Name, value);
            }

            mergedSln.CreateNestedDirs(allNestedProjects)
                .Projects.ForEach(pr =>
                {
                    pr.ProjectInfo.SolutionInfo = mergedSln;
                });

            return mergedSln;
        }
    }
}