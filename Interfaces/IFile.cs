using System;

namespace Commons.VersionBumper.Interfaces
{
	public interface IFile
	{
		string FullPath { get; }

		string Name { get; }
	}
}