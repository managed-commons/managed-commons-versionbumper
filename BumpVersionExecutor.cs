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
using Commons.VersionBumper.Components.CSharp;
using Commons.VersionBumper.Data;
using Commons.VersionBumper.Interfaces;
using NuGet.Versioning;

namespace Commons.VersionBumper.Commands
{
    public class BumpVersionExecutor
    {
        public string Help
        {
            get
            {
                return @"B[umpVersion] [options] pattern [pattern] ...

	Bumps up the [AssemblyVersion]/Package SemanticVersion of the components and rebuilds/repackages.
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

    -diag{nostics}
        Prints information about missing source files for projects and missing projects for solutions.   
";
            }
        }

        public void Process(ILogger logger, IEnumerable<string> args, params IComponentsFactory[] factories)
        {
            ComponentsList components = Rescan(logger, factories);
            var partToBump = ParsePartToBump(args);
            var diagnosticsMode = args.Any(s => s.Length > 4 && "-diagnostics".StartsWith(s.ToLower()));
            if (partToBump != VersionPart.None)
                foreach (var componentNamePattern in args.Where(s => !s.StartsWith("-")))
                {
                    var specificComponent = components.FindComponent<IVersionable>(componentNamePattern);
                    if (specificComponent != null)
                        BumpVersion(logger, specificComponent, partToBump);
                }
            logger.Info("==========================");
            logger.Info("Components final versions:");
            foreach (var component in components)
            {
                logger.Info(component.ToString());
                if (diagnosticsMode)
                {
                    var project = component as CSharpProject;
                    if (project != null && project.HasMissingFiles)
                    {
                        logger.ErrorDetail("Missing files for project at {0}", project.FullPath);
                        foreach (var missingFile in project.MissingFiles)
                            logger.ErrorDetail("-- {0}", missingFile);
                    }
                }
            }
            if (diagnosticsMode)
            {
                logger.Info("================");
                logger.Info("Solutions found:");
                foreach (var solution in components.Solutions)
                {
                    logger.Info(solution.ToString());
                    if (solution.HasMissingProjects)
                        solution.DumpMissingProjects(logger);
                }
            }
        }

        private static bool BumpUp(ILogger logger, IVersionable component, VersionPart partToBump)
        {
            var componentName = component.Name;
            SemanticVersion currentVersion = component.CurrentVersion.Bump(VersionPart.None);
            SemanticVersion newVersion = currentVersion.Bump(partToBump);
            if (component.SetNewVersion(logger, newVersion))
            {
                logger.Info("Bumped component '{0}' version from {1} to {2}", componentName, currentVersion.ToString(), newVersion.ToString());
                return true;
            }
            logger.Error("Could not bump component '{0}' version to {1}", componentName, newVersion.ToString());
            return false;
        }

        private static bool BumpVersion(ILogger logger, IVersionable component, VersionPart partToBump)
        {
            logger.Info("Bumping versions. Affected version part: {0} number", partToBump);
            using (logger.Block)
            {
                if (!BumpUp(logger, component, partToBump))
                    return false;
                foreach (IVersionable versionableComponent in component.DependentComponents.As<IVersionable>())
                    BumpUp(logger, versionableComponent, versionableComponent.PartToCascadeBump(partToBump));
            }
            return true;
        }

        private static VersionPart ParsePartToBump(IEnumerable<string> args)
        {
            return TranslateToVersionPart(args.ParseStringParameter("part"));
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

        private static VersionPart TranslateToVersionPart(string part)
        {
            switch (part)
            {
                case "major":
                    return VersionPart.Major;

                case "minor":
                    return VersionPart.Minor;

                case "patch":
                case "build":
                    return VersionPart.Patch;

                default:
                    return VersionPart.None;
            }
        }
    }
}
