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
using Commons.VersionBumper.Components;
using Commons.VersionBumper.Interfaces;

namespace Commons.VersionBumper.Data
{
    public class ComponentsList : IComponentFinder, IEnumerable<IComponent>
    {
        public readonly List<ISolution> Solutions = new List<ISolution>();

        public int Count => _list.Count;

        public void Clear()
        {
            _list.Clear();
            Solutions.Clear();
        }

        public bool Contains(IComponent component) => _list.Any(component.Equals);

        public IEnumerable<IComponent> FilterBy(string pattern, bool orderByTreeDepth = false, bool groupByType = false, bool orphans = false)
        {
            var list = _list.FindAll(c => c.MatchName(pattern) && (!orphans || isOrphan(c)));
            if (groupByType) {
                if (orderByTreeDepth)
                    list.Sort((c1, c2) => {
                        var typeCompare = string.Compare(c1.Type, c2.Type, StringComparison.InvariantCultureIgnoreCase);
                        return typeCompare != 0 ? typeCompare : (c2.DependentComponents.Count() - c1.DependentComponents.Count());
                    });
                else
                    list.Sort((c1, c2) => {
                        var typeCompare = string.Compare(c1.Type, c2.Type, StringComparison.InvariantCultureIgnoreCase);
                        return typeCompare != 0 ? typeCompare : (string.Compare(c1.Name, c2.Name, StringComparison.InvariantCultureIgnoreCase));
                    });
            } else
                if (orderByTreeDepth)
                list.Sort((c1, c2) => (c2.DependentComponents.Count() - c1.DependentComponents.Count()));
            return list;
        }

        public T FindComponent<T>(ILogger logger, string componentNamePattern, Func<T, bool> filter = null, bool interactive = true) where T : class
        {
            try {
                var list = _list.FindAll(c => c is T && c.MatchName(componentNamePattern));
                if (filter != null)
                    list = list.FindAll(c => filter(c as T));
                if (list.Count == 1)
                    return (T)list[0];
                if (interactive) {
                    if (list.Count > 20)
                        Console.WriteLine("Too many components match the pattern '{0}': {1}. Try another pattern!", componentNamePattern, list.Count);
                    else if (list.Count == 0)
                        Console.WriteLine("No components match the pattern '{0}'. Try another pattern!", componentNamePattern);
                    else {
                        do {
                            var i = 0;
                            Console.WriteLine("Select from this list:");
                            foreach (var component in list)
                                Console.WriteLine("[{0:0000}] {1}", ++i, component);
                            Console.Write("Type 1-{0}, 0 to abandon: ", list.Count);
                            var input = Console.ReadLine();
                            if (int.TryParse(input, out i)) {
                                if (i == 0)
                                    break;
                                if (i > 0 && i <= list.Count)
                                    return (T)list[i - 1];
                            }
                        } while (true);
                    }
                }
            } catch (Exception e) {
                logger.Error(e);
            }
            return (T)null;
        }

        public void FindDependents()
        {
            foreach (IComponent component in _list) {
                var preLista = _list.FindAll(c => c.Dependencies.Any(r => r.Equals(component)));
                var initialCount = 0;
                do {
                    initialCount = preLista.Count;
                    var delta = new List<IComponent>();
                    foreach (IComponent dependentComponent in preLista)
                        delta.AddRange(_list.FindAll(c => c.Dependencies.Any(r => r.Equals(dependentComponent))));
                    preLista.AddRange(delta.Distinct());
                    preLista = new List<IComponent>(preLista.Distinct());
                } while (initialCount < preLista.Count);
                component.DependentComponents = new LayeredDependencies(preLista);
            }
        }

        public IProject FindMatchingComponent(ILogger logger, IFile project) => FindComponent<IProject>(logger, "^" + project.Name + "$", p => p.FullPath == project.FullPath, false);

        public IEnumerator<IComponent> GetEnumerator() => _list.GetEnumerator();

        public void MatchSolutionsToProjects(ILogger logger)
        {
            Solutions.Sort((s1, s2) => string.Compare(s1.Name, s2.Name, StringComparison.InvariantCultureIgnoreCase));
            foreach (var solution in Solutions)
                foreach (var project in solution.Projects) {
                    var component = FindMatchingComponent(logger, project);
                    if (component != null)
                        component.AddParent(solution);
                    else
                        solution.AddMissingProject(project, FindSimilarProjects(project));
                }
        }

        public void Prune(string path)
        {
            _list = new List<IComponent>(_list.FindAll(c => !c.FullPath.StartsWith(path, StringComparison.InvariantCultureIgnoreCase)));
            SortByName();
            FindDependents();
        }

        public void Scan(ILogger logger, string path, IEnumerable<IComponentsFactory> factories, Action<string> scanned)
        {
            try {
                foreach (IComponentsFactory factory in factories)
                    _list.AddRange(factory.FindComponentsIn(logger, path));
                foreach (var dir in Directory.EnumerateDirectories(path))
                    Scan(logger, dir, factories, scanned);
                foreach (var solutionFullPath in Directory.EnumerateFiles(path, "*.sln"))
                    Solutions.Add(new Solution(solutionFullPath));
                scanned(path);
            } catch (Exception e) {
                Console.Error.WriteLine(e);
            }
        }

        public void SortByName()
        {
            _list.Sort((c1, c2) => string.Compare(c1.Name, c2.Name, StringComparison.InvariantCultureIgnoreCase));
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _list.GetEnumerator();

        List<IComponent> _list = new List<IComponent>();

        IEnumerable<IProject> FindSimilarProjects(IFile project) => _list.As<IProject>().Where(c => c.MatchName("^" + project.Name + "$"));

        bool isOrphan(IComponent c)
        {
            var p = c as IProject;
            return (p != null) && (p.Parents.Count() == 0);
        }

        class LayeredDependencies : IEnumerable<IComponent>
        {
            public LayeredDependencies(IEnumerable<IComponent> initialList)
            {
                lists = new List<List<IComponent>>();
                Divide(new List<IComponent>(initialList));
            }

            public IEnumerator<IComponent> GetEnumerator()
            {
                foreach (var list in lists)
                    foreach (var component in list)
                        yield return component;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            readonly List<List<IComponent>> lists;

            void Divide(List<IComponent> initialList)
            {
                if (initialList.Count == 0)
                    return;
                var itemsHere = new List<IComponent>();
                var itemsAbove = new List<IComponent>();
                foreach (var component in initialList)
                    if (initialList.Any(c => c.Dependencies.Any(r => r.Equals(component))))
                        itemsHere.Add(component);
                    else
                        itemsAbove.Add(component);
                lists.Insert(0, itemsAbove);
                Divide(itemsHere);
            }
        }
    }
}
