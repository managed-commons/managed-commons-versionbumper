using System;
using System.Collections.Generic;

namespace NugetCracker.Interfaces
{
	public interface ISolution : IEquatable<ISolution>
	{
		string FullPath { get; }
		bool MatchName(string pattern);
		string Name { get; }
		IEnumerable<IFile> Projects { get; }
	}
}
