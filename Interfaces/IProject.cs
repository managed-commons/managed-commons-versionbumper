
using System.Collections.Generic;
using NugetCracker.Data;
namespace NugetCracker.Interfaces
{
	public interface IProject : IVersionable
	{
		IEnumerable<ISolution> Parents { get; }

		void AddParent(ISolution solution);
		void RemoveParent(ISolution solution);
	}
}
