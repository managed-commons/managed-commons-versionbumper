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

using System.IO;
using System.Linq;
using Commons.VersionBumper.Interfaces;

namespace Commons.VersionBumper.Components.CSharp
{
    public class CSharpWebsite : CSharpProject
    {
        public CSharpWebsite(ILogger logger, string solutionPath, string webApplicationName, string webApplicationPath) :
            base(logger, Path.Combine(Path.GetDirectoryName(solutionPath), webApplicationPath))
        {
            SolutionPath = solutionPath;
            Name = webApplicationName.AsSimpleName();
            _isWeb = true;
        }

        public string SolutionPath { get; }

        public override string Type => "C# Web Site";

        protected override void ParseAvailableData(ILogger logger)
        {
            if (!Directory.Exists(_projectDir))
                return;
            var app_code = Path.Combine(_projectDir, "App_Code");
            if (!Directory.Exists(app_code))
                app_code = _projectDir;
            ParseAssemblyInfo(logger, Directory.EnumerateFiles(app_code, "*.cs", SearchOption.AllDirectories));
        }
    }
}
