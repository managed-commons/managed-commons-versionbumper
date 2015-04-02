using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NugetCracker.Data;
using NugetCracker.Interfaces;

namespace NugetCracker.Commands
{
	public class BumpVersionExecutor
	{
		public string Help
		{
			get
			{
				return @"B[umpVersion] [options] pattern [pattern] ...

	Bumps up the [AssemblyVersion]/Package Version of the components and rebuilds/repackages. 
	The [AssemblyFileVersion] attribute also is kept in sync with the [AssemblyVersion].

	Update all dependent components to use the new build/package, also bumping up their 
	version numbers using the specific policy for each component type, and them their dependent 
    components and so on. 

	If some components generate a Nuget, the Nuget is published to a temporary output 'source' 
	and the dependent components have their package references updated.

	Options
	-part:major|minor|build|revision		
		Increments the major, minor, build, revision version number. 
		If option is ommitted the default is to increment revision number.
";
			}
		}

		public bool Process(ILogger logger, IEnumerable<string> args, params IComponentsFactory[] factories)
		{
			ComponentsList components = Rescan(logger, factories);
            bool foundOne = false;
			var partToBump = ParsePartToBump(logger, args);
			foreach (var componentNamePattern in args.Where(s => !s.StartsWith("-"))) {
				foundOne = true;
				var specificComponent = components.FindComponent<IVersionable>(componentNamePattern);
				if (specificComponent == null)
					return true;
				BumpVersion(logger, specificComponent, partToBump);
			}
			if (!foundOne) {
				logger.Error("No component pattern specified");
				return true;
			}
			return true;
		}
		private static ComponentsList Rescan(ILogger logger, IComponentsFactory[] factories)
		{
			var components = new ComponentsList();
			components.Clear();
			int scannedDirsCount = 0;
			string path = Path.GetFullPath(Directory.GetCurrentDirectory());
			logger.Info("Scanning '{0}'", path);
			components.Scan(path, factories, s => { logger.Debug(s); scannedDirsCount++; });
			logger.Info("Scanned {0} directories", scannedDirsCount);
			logger.Info("Found {0} component{1}", components.Count, components.Count > 1 ? "s" : "");
			logger.Info("Found {0} solution{1}", components.Solutions.Count, components.Solutions.Count > 1 ? "s" : "");
			logger.Info("Sorting...");
			components.SortByName();
			logger.Info("Finding dependents...");
			components.FindDependents();
			logger.Info("Matching solutions to projects...");
			components.MatchSolutionsToProjects();
			return components;
		}

		private static VersionPart ParsePartToBump(ILogger logger, IEnumerable<string> args)
		{
			var defaultPart = "build";
			var part = args.ParseStringParameter("part", defaultPart);
			var versionPart = TranslateToVersionPart(part);
			if (versionPart != VersionPart.None)
				return versionPart;
			logger.ErrorDetail("Invalid value for 'part' option: '{0}'. Using default value '{1}'.", part, defaultPart);
			return TranslateToVersionPart(defaultPart);
		}

		private static VersionPart TranslateToVersionPart(string part)
		{
			switch (part) {
				case "major":
					return VersionPart.Major;
				case "minor":
					return VersionPart.Minor;
				case "build":
					return VersionPart.Build;
				case "revision":
					return VersionPart.Revision;
				default:
					return VersionPart.None;
			}
		}

		private bool BumpVersion(ILogger logger, IVersionable component, VersionPart partToBump)
		{
			logger.Info("Bumping versions. Affected version part: {0} number", partToBump);
			using (logger.Block) {
				if (!BumpUp(logger, component, partToBump))
					return false;
				foreach (IComponent dependentComponent in component.DependentComponents) {
					if (dependentComponent is IVersionable) {
						var versionableComponent = (IVersionable)dependentComponent;
						BumpUp(logger, versionableComponent, versionableComponent.PartToCascadeBump(partToBump));
					}
				}
			}
			return true;
		}


		private static bool BumpUp(ILogger logger, IVersionable component, VersionPart partToBump)
		{
			var componentName = component.Name;
			Version currentVersion = component.CurrentVersion;
			Version newVersion = currentVersion.Bump(partToBump);
			if (component.SetNewVersion(logger, newVersion)) {
				logger.Info("Bumped component '{0}' version from {1} to {2}", componentName, currentVersion.ToString(), newVersion.ToString());
				return true;
			}
			logger.Error("Could not bump component '{0}' version to {1}", componentName, newVersion.ToString());
			return false;
		}

	}
}
