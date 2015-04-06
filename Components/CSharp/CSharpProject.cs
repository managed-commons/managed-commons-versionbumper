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
using System.Xml.Linq;
using Commons.VersionBumper.Interfaces;
using NuGet.Versioning;

namespace Commons.VersionBumper.Components.CSharp
{
	public class CSharpProject : IProject
	{
		public class ProjectProperty
		{
			public ProjectProperty(string filePath, string value)
			{
				FilePath = filePath;
				Value = value;
			}

			public string FilePath { get; private set; }

			public bool Ok { get { return !string.IsNullOrWhiteSpace(Value); } }

			public string Value { get; private set; }
		}

		public CSharpProject(string projectFileFullPath)
		{
			FullPath = projectFileFullPath;
			_projectDir = Path.GetDirectoryName(FullPath);
			_isWeb = false;
			Name = GetProjectName(projectFileFullPath);
			CurrentVersion = new SemanticVersion(1, 0, 0);
			Description = string.Empty;
			ParseAvailableData();
		}

		public SemanticVersion CurrentVersion { get; private set; }

		public IEnumerable<IReference> Dependencies
		{
			get { return _dependencies; }
		}

		public IEnumerable<IComponent> DependentComponents { get; set; }

		public IEnumerable<IProject> DependentProjects
		{
			get { return DependentComponents.Where(c => c is IProject).Cast<IProject>(); }
		}

		public string Description { get; private set; }

		public string FullPath { get; private set; }

		public string Name { get; protected set; }

		public string OwningStatus
		{
			get
			{
				if (_parents.Count == 0)
					return "Orphan!";
				if (_parents.Count > 1)
					return "MultiOwned!";
				return _parents[0].Name;
			}
		}

		public IEnumerable<ISolution> Parents { get { return _parents; } }

		public virtual string Type { get { return _isWeb ? "C# Web Project" : ("C# {0} Project".FormatWith(ComponentType)); } }

		public void AddParent(ISolution solution)
		{
			if (!_parents.Contains(solution))
				_parents.Add(solution);
		}

		public bool Equals(IReference other)
		{
			return IsEqual(other);
		}

		public override bool Equals(object obj)
		{
			return IsEqual(obj as IReference);
		}

		public override int GetHashCode()
		{
			return FullPath.GetHashCode();
		}

		public bool MatchName(string pattern)
		{
			return string.IsNullOrWhiteSpace(pattern) || Regex.IsMatch(Name, pattern,
				RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
		}

		public VersionPart PartToCascadeBump(VersionPart partBumpedOnDependency)
		{
			return _usesNUnit || (ComponentType == "Library" && !_isWeb) ? partBumpedOnDependency : VersionPart.Patch;
		}

		public void RemoveParent(ISolution solution)
		{
			if (_parents.Contains(solution))
				_parents.Remove(solution);
		}

		public bool SetNewVersion(ILogger logger, SemanticVersion version)
		{
			if (version == CurrentVersion)
				return true;
			try {
				if (_usesVersionProperty)
					return VersionPropertySetNewVersion(logger, version);
				return AssemblyInfoSetNewVersion(logger, version);
			} catch (Exception e) {
				logger.Error(e);
				return false;
			}
		}

		public string ToLongString()
		{
			var sb = new StringBuilder();
			sb.AppendFormat("{0}{1} [{3}]\n  from '{2}'\n  dependencies:\n", Name, CurrentVersionTag, FullPath, Type);
			foreach (var dep in _dependencies)
				sb.AppendFormat("    {0}\n", dep);
			sb.AppendFormat("  needed by\n");
			foreach (var dep in DependentComponents)
				sb.AppendFormat("    {0}\n", dep);
			return sb.ToString();
		}

		public override string ToString()
		{
			return string.Format("{0}{1} - {2} ({5}) [{3}] {4}", Name, CurrentVersionTag, Description, Type, _status, OwningStatus);
		}

		protected bool _isWeb;
		protected string _projectDir;

		protected virtual string GetProjectName(string projectFileFullPath)
		{
			return Path.GetFileNameWithoutExtension(projectFileFullPath);
		}

		protected void ParseAssemblyInfo(IEnumerable<string> sourceFilesList)
		{
			foreach (var sourcePath in sourceFilesList)
				if (ParseAssemblyInfoFile(sourcePath)) {
					_assemblyInfoPath = sourcePath;
					return;
				}
		}

		protected virtual void ParseAvailableData()
		{
			_dependencies.Clear();
			ParseProjectFile();
			_dependencies.Sort((c1, c2) => c1.Name.CompareTo(c2.Name));
		}

		private readonly List<IReference> _dependencies = new List<IReference>();
		private readonly XNamespace _nm = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
		private readonly List<ISolution> _parents = new List<ISolution>();
		private string _assemblyInfoPath;
		private string _assemblyName;
		private string _status;
		private bool _usesNUnit;
		private bool _usesVersionProperty;
		private string _versionPropertyPath;

		private string ComponentType { get; set; }

		private string CurrentVersionTag { get { return string.Format(_isWeb ? " ({0})" : ".{0}", CurrentVersion.ToString()); } }

		private static string TranslateType(bool usesNUnit, string targetType)
		{
			switch (targetType.ToLowerInvariant()) {
				case "exe": return "Console Application";
				case "winexe": return "Desktop Application";
				default: return targetType;
			}
		}

		private bool AssemblyInfoSetNewVersion(ILogger logger, SemanticVersion version)
		{
			if (!File.Exists(_assemblyInfoPath)) {
				logger.Error("There's no file to keep the version information in this component.");
				return false;
			}
			_assemblyInfoPath.SetVersion(version);
			CurrentVersion = version;
			return true;
		}

		private ProjectProperty ExtractProjectProperty(string filePath, string tagName, string @default = null)
		{
			if (File.Exists(filePath)) {
				XDocument project = XDocument.Load(filePath);
				var element = project.Descendants(_nm + tagName).FirstOrDefault();
				if (element != null)
					return new ProjectProperty(filePath, element.Value);
				foreach (var import in project.Descendants(_nm + "Import")) {
					var importedProject = import.Attribute("Project");
					if (importedProject != null) {
						string path = importedProject.Value;
						if (!(string.IsNullOrWhiteSpace(path) || path.Contains('$') || path.ToLower().EndsWith(".targets"))) {
							path = Path.Combine(Path.GetDirectoryName(filePath), path);
							var value = ExtractProjectProperty(path, tagName);
							if (value.Ok)
								return value;
						}
					}
				}
			}
			return new ProjectProperty(filePath, @default);
		}

		private IEnumerable<string> GetListForTag(XDocument project, string tagName)
		{
			foreach (XElement source in project.Descendants(_nm + tagName)) {
				var sourcePath = source.Attribute("Include").Value;
				if (!string.IsNullOrWhiteSpace(sourcePath))
					yield return Path.GetFullPath(_projectDir.Combine(sourcePath));
			}
		}

		private IEnumerable<string> GetListOfReferencedLibraries(XDocument project)
		{
			foreach (XElement reference in project.Descendants(_nm + "Reference"))
				yield return reference.Attribute("Include").Value.Split(',')[0];
		}

		private IEnumerable<string> GetListOfReferencedProjects(XDocument project)
		{
			return GetListForTag(project, "ProjectReference");
		}

		private IEnumerable<string> GetListOfSources(XDocument project)
		{
			return GetListForTag(project, "Compile");
		}

		private bool IsEqual(IReference other)
		{
			return other != null && other is IProject && FullPath == ((IProject)other).FullPath;
		}

		private bool MatchVersionPattern(string info, string pattern)
		{
			var match = Regex.Match(info, pattern, RegexOptions.Multiline);
			if (match.Success) {
				try {
					string version = match.Groups[1].Value;
					version = version.NormalizeVersion();
					CurrentVersion = SemanticVersion.Parse(version);
				} catch {
					return false;
				}
				return true;
			}
			return false;
		}

		private bool ParseAssemblyInfoFile(string sourcePath)
		{
			bool found = false;
			if (File.Exists(sourcePath)) {
				try {
					string info = File.ReadAllText(sourcePath);
					found = MatchVersionPattern(info, "AssemblyInformationalVersion\\(\"([^\"]*)\"\\)") || MatchVersionPattern(info, "AssemblyVersion\\(\"([^\"]*)\"\\)");
					Match match = Regex.Match(info, "AssemblyDescription\\(\"([^\"]+)\"\\)", RegexOptions.Multiline);
					if (match.Success)
						Description = match.Groups[1].Value;
				} catch (Exception e) {
					Console.Error.WriteLine("Could not read file '{0}'. Cause: {1}", sourcePath, e.Message);
				}
			} else
				Console.Error.WriteLine("\nMissing file: {0}", sourcePath);
			return found;
		}

		private void ParseCurrentVersion(XDocument project)
		{
			var appVersionProperty = ExtractProjectProperty(FullPath, "ApplicationVersion");
			var appVersion = appVersionProperty.Value;
			_usesVersionProperty = !string.IsNullOrWhiteSpace(appVersion) && !appVersion.Contains('%');
			if (_usesVersionProperty) {
				_versionPropertyPath = appVersionProperty.FilePath;
				CurrentVersion = SemanticVersion.Parse(appVersion.NormalizeVersion());
				Description = ExtractProjectProperty(FullPath, "ApplicationDescription").Value;
			} else
				ParseAssemblyInfo(GetListOfSources(project));
		}

		private void ParseProjectFile()
		{
			try {
				XDocument project = XDocument.Load(FullPath);
				ParseCurrentVersion(project);
				_isWeb = (project.Descendants(_nm + "WebProjectProperties").Count() > 0);
				_assemblyName = ExtractProjectProperty(FullPath, "AssemblyName", Name).Value;
				_usesNUnit = GetListOfReferencedLibraries(project).Any(s => s.Equals("nunit.framework", StringComparison.OrdinalIgnoreCase));
				ComponentType = TranslateType(_usesNUnit, ExtractProjectProperty(FullPath, "OutputType", "Library").Value);
				foreach (var projectReference in GetListOfReferencedProjects(project))
					_dependencies.Add(new ProjectReference(projectReference));
			} catch (Exception e) {
				_status = "Error while loading: " + e.Message;
			}
		}

		private bool VersionPropertySetNewVersion(ILogger logger, SemanticVersion version)
		{
			XDocument project = XDocument.Load(_versionPropertyPath);
			var element = project.Descendants(_nm + "ApplicationVersion").FirstOrDefault();
			if (element == null)
				return false;
			element.Value = version.ShortVersion();
			project.Save(_versionPropertyPath);
			CurrentVersion = version;
			return true;
		}
	}
}