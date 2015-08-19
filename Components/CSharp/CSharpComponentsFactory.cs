﻿// Commons.VersionBumper
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
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Commons.VersionBumper.Interfaces;

namespace Commons.VersionBumper.Components.CSharp
{
    public class CSharpComponentsFactory : IComponentsFactory
    {
        public IEnumerable<IComponent> FindComponentsIn(ILogger logger, string folderPath)
        {
            foreach (var project in Directory.EnumerateFiles(folderPath, "*.csproj")) {
                yield return new CSharpProject(logger, project);
            }
            foreach (var solution in Directory.EnumerateFiles(folderPath, "*.sln")) {
                string solutionContents = File.ReadAllText(solution);
                string webApplicationPattern = "Project\\(\"\\{E24C65DC-7377-472B-9ABA-BC803B73C61A\\}\"\\)\\s*\\=\\s*\"([^\"]+)\"\\s*\\,\\s*\"([^\"]+)\"";
                var match = Regex.Match(solutionContents, webApplicationPattern, RegexOptions.Multiline);
                while (match.Success) {
                    var webApplicationPath = match.Groups[2].Value;
                    if (Directory.Exists(Path.Combine(Path.GetDirectoryName(solution), webApplicationPath)))
                        yield return new CSharpWebsite(logger, solution, match.Groups[1].Value, webApplicationPath);
                    match = match.NextMatch();
                }
            }
        }
    }
}
