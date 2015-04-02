using System;
using System.Collections.Generic;

namespace Commons.VersionBumper.Interfaces
{
	public interface IComponent : IReference
	{
		Version CurrentVersion { get; }

		IEnumerable<IReference> Dependencies { get; }

		IEnumerable<IComponent> DependentComponents { get; set; }

		IEnumerable<IProject> DependentProjects { get; }

		string Description { get; }

		string FullPath { get; }

		string Type { get; }

		bool MatchName(string pattern);

		string ToLongString();
	}
}