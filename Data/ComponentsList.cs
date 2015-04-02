﻿using System;
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

		public int Count { get { return _list.Count; } }

		public void Clear()
		{
			_list.Clear();
			Solutions.Clear();
		}

		public bool Contains(IComponent component)
		{
			return _list.Any(c => component.Equals(c));
		}

		public IEnumerable<IComponent> FilterBy(string pattern, bool orderByTreeDepth = false, bool groupByType = false, bool orphans = false)
		{
			var list = _list.FindAll(c => c.MatchName(pattern) && (!orphans || isOrphan(c)));
			if (groupByType) {
				if (orderByTreeDepth)
					list.Sort((c1, c2) =>
					{
						var typeCompare = c1.Type.CompareTo(c2.Type);
						return typeCompare != 0 ? typeCompare : (c2.DependentComponents.Count() - c1.DependentComponents.Count());
					});
				else
					list.Sort((c1, c2) =>
					{
						var typeCompare = c1.Type.CompareTo(c2.Type);
						return typeCompare != 0 ? typeCompare : (c1.Name.CompareTo(c2.Name));
					});
			} else
				if (orderByTreeDepth)
					list.Sort((c1, c2) => (c2.DependentComponents.Count() - c1.DependentComponents.Count()));
			return list;
		}

		public T FindComponent<T>(string componentNamePattern, Func<T, bool> filter = null, bool interactive = true) where T : class
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
			} catch {
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

		public IProject FindMatchingComponent(IFile project)
		{
			return FindComponent<IProject>("^" + project.Name + "$", p => p.FullPath == project.FullPath, false);
		}

		public IEnumerator<IComponent> GetEnumerator()
		{
			return _list.GetEnumerator();
		}

		public void MatchSolutionsToProjects()
		{
			Solutions.Sort((s1, s2) => s1.Name.CompareTo(s2.Name));
			foreach (var solution in Solutions)
				foreach (var project in solution.Projects) {
					var component = FindMatchingComponent(project);
					if (component != null)
						component.AddParent(solution);
					else
						Console.WriteLine("Could not find project '{0}' for solution '{1}'", project.Name, solution.Name);
				}
		}

		public void Prune(string path)
		{
			_list = new List<IComponent>(_list.FindAll(c => !c.FullPath.StartsWith(path)));
			SortByName();
			FindDependents();
		}

		public void Scan(string path, IEnumerable<IComponentsFactory> factories, Action<string> scanned)
		{
			try {
				foreach (IComponentsFactory factory in factories)
					_list.AddRange(factory.FindComponentsIn(path));
				foreach (var dir in Directory.EnumerateDirectories(path))
					Scan(dir, factories, scanned);
				foreach (var solutionFullPath in Directory.EnumerateFiles(path, "*.sln"))
					Solutions.Add(new Solution(solutionFullPath));
				scanned(path);
			} catch (Exception e) {
				Console.Error.WriteLine(e);
			}
		}

		public void SortByName()
		{
			_list.Sort((c1, c2) => c1.Name.CompareTo(c2.Name));
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return _list.GetEnumerator();
		}

		private class LayeredDependencies : IEnumerable<IComponent>
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

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			private List<List<IComponent>> lists;

			private void Divide(List<IComponent> initialList)
			{
				if (initialList.Count == 0)
					return;
				List<IComponent> itemsHere = new List<IComponent>();
				List<IComponent> itemsAbove = new List<IComponent>();
				foreach (var component in initialList)
					if (initialList.Any(c => c.Dependencies.Any(r => r.Equals(component))))
						itemsHere.Add(component);
					else
						itemsAbove.Add(component);
				lists.Insert(0, itemsAbove);
				Divide(itemsHere);
			}
		}

		private List<IComponent> _list = new List<IComponent>();

		private bool isOrphan(IComponent c)
		{
			var p = c as IProject;
			return (p != null) && (p.Parents.Count() == 0);
		}
	}
}