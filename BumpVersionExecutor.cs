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
        public string Help => @"B[umpVersion] [options] pattern [pattern] ...

	Bumps up the [AssemblyVersion] SemanticVersion of the components.

    The [AssemblyFileVersion] and [AssemblyInformationalVersion] attributes are also kept
    in sync with the [AssemblyVersion].

	Update all dependent components to use the new build/package, also bumping up their
	version numbers using the specific policy for each component type, and them their dependent
    components and so on.

    Patterns are interpreted as regular expressions to match a single component (project/web site)
    name, each.

	Options
	  -part:major|minor|build|patch
		  Increments the major, minor, build (patch) version number.
		  If option is ommitted the default is not increment any number.

    -de{bug}
        Prints debug messages.

    -diag{nostics}
        Prints information about missing source files for projects and missing projects for solutions.

    -q{uiet}
        Prints minimal information.
";

        public void Process(ILogger logger, IEnumerable<string> args, params IComponentsFactory[] factories)
        {
            var partToBump = ParsePartToBump(args);
            var diagnosticsMode = args.HasParameter("diagnostics", 4);
            var debugMode = args.HasParameter("debug", 2);
            var quietMode = args.HasParameter("quiet") && !(diagnosticsMode || debugMode);
            var patterns = args.NonParameters();
            Execute(logger, factories, partToBump, diagnosticsMode, quietMode, debugMode, patterns);
        }


        static bool BumpUp(ILogger logger, IVersionable component, VersionPart partToBump)
        {
            var componentName = component.Name;
            SemanticVersion currentVersion = component.CurrentVersion.Bump(VersionPart.None);
            SemanticVersion newVersion = currentVersion.Bump(partToBump);
            if (component.SetNewVersion(logger, newVersion)) {
                logger.Info("Bumped component '{0}' version from {1} to {2}", componentName, currentVersion.ToString(), newVersion.ToString());
                return true;
            }
            logger.Error("Could not bump component '{0}' version to {1}", componentName, newVersion.ToString());
            return false;
        }

        static bool BumpVersion(ILogger logger, IVersionable component, VersionPart partToBump)
        {
            Separator(logger);
            logger.Info("Bumping versions...");
            using (logger.Block) {
                if (!BumpUp(logger, component, partToBump))
                    return false;
                foreach (IVersionable versionableComponent in component.DependentComponents.As<IVersionable>())
                    BumpUp(logger, versionableComponent, versionableComponent.PartToCascadeBump(partToBump));
            }
            return true;
        }

        static void Execute(ILogger logger, IComponentsFactory[] factories, VersionPart partToBump, bool diagnosticsMode, bool quietMode, bool debugMode, IEnumerable<string> patterns)
        {
            logger.QuietMode = quietMode;
            logger.IsDebugEnabled = debugMode;
            LogHeader(logger, partToBump, diagnosticsMode, debugMode, patterns);
            Separator(logger);
            ComponentsList components = Rescan(logger, factories);
            if (partToBump != VersionPart.None) {
                foreach (var componentNamePattern in patterns) {
                    var specificComponent = components.FindComponent<IVersionable>(logger, componentNamePattern);
                    if (specificComponent != null)
                        BumpVersion(logger, specificComponent, partToBump);
                }
            }

            Separator(logger);
            logger.Info("Components final versions:");
            using (logger.Block) {
                foreach (var component in components)
                    component.DumpTo(logger, diagnosticsMode);
            }
            if (diagnosticsMode) {
                Separator(logger);
                logger.Info("Solutions found:");
                using (logger.Block) {
                    foreach (var solution in components.Solutions) {
                        logger.Info(solution.ToString());
                        if (solution.HasMissingProjects)
                            solution.DumpMissingProjects(logger);
                    }
                }
            }
        }

        static void LogHeader(ILogger logger, VersionPart partToBump, bool diagnosticsMode, bool debugMode, IEnumerable<string> patterns)
        {
            var info = AssemblyInformation.FromEntryAssembly;
            Separator(logger);
            logger.Info($"{info.ExeName} {info.Version} bumping versions with");
            using (logger.Block) {
                logger.Info($"Part:{partToBump}");
                logger.Info($"Diagnostics:{diagnosticsMode}");
                logger.Info($"Debug:{debugMode}");
                logger.Info($"Patterns: ");
                using (logger.Block) {
                    foreach (var pattern in patterns)
                        logger.Info(pattern);
                }
            }
        }

        static void Separator(ILogger logger)
        {
            logger.Info("======================================================");
        }

        static VersionPart ParsePartToBump(IEnumerable<string> args) => TranslateToVersionPart(args.GetValueForParameter("part"));

        static ComponentsList Rescan(ILogger logger, IComponentsFactory[] factories)
        {
            var components = new ComponentsList();
            components.Clear();
            int scannedDirsCount = 0;
            string path = Path.GetFullPath(Directory.GetCurrentDirectory());
            logger.Info("Scanning '{0}'", path);
            components.Scan(logger, path, factories, s => { logger.Debug(s); scannedDirsCount++; });
            logger.Info("Scanned {0} directories", scannedDirsCount);
            logger.Info("Found {0} component{1}", components.Count, components.Count > 1 ? "s" : "");
            logger.Info("Found {0} solution{1}", components.Solutions.Count, components.Solutions.Count > 1 ? "s" : "");
            logger.Info("Sorting...");
            components.SortByName();
            logger.Info("Finding dependents...");
            components.FindDependents();
            logger.Info("Matching solutions to projects...");
            components.MatchSolutionsToProjects(logger);
            return components;
        }

        static VersionPart TranslateToVersionPart(string part)
        {
            switch (part) {
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
