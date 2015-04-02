using System;
using System.Collections.Generic;
using System.Linq;

namespace Commons.VersionBumper.Interfaces
{
	public interface IReference : IEquatable<IReference>
	{
		string Name { get; }

		string Platform { get; }

		string Version { get; }
	}
}