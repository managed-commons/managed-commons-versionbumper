using System;
using System.IO;
using Commons.VersionBumper.Interfaces;

namespace Commons.VersionBumper.Components
{
	public class ProjectReference : BasicReference
	{
		public ProjectReference(string fullPath)
		{
			Name = Path.GetFileNameWithoutExtension(fullPath);
			FullPath = fullPath;
		}

		public string FullPath { get; private set; }

		public bool Equals(IProject other)
		{
			return IsEqual(other);
		}

		public override bool Equals(object obj)
		{
			return IsEqual(obj as IProject);
		}

		public override int GetHashCode()
		{
			return FullPath.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("Project Reference: {0}", FullPath);
		}

		private bool IsEqual(IProject other)
		{
			return other != null && FullPath.Equals(other.FullPath, StringComparison.OrdinalIgnoreCase);
		}
	}
}