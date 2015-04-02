using System;
using Commons.VersionBumper.Interfaces;

namespace Commons.VersionBumper.Components
{
	public abstract class BasicReference : IReference
	{
		public string Name { get; protected set; }

		public string Platform
		{
			get { return "net40"; }	// FIXME need to have access to either the referenced project target platform or the consumer project's.
		}

		public abstract string Version { get; }

		public bool Equals(IReference other)
		{
			return IsEqual(other);
		}

		public override bool Equals(object obj)
		{
			return IsEqual(obj as IReference);
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		private bool IsEqual(IReference other)
		{
			return other != null && Name == other.Name;
		}
	}
}