using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NugetCracker.Interfaces;
using NugetCracker.Data;

namespace NugetCracker.Components.CSharp
{
	public class CSharpProject : IProject
	{
		public IEnumerable<IComponent> DependentComponents { get; set; }
		public IEnumerable<ISolution> Parents { get { return _parents; } }

		readonly List<IReference> _dependencies = new List<IReference>();
		readonly List<ISolution> _parents = new List<ISolution>();
		protected string _assemblyInfoPath;
		protected string _projectDir;
		protected bool _isWeb;
		protected string _assemblyName;
		protected string _targetFrameworkVersion;
		string _status;

		public CSharpProject(string projectFileFullPath)
		{
			FullPath = projectFileFullPath;
			_projectDir = Path.GetDirectoryName(FullPath);
			_isWeb = false;
			Name = GetProjectName(projectFileFullPath);
			CurrentVersion = new Version("1.0.0.0");
			Description = string.Empty;
			ParseAvailableData();
		}

		private static string CreateDirectory(string dir)
		{
			Directory.CreateDirectory(dir);
			return dir;
		}

		protected virtual void ParseAvailableData()
		{
			_dependencies.Clear();
			ParseProjectFile();
			_dependencies.Sort((c1, c2) => c1.Name.CompareTo(c2.Name));
		}

		public virtual VersionPart PartToCascadeBump(VersionPart partBumpedOnDependency)
		{
			return UsesNUnit || (ComponentType == "Library" && !_isWeb) ? partBumpedOnDependency : VersionPart.Revision;
		}

		private static string AddSingleLibReference(string xml, string packageName, string assemblyName, string framework, string installedPackagesDir)
		{
			string packageReference = "\r\n" +
				"    <Reference Include=\"" + packageName + "\" >\r\n" +
				"      <HintPath>" + installedPackagesDir.Combine(packageName) + "\\lib\\" + framework.ToLibFolder() + "\\" + assemblyName + ".dll</HintPath>\r\n" +
				"    </Reference>";
			string pattern = "(<ItemGroup>)()(\\s*<Reference)";
			string replace = "$1" + packageReference + "$3";
			string altPattern = "(</PropertyGroup>\\s*)(<ItemGroup>)";
			string altReplace = "$1<ItemGroup>\r\n    " + packageReference + "\r\n  </ItemGroup>\r\n  $2";
			return xml.RegexReplace(pattern, replace, altPattern, altReplace);
		}

		private void ParseProjectFile()
		{
			try {
				XDocument project = XDocument.Load(FullPath);
				XNamespace nm = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
				ParseAssemblyInfo(GetListOfSources(project, nm));
				_isWeb = (project.Descendants(nm + "WebProjectProperties").Count() > 0);
				_assemblyName = ExtractProjectProperty(project, nm + "AssemblyName", Name);
				_targetFrameworkVersion = ExtractProjectProperty(project, nm + "TargetFrameworkVersion", "v4.0");
				UsesNUnit = GetListOfReferencedLibraries(project, nm).Any(s => s.Equals("nunit.framework", StringComparison.OrdinalIgnoreCase));
				ComponentType = TranslateType(UsesNUnit, ExtractProjectProperty(project, nm + "OutputType", "Library"));
				foreach (var projectReference in GetListOfReferencedProjects(project, nm))
					_dependencies.Add(new ProjectReference(projectReference));
			} catch (Exception e) {
				_status = "Error while loading: " + e.Message;
			}
		}

		private static string TranslateType(bool usesNUnit, string targetType)
		{
			switch (targetType.ToLowerInvariant()) {
				case "exe": return "Console Application";
				case "winexe": return "Desktop Application";
				default: return targetType;
			}
		}

		private static string ExtractProjectProperty(XDocument project, XName name, string @default)
		{
			var element = project.Descendants(name).FirstOrDefault();
			return (element == null) ? @default : element.Value;
		}

		private IEnumerable<string> GetListOfSources(XDocument project, XNamespace nm)
		{
			return GetListForTag(project, nm, "Compile");
		}

		private IEnumerable<string> GetListOfReferencedProjects(XDocument project, XNamespace nm)
		{
			return GetListForTag(project, nm, "ProjectReference");
		}

		private IEnumerable<string> GetListOfReferencedLibraries(XDocument project, XNamespace nm)
		{
			foreach (XElement reference in project.Descendants(nm + "Reference"))
				yield return reference.Attribute("Include").Value.Split(',')[0];
		}

		private bool UsesNUnit { get; set; }

		private IEnumerable<string> GetListForTag(XDocument project, XNamespace nm, string tagName)
		{
			foreach (XElement source in project.Descendants(nm + tagName)) {
				var sourcePath = source.Attribute("Include").Value;
				if (!string.IsNullOrWhiteSpace(sourcePath))
					yield return Path.GetFullPath(_projectDir.Combine(sourcePath));
			}
		}

		protected void ParseAssemblyInfo(IEnumerable<string> sourceFilesList)
		{
			foreach (var sourcePath in sourceFilesList)
				if (ParseAssemblyInfoFile(sourcePath)) {
					_assemblyInfoPath = sourcePath;
					return;
				}
		}

		private bool ParseAssemblyInfoFile(string sourcePath)
		{
			bool found = false;
			if (File.Exists(sourcePath)) {
				try {
					string info = File.ReadAllText(sourcePath);
					string pattern = "AssemblyVersion\\(\"([^\"]*)\"\\)";
					var match = Regex.Match(info, pattern, RegexOptions.Multiline);
					if (match.Success) {
						try {
							string version = match.Groups[1].Value;
							if (version.Contains('*'))
								version = version.Replace('*', '0');
							if (version.Count(c => c == '.') < 3)
								version = version + ".0";
							CurrentVersion = new Version(version);
						} catch {
							return false;
						}
						found = true;
					}
					pattern = "AssemblyDescription\\(\"([^\"]+)\"\\)";
					match = Regex.Match(info, pattern, RegexOptions.Multiline);
					if (match.Success)
						Description = match.Groups[1].Value;
				} catch (Exception e) {
					Console.Error.WriteLine("Could not read file '{0}'. Cause: {1}", sourcePath, e.Message);
				}
			} else
				Console.Error.WriteLine("\nMissing file: {0}", sourcePath);
			return found;
		}

		protected virtual string GetProjectName(string projectFileFullPath)
		{
			return Path.GetFileNameWithoutExtension(projectFileFullPath);
		}


		public bool SetNewVersion(ILogger logger, Version version)
		{
			if (version == CurrentVersion)
				return true;
			if (!File.Exists(_assemblyInfoPath)) {
				logger.Error("There's no file to keep the version information in this component.");
				return false;
			}
			try {
				_assemblyInfoPath.SetVersion(version);
				CurrentVersion = version;
				return true;
			} catch (Exception e) {
				logger.Error(e);
				return false;
			}
		}

		public string Name { get; protected set; }

		public string Description { get; private set; }

		public Version CurrentVersion { get; private set; }

		public string FullPath { get; private set; }

		public string InstalledPackagesDir { get; private set; }

		public IEnumerable<IReference> Dependencies
		{
			get { return _dependencies; }
		}

		public bool Equals(IReference other)
		{
			return IsEqual(other);
		}

		private bool IsEqual(IReference other)
		{
			return other != null && other is IProject && FullPath == ((IProject)other).FullPath;
		}

		public override bool Equals(object obj)
		{
			return IsEqual(obj as IReference);
		}

		public override int GetHashCode()
		{
			return FullPath.GetHashCode();
		}

		private string ComponentType { get; set; }

		public virtual string Type { get { return _isWeb ? "C# Web Project" : ("C# {0} Project".FormatWith(ComponentType)); } }

		private string CurrentVersionTag { get { return string.Format(_isWeb ? " ({0})" : ".{0}", CurrentVersion.ToString(3)); } }

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
			return string.Format("{0}{1} - {2} ({6}) [{3} - {4}] {5}", Name, CurrentVersionTag, Description, Type, _targetFrameworkVersion.ToLibFolder(), _status, OwningStatus);
		}

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

		public bool MatchName(string pattern)
		{
			return string.IsNullOrWhiteSpace(pattern) || Regex.IsMatch(Name, pattern,
				RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
		}

		public string Platform
		{
			get { return _targetFrameworkVersion.ToLibFolder(); }
		}


		public IEnumerable<IProject> DependentProjects
		{
			get { return DependentComponents.Where(c => c is IProject).Cast<IProject>(); }
		}


		public string Version
		{
			get { return CurrentVersion.ToString(4); }
		}


		public void AddParent(ISolution solution)
		{
			if (!_parents.Contains(solution))
				_parents.Add(solution);
		}

		public void RemoveParent(ISolution solution)
		{
			if (_parents.Contains(solution))
				_parents.Remove(solution);
		}
	}
}
