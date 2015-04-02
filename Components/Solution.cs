using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Commons.VersionBumper.Interfaces;

namespace Commons.VersionBumper.Components
{
	public class ProjectInSolution : IFile
	{
		public ProjectInSolution(string name, string fullPath)
		{
			FullPath = fullPath;
			Name = name;
		}

		public string FullPath { get; private set; }

		public string Name { get; private set; }
	}

	public class Solution : ISolution
	{
		public Solution(string solutionFileFullPath)
		{
			FullPath = solutionFileFullPath;
			_solutionDir = Path.GetDirectoryName(FullPath);
			Name = Path.GetFileNameWithoutExtension(FullPath);
			ParseAvailableData(File.ReadAllText(FullPath), (name, path) => _projects.Add(new ProjectInSolution(name, Path.Combine(_solutionDir, path))));
			InstalledPackagesDir = Path.Combine(_solutionDir, "packages");
		}

		public string FullPath { get; private set; }

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

		protected string _solutionDir;

		private static readonly Regex projectFinder =
			new Regex(@"Project\(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}""\)\s*=\s*""([^""]*)""\s*,\s*""([^""]*)""",
				RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static readonly Regex webApplicationFinder =
			new Regex(@"Project\(""{E24C65DC-7377-472B-9ABA-BC803B73C61A}""\)\s*=\s*""([^""]*)""\s*,\s*""([^""]*)""",
				RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private readonly List<ProjectInSolution> _projects = new List<ProjectInSolution>();
	}
}