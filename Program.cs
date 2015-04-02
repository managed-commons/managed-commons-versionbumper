using System;
using System.Collections.Generic;
using System.Linq;
using Commons.VersionBumper.Commands;
using Commons.VersionBumper.Components.CSharp;
using Commons.VersionBumper.Utilities;

namespace Commons.VersionBumper
{
	public class Program
	{
		public static void Main(string[] args)
		{
			try {
				new BumpVersionExecutor().Process(new ConsoleLogger(false), args, new CSharpComponentsFactory());
			} catch {
			}
			Console.WriteLine("Done");
		}
	}
}