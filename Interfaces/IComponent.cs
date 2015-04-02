using System;
using System.Collections.Generic;

namespace NugetCracker.Interfaces
{
	public interface IComponent : IReference
	{
		string Description { get; }
		Version CurrentVersion { get; }
		string FullPath { get; }
		string Type { get; }

		bool MatchName(string pattern);
		string ToLongString();

		IEnumerable<IReference> Dependencies { get; }
		IEnumerable<IComponent> DependentComponents { get; set; }
		IEnumerable<IProject> DependentProjects { get; }
	}
}
