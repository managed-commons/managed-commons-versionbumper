using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NugetCracker
{
	public enum VersionPart
	{
		None,
		Major,
		Minor,
		Build,
		Revision
	}

	public static class Extensions
	{
		static char[] PATH_SEPARATOR = new[] { Path.PathSeparator };

		public static Version Bump(this Version oldVersion, VersionPart partToBump)
		{
			switch (partToBump) {
				case VersionPart.Major:
					return new Version(oldVersion.Major + 1, 0, 0, 0);

				case VersionPart.Minor:
					return new Version(oldVersion.Major, oldVersion.Minor + 1, 0, 0);

				case VersionPart.Build:
					return new Version(oldVersion.Major, oldVersion.Minor, oldVersion.Build + 1, 0);

				case VersionPart.Revision:
					return new Version(oldVersion.Major, oldVersion.Minor, oldVersion.Build, oldVersion.Revision + 1);
			}
			return oldVersion;
		}

		public static string Combine(this string path, string relativePath)
		{
			if (Path.DirectorySeparatorChar != '\\' && relativePath.Contains('\\'))
				relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
			return Path.Combine(path, relativePath);
		}

		public static string FormatWith(this string format, params string[] args)
		{
			return string.Format(format, args);
		}

		public static string RegexReplace(this string text, string pattern, string replace, string altPattern = null, string altReplace = null)
		{
			if (Regex.IsMatch(text, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
				return Regex.Replace(text, pattern, replace, RegexOptions.Multiline | RegexOptions.IgnoreCase);
			else if (!string.IsNullOrWhiteSpace(altPattern))
				return Regex.Replace(text, altPattern, altReplace, RegexOptions.Multiline | RegexOptions.IgnoreCase);
			return text;
		}

		public static void TransformFile(this string filename, Func<string, string> transformer)
		{
			TurnOffReadOnlyAttribute(filename);
			File.WriteAllText(filename, transformer(File.ReadAllText(filename)));
		}

		private static void TurnOffReadOnlyAttribute(string filename)
		{
			var attribs = File.GetAttributes(filename);
			if (attribs.HasFlag(FileAttributes.ReadOnly)) {
				attribs ^= FileAttributes.ReadOnly;
				File.SetAttributes(filename, attribs);
			}
		}

		public static string ParseStringParameter(this IEnumerable<string> args, string paramName, string @default = null)
		{
			paramName = "-" + paramName.Trim().ToLowerInvariant() + ":";
			var arg = args.FirstOrDefault(s => s.ToLowerInvariant().StartsWith(paramName));
			if (arg == null || arg.Length <= paramName.Length)
				return @default;
			return arg.Substring(paramName.Length);
		}

		public static string ToLibFolder(this string framework)
		{
			switch (framework) {
				case "v2.0":
					return "net20";

				case "v3.0":
					return "net30";

				case "v3.5":
					return "net35";

				case "v4.5":
					return "net45";

				default:
					return "net40";
			}
		}

		public static void SetVersion(this string versionFile, Version version)
		{
			string pattern = "(Assembly(Informational|File|))(Version\\(\")([^\"]*)(\"\\s*\\))";
			string replace = "$1Version(\"" + version + "$5";
			versionFile.TransformFile(xml => xml.RegexReplace(pattern, replace));
		}
	}
}