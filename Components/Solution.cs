// Commons.VersionBumper
//
// Copyright (c) 2012-2015 Rafael 'Monoman' Teixeira, Managed Commons Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Commons.VersionBumper.Interfaces;

namespace Commons.VersionBumper.Components
{
    public class ProjectInSolution : IFile
    {
        public ProjectInSolution(string name, string fullPath)
        {
            FullPath = GetStraighPath(fullPath);
            Name = name.AsSimpleName();
        }

        private string GetStraighPath(string fullPath)
        {
            if (!fullPath.Contains(".."))
                return fullPath;
            var parts = fullPath.Split('\\');
            var neededParts = new List<string>();
            neededParts.Add(parts[0]);
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i] == "..")
                    neededParts.RemoveAt(neededParts.Count - 1);
                else if (parts[i] != ".")
                    neededParts.Add(parts[i]);
            }
            return string.Join("\\", neededParts.ToArray());
        }

        public string FullPath { get; private set; }

        public string Name { get; private set; }
    }

    public class Solution : ISolution
    {
        protected string _solutionDir;

        private static readonly Regex projectFinder =
            new Regex(@"Project\(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}""\)\s*=\s*""([^""]*)""\s*,\s*""([^""]*)""",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex webApplicationFinder =
            new Regex(@"Project\(""{E24C65DC-7377-472B-9ABA-BC803B73C61A}""\)\s*=\s*""([^""]*)""\s*,\s*""([^""]*)""",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly List<ProjectInSolution> _projects = new List<ProjectInSolution>();

        private List<MissingProject> _missingProjects = new List<MissingProject>();

        public Solution(string solutionFileFullPath)
        {
            FullPath = solutionFileFullPath;
            _solutionDir = Path.GetDirectoryName(FullPath);
            Name = Path.GetFileNameWithoutExtension(FullPath);
            ParseAvailableData(File.ReadAllText(FullPath), (name, path) => _projects.Add(new ProjectInSolution(name, Path.Combine(_solutionDir, path))));
            InstalledPackagesDir = Path.Combine(_solutionDir, "packages");
        }

        public string FullPath { get; private set; }

        public bool HasMissingProjects { get { return _missingProjects.Count > 0; } }

        public string InstalledPackagesDir { get; private set; }

        public string Name { get; private set; }

        public IEnumerable<IFile> Projects
        {
            get { return _projects; }
        }

        public static void ParseAvailableData(string solutionText, Action<string, string> addProject)
        {
            var matches = projectFinder.Matches(solutionText).Cast<Match>().Concat(webApplicationFinder.Matches(solutionText).Cast<Match>());
            foreach (var match in matches)
                addProject(match.Groups[1].Value, match.Groups[2].Value);
        }

        public void AddMissingProject(IFile project, IEnumerable<IProject> similarProjects)
        {
            _missingProjects.Add(new MissingProject(project.FullPath, similarProjects));
        }

        public void DumpMissingProjects(ILogger logger)
        {
            foreach (var mp in _missingProjects)
                logger.ErrorDetail(mp.ToString());
        }

        public bool Equals(ISolution other)
        {
            return this.FullPath.Equals(other.FullPath, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool MatchName(string pattern)
        {
            return Regex.IsMatch(Name, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public override string ToString()
        {
            return string.Format("{0} ({1}) - {2} Projects", Name, FullPath, Projects.Count());
        }

        private class MissingProject
        {
            private readonly string _project;
            private readonly List<string> _similarProjects = new List<string>();

            public MissingProject(string project, IEnumerable<IProject> similarProjects)
            {
                _project = project;
                _similarProjects.AddRange(similarProjects.Select(p => p.FullPath));
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Missing project at ").AppendLine(_project);
                if (_similarProjects.Count > 0)
                {
                    sb.AppendLine("-- similar projects at:");
                    foreach (var similarProject in _similarProjects)
                        sb.Append("---- ").AppendLine(similarProject);
                }
                return sb.ToString();
            }
        }
    }
}
