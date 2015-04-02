using System.IO;
using System.Linq;

namespace Commons.VersionBumper.Components.CSharp
{
	public class CSharpWebsite : CSharpProject
	{
		public CSharpWebsite(string solutionPath, string webApplicationName, string webApplicationPath) :
			base(Path.Combine(Path.GetDirectoryName(solutionPath), webApplicationPath))
		{
			SolutionPath = solutionPath;
			Name = webApplicationName.Split('\\').Last(s => !string.IsNullOrWhiteSpace(s));
			_isWeb = true;
		}

		public string SolutionPath { get; private set; }

		public override string Type { get { return "C# Web Site"; } }

		protected override void ParseAvailableData()
		{
			if (!Directory.Exists(_projectDir))
				return;
			var app_code = Path.Combine(_projectDir, "App_Code");
			if (!Directory.Exists(app_code))
				app_code = _projectDir;
			ParseAssemblyInfo(Directory.EnumerateFiles(app_code, "*.cs", SearchOption.AllDirectories));
		}
	}
}