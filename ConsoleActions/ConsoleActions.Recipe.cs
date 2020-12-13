using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Bam.Net.Application;
using Bam.Net.Automation;
using Bam.Net.CommandLine;
using Bam.Net.Testing;

namespace Bam.Net.Bake
{
    public partial class ConsoleActions
    {
        [ConsoleAction("recipe", "bake the specified recipe")]
        public void BakeRecipe()
        {
            if (Arguments.Contains("version")) // don't bake the recipe if all we're doing is updating the version
            {
                return;
            }
            // build each csproj with dotnet publish
            string startDir = Environment.CurrentDirectory;
            Recipe recipe = GetRecipe();
            if (Arguments.Contains("output"))
            {
                recipe.OutputDirectory = GetArgument("output");
            }
            BamSettings settings = BamSettings.Load();
            string outputDirectory = GetOutputDirectory(recipe);
            string buildConfigString = recipe.BuildConfig.ToString();
            if (Arguments.Contains("buildConfig"))
            {
                buildConfigString = Arguments["buildConfig"];
                Message.PrintLine("Recipe BuildConfig = {0}, Specified BuildConfig = {1}", ConsoleColor.DarkYellow, recipe.BuildConfig.ToString(), buildConfigString);
            }

            if (!BuildConfig.TryParse(buildConfigString, out BuildConfig buildConfig))
            {
                Message.PrintLine("Unable to parse specified buildConfig (should be either Debug or Release: {0}", ConsoleColor.Magenta, buildConfigString);
                Exit(1);
            }
            Message.PrintLine("dotnet info:", ConsoleColor.Cyan);
            Message.PrintLine("path: {0}", ConsoleColor.Cyan, settings.DotNetPath);
            Message.PrintLine("version:", ConsoleColor.Cyan);
            settings.DotNetPath.ToStartInfo("--version").Run(msg => Message.PrintLine(msg, ConsoleColor.Cyan));
            foreach (string projectFile in recipe.ProjectFilePaths)
            {
                string projectFilePath = projectFile.Replace("\\", "/");
                string projectName = Path.GetFileNameWithoutExtension(projectFile);
                DirectoryInfo projectDirectory = new FileInfo(projectFilePath).Directory;
                Environment.CurrentDirectory = projectDirectory.FullName;
                DirectoryInfo projectOutputDirectory = new DirectoryInfo(Path.Combine(outputDirectory, projectName));
                if (!projectOutputDirectory.Exists)
                {
                    projectOutputDirectory.Create();
                }
                string outputDirectoryPath = projectOutputDirectory.FullName.Replace("\\", "/");
                Message.PrintLine(outputDirectoryPath);
                string dotNetArgs = $"publish {projectFilePath} -c {buildConfig.ToString()} -o {outputDirectoryPath}";
                Message.PrintLine("{0} {1}", ConsoleColor.Blue, settings.DotNetPath, dotNetArgs);
                ProcessStartInfo startInfo = settings.DotNetPath.ToStartInfo(dotNetArgs);
                startInfo.Run(msg => Message.PrintLine(msg, ConsoleColor.DarkYellow), msg => Message.PrintLine(msg, ConsoleColor.Magenta));
                Message.PrintLine("publish command finished for project {0}, output directory = {1}", ConsoleColor.Blue, projectFilePath, outputDirectoryPath);
            }

            Environment.CurrentDirectory = startDir;
        }
    }
}