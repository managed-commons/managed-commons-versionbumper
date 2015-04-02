using System;
using Commons.VersionBumper.Interfaces;

namespace Commons.VersionBumper.Components
{
	public abstract class BasicReference : IReference
	{
		public string Name { get; protected set; }

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