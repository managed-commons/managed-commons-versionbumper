// Commons.VersionBumper
//
// Copyright (c) 2012-2015 Rafael 'Monoman' Teixeira, Managed Commons Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace Commons.VersionBumper
{
    public enum VersionPart
    {
        None,
        Major,
        Minor,
        Patch
    }

    public static class Extensions
    {
        public static IEnumerable<TResult> As<TResult>(this IEnumerable list)
        {
            if (list == null)
                yield break;
            foreach (var item in list)
                if (item is TResult)
                    yield return (TResult)item;
        }

        public static string AsSimpleName(this string originalName) => originalName.Split('\\').Last(s => !string.IsNullOrWhiteSpace(s));

        public static SemanticVersion Bump(this SemanticVersion oldVersion, VersionPart partToBump)
        {
            switch (partToBump) {
                case VersionPart.Major:
                    return new SemanticVersion(oldVersion.Major + 1, 0, 0);

                case VersionPart.Minor:
                    return new SemanticVersion(oldVersion.Major, oldVersion.Minor + 1, 0);

                case VersionPart.Patch:
                    return new SemanticVersion(oldVersion.Major, oldVersion.Minor, oldVersion.Patch + 1);
            }
            return new SemanticVersion(oldVersion.Major, oldVersion.Minor, oldVersion.Patch);
        }

        public static string Combine(this string path, string relativePath)
        {
            if (Path.DirectorySeparatorChar != '\\' && relativePath.Contains('\\'))
                relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(path, relativePath);
        }

        public static string FormatWith(this string format, params string[] args) => string.Format(format, args);

        public static string NormalizeVersion(this string version)
        {
            if (version.Contains('*'))
                version = version.Replace('*', '0');
            var parts = version.Split('-');
            version = parts[0].Trim();
            var periods = version.Count(c => c == '.');
            if (periods > 2) {
                version = version.Substring(0, version.LastIndexOf('.'));
            }
            if (periods < 2)
                version += ".0";
            return version;
        }

         public static string RegexReplace(this string text, string pattern, string replace, string altPattern = null, string altReplace = null)
        {
            if (Regex.IsMatch(text, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
                return Regex.Replace(text, pattern, replace, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            else if (!string.IsNullOrWhiteSpace(altPattern))
                return Regex.Replace(text, altPattern, altReplace, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return text;
        }

        public static string RelativeTo(this string path, string basedir)
        {
            var fullPath = Path.GetFullPath(path);
            if (path.StartsWith(basedir, StringComparison.InvariantCultureIgnoreCase))
                return path.Substring(basedir.Length + 1);
            var parentBaseDir = Path.GetDirectoryName(basedir);
            if (parentBaseDir.Length < 4 && parentBaseDir[1] == ':')
                return path;
            return Combine("..", path.RelativeTo(parentBaseDir));
        }

        public static void SetVersion(this string versionFile, SemanticVersion version)
        {
            string pattern = "(Assembly(File|))(Version\\(\")([^\"]*)(\"\\s*\\))";
            string replace = "$1Version(\"" + version.ShortVersion() + "$5";
            versionFile.TransformFile(xml => xml.RegexReplace(pattern, replace));
            pattern = "(AssemblyInformational)(Version\\(\")([^\"]*)(\"\\s*\\))";
            replace = "$1Version(\"" + version.ToNormalizedString() + "$4";
            versionFile.TransformFile(xml => xml.RegexReplace(pattern, replace));
        }

        public static string ShortVersion(this SemanticVersion version) => version.ToNormalizedString().Split('-')[0];

        public static void TransformFile(this string filename, Func<string, string> transformer)
        {
            TurnOffReadOnlyAttribute(filename);
            File.WriteAllText(filename, transformer(File.ReadAllText(filename)));
        }

        static void TurnOffReadOnlyAttribute(string filename)
        {
            var attribs = File.GetAttributes(filename);
            if (attribs.HasFlag(FileAttributes.ReadOnly)) {
                attribs ^= FileAttributes.ReadOnly;
                File.SetAttributes(filename, attribs);
            }
        }
    }
}
