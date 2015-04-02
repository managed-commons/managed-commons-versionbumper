using System.Collections.Generic;

namespace Commons.VersionBumper.Interfaces
{
	public interface IComponentsFactory
	{
		IEnumerable<IComponent> FindComponentsIn(string folderPath);
	}
}