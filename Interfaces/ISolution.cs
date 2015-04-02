using System;
using System.Collections.Generic;

namespace Commons.VersionBumper.Interfaces
{
	public interface ISolution : IEquatable<ISolution>
	{
		string FullPath { get; }

		string Name { get; }

		IEnumerable<IFile> Projects { get; }

		bool MatchName(string pattern);
	}
}