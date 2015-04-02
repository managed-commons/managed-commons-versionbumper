﻿using System;

namespace Commons.VersionBumper.Interfaces
{
	public interface IComponentFinder
	{
		T FindComponent<T>(string componentNamePattern, Func<T, bool> filter = null, bool interactive = true) where T : class;
	}
}