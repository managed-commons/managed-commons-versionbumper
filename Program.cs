using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NugetCracker.Commands;
using NugetCracker.Components.CSharp;
using NugetCracker.Data;
using NugetCracker.Interfaces;
using NugetCracker.Utilities;

namespace NugetCracker
{
	public class Program
	{
		public static void Main(string[] args)
		{
			new BumpVersionExecutor().Process(new ConsoleLogger(false), args, new CSharpComponentsFactory());
		}
	}
}