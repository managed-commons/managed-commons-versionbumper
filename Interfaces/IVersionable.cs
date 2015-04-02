using System;

namespace Commons.VersionBumper.Interfaces
{
	public interface IVersionable : IComponent
	{
		VersionPart PartToCascadeBump(VersionPart partBumpedOnDependency);

		bool SetNewVersion(ILogger log, Version version);
	}
}