using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Bam.Net.Application;
using Bam.Net.Automation;
using Bam.Net.Automation.SourceControl;
using Bam.Net.CommandLine;
using Bam.Net.Encryption;
using Bam.Net.Logging;
using Bam.Net.Testing;

namespace Bam.Net.Bake
{
    public partial class ConsoleActions
    {
        [ConsoleAction("version", "Update the package version of each project referenced by a recipe; also (over)writes the SemanticAssemblyInfo.cs file for all projects in the recipe.")]
        public void Version()
        {
            string prompt = "Please specify 'major', 'minor' or 'patch' to increment version component.";
            string versionArg = GetArgument("version", true, prompt);
            SemanticVersion currentVersion = GetCurrentVersion(versionArg);
            SemanticVersion nextVersion = GetNextVersion(versionArg);
            SetLifecycle(nextVersion);
            bool reset = Arguments.Contains("reset");

            string recipePath = Arguments["versionRecipe"];
            if (string.IsNullOrEmpty(recipePath))
            {
                Message.PrintLine("Please specify /versionRecipe:<path_to_recipe_to_update_version>");
                Exit(1);
            }

            Recipe recipe = recipePath.FromJsonFile<Recipe>();

            foreach (string projectFile in recipe.ProjectFilePaths)
            {
                FileInfo projectFileInfo = new FileInfo(projectFile);
                FileSystemSemanticVersion currentProjectVersion = FileSystemSemanticVersion.Find(projectFileInfo.Directory);
                SemanticVersion nextProjectVersion = GetNextVersionFrom(currentProjectVersion);
                nextProjectVersion = SetBuild(nextProjectVersion, projectFile);
                SetLifecycle(nextProjectVersion);
                SemanticVersion versionToUse = reset ? currentVersion : nextVersion >= nextProjectVersion ? nextVersion : nextProjectVersion;
                
                versionToUse = SetBuild(versionToUse, projectFileInfo.FullName);
                SetLifecycle(versionToUse);
                
                Message.PrintLine("Project: {0}", ConsoleColor.Cyan, projectFileInfo.FullName);
                Message.PrintLine("Current version in semver directory {0}: {1}", currentProjectVersion.SemverDirectory, currentProjectVersion.ToString());
                Message.PrintLine("Next project version: {0}", nextProjectVersion.ToString());
                Message.PrintLine("Using version: {0}", versionToUse.ToString());

                WriteNuspecFile(projectFileInfo, versionToUse);
                EnsureNugetElements(projectFileInfo);
                string semanticAssemblyInfoPath = WriteProjectSemanticAssemblyInfo(projectFileInfo, versionToUse);

                Message.PrintLine("Wrote file {0}", ConsoleColor.Yellow, semanticAssemblyInfoPath);
                Message.PrintLine(semanticAssemblyInfoPath.SafeReadFile(), ConsoleColor.Cyan);
                Message.PrintLine();
            }
        }

        private static void EnsureNugetElements(FileInfo projectFileInfo)
        {
            // update this to only apply to _tools 
            string projectFile = projectFileInfo.FullName;
            if ((bool)!projectFileInfo.Directory?.Parent?.Name.Equals("_tools"))
            {
                return;
            }
            
            string projectName = Path.GetFileNameWithoutExtension(projectFile);
            XDocument projectXDocument = XDocument.Load(projectFile);
            XElement propertyGroupElement = projectXDocument.Element("Project").Element("PropertyGroup");
            XElement noPackageAnalysis = propertyGroupElement.Element("NoPackageAnalysis");
            if (noPackageAnalysis == null)
            {
                propertyGroupElement.Add(NuspecFile.NoPackageAnalysisElement);
            }

            XElement nuspecFile = propertyGroupElement.Element("NuspecFile");
            if (nuspecFile == null)
            {
                propertyGroupElement.Add(NuspecFile.GetNuspecFileElement(projectFile));
            }

            XElement intermediatePackDir = propertyGroupElement.Element("IntermediatePackDir");
            if (intermediatePackDir == null)
            {
                propertyGroupElement.Add(NuspecFile.IntermediatePackDirElement);
            }

            XElement publishDir = propertyGroupElement.Element("PublishDir");
            if (publishDir == null)
            {
                propertyGroupElement.Add(NuspecFile.PublishDirElement);
            }

            XElement nuspecProperties = propertyGroupElement.Element("NuspecProperties");
            if (nuspecProperties == null)
            {
                propertyGroupElement.Add(NuspecFile.NuspecPropertiesElement);
            }

            XElement publishAllTarget = projectXDocument.Elements("Target").FirstOrDefault(xe => xe.Attribute("Name").Value == "PublishAll");
            if (publishAllTarget == null)
            {
                projectXDocument.Add(NuspecFile.PublishAllTargetElement);
            }
            
            XmlWriterSettings settings = new XmlWriterSettings {Indent = true, OmitXmlDeclaration = true};
            using (XmlWriter xw = XmlWriter.Create(projectFile, settings))
            {
                projectXDocument.Save(xw);
            }
        }
        
        private static string WriteProjectSemanticAssemblyInfo(FileInfo projectFileInfo, SemanticVersion versionToUse)
        {
            SetProjectVersion(projectFileInfo.FullName, versionToUse);

            string semanticAssemblyInfo = AssemblySemanticVersion.WriteProjectSemanticAssemblyInfo(projectFileInfo.FullName, versionToUse);
            return semanticAssemblyInfo;
        }

        private static void WriteNuspecFile(FileInfo projectFileInfo, SemanticVersion versionToUse)
        {
            string nuspecFile = Path.Combine(projectFileInfo.Directory.FullName, $"{Path.GetFileNameWithoutExtension(projectFileInfo.Name)}.nuspec");
            if (!File.Exists(nuspecFile))
            {
                NuspecFile nuspec = new NuspecFile(nuspecFile)
                {
                    Version = versionToUse.ToString(),
                    Authors = ReadPropertyGroupElement(projectFileInfo, "Authors"),
                    Description = ReadPropertyGroupElement(projectFileInfo, "Description")
                };
                nuspec.Write();
            }
            if (File.Exists(nuspecFile))
            {
                SetNuspecVersion(nuspecFile, versionToUse);
            }
        }

        private static string ReadPropertyGroupElement(FileInfo projectFileInfo, string propertyGroupElement)
        {
            XDocument xdoc = XDocument.Load(projectFileInfo.FullName);
            XElement propertyGroup = xdoc.Element("Project").Element("PropertyGroup");
            XElement targetElement = propertyGroup.Element(propertyGroupElement);
            if (targetElement != null)
            {
                return targetElement.Value;
            }

            return string.Empty;
        } 
        
        private static void SetProjectVersion(string projectFile, SemanticVersion versionToUse)
        {
            XDocument xdoc = XDocument.Load(projectFile);
            XElement versionElement = xdoc.Element("Project").Element("PropertyGroup").Element("Version");

            if (versionElement != null)
            {
                string version = versionToUse.ToString();
                Message.PrintLine("Setting project version for {0} to {1}", projectFile, version);
                versionElement.Value = version;
                XmlWriterSettings settings = new XmlWriterSettings {Indent = true, OmitXmlDeclaration = true};
                using (XmlWriter xw = XmlWriter.Create(projectFile, settings))
                {
                    xdoc.Save(xw);
                }
            }
            else
            {
                Message.PrintLine("Version element not found in project file: {0}", ConsoleColor.Yellow, projectFile);
            }
        }

        private static void SetNuspecVersion(string nuspecFile, SemanticVersion versionToUse)
        {
            XDocument xdoc = XDocument.Load(nuspecFile);
            XElement versionElement = xdoc.Element("package").Element("metadata").Element("version");

            if (versionElement != null)
            {
                string version = versionToUse.ToString();
                Message.PrintLine("Setting nuspec version for {0} to {1}", nuspecFile, version);
                versionElement.Value = version;
                XmlWriterSettings settings = new XmlWriterSettings {Indent = true, OmitXmlDeclaration = true};
                using (XmlWriter xw = XmlWriter.Create(nuspecFile, settings))
                {
                    xdoc.Save(xw);
                }
            }
            else
            {
                Message.PrintLine("Version element not found nuspec file: {0}", ConsoleColor.DarkYellow, nuspecFile);
            }
        }

        private static string GetAuthors(string projectFile)
        {
            XDocument xdoc = XDocument.Load(projectFile);
            XElement authorsElement = xdoc.Element("Project").Element("PropertyGroup").Element("Authors");
            if (authorsElement != null)
            {
                return authorsElement.Value;
            }

            Message.PrintLine("Authors element not found in project file: {0}", ConsoleColor.DarkYellow, projectFile);
            return string.Empty;
        }
        
        private SemanticVersion GetCurrentVersion(string versionArg, string semverDirectory = ".")
        {
            if (SemanticVersion.TryParse(versionArg, out SemanticVersion parsedVersion))
            {
                return parsedVersion;
            }
            
            SemanticVersion result = new SemanticVersion();
            if (FileSystemSemanticVersion.TryFind(semverDirectory, out FileSystemSemanticVersion version))
            {
                result = version;
            }

            return result;
        }

        private SemanticVersion GetNextVersion(string versionArg, string semverDirectory = ".")
        {
            SemanticVersion currentVersion = GetCurrentVersion(versionArg, semverDirectory);
            if (versionArg.TryToEnum<VersionSpec>(out VersionSpec versionSpec))
            {
                return currentVersion.Increment(versionSpec);
            }

            return GetNextVersionFrom(currentVersion);
        }
        
        private SemanticVersion GetNextVersionFrom(SemanticVersion currentVersion)
        {
            SemanticVersion newVersion = currentVersion.CopyAs<SemanticVersion>();
           
            if (Arguments.Contains("major"))
            {
                newVersion.Increment(VersionSpec.Major);
            }

            if (Arguments.Contains("minor"))
            {
                newVersion.Increment(VersionSpec.Minor);
            }

            if (Arguments.Contains("patch"))
            {
                newVersion.Increment(VersionSpec.Patch);
            }

            if (newVersion.Equals(currentVersion))
            {
                newVersion.Increment(VersionSpec.Patch);
            }
            
            return newVersion;
        }

        private static void SetLifecycle(SemanticVersion version)
        {
            if (Arguments.Contains("dev"))
            {
                version.Lifecycle = SemanticLifecycle.Dev;
            }

            if (Arguments.Contains("test"))
            {
                version.Lifecycle = SemanticLifecycle.Test;
            }

            if (Arguments.Contains("staging"))
            {
                version.Lifecycle = SemanticLifecycle.Staging;
            }

            if (Arguments.Contains("release"))
            {
                version.Lifecycle = SemanticLifecycle.Release;
            }
        }

        private static SemanticVersion SetBuild(SemanticVersion version, string projectFile)
        {
            string gitRepo = new FileInfo(projectFile).Directory.FullName;
            GitLog gitLog = GitLog.Get(gitRepo, 1).First();
            version.Build = gitLog.AbbreviatedCommitHash;
            return version.CopyAs<SemanticVersion>();
        }
    }
}