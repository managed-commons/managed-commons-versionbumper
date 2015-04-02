using System.Collections.Generic;

namespace Commons.VersionBumper.Interfaces
{
	public interface IProject : IVersionable
	{
		IEnumerable<ISolution> Parents { get; }

		void AddParent(ISolution solution);

		void RemoveParent(ISolution solution);
	}
}