using System;
using System.IO;
using System.Threading;
using Bam.Net.Analytics;
using Bam.Net.Automation;
using Bam.Net.CommandLine;

namespace Bam.Net.Bake
{
    public partial class ConsoleActions
    {
        [ConsoleAction("environmentVariables", "/read current environment variables into a file and/or /write environment variables from a file.")]
        public void EnvironmentVariables()
        {
            string fileSystemPath = GetArgument("environmentVariables", $"./{nameof(BuildVariables)}.yaml");
            bool read = Arguments.Contains("read");
            Enum.TryParse(Arguments["read"], out BamEnvironmentVariableTarget readTarget);
            if (readTarget == BamEnvironmentVariableTarget.Invalid)
            {
                readTarget = BamEnvironmentVariableTarget.Environment;
            }
            bool write = Arguments.Contains("write");
            Enum.TryParse(Arguments["write"], out BamEnvironmentVariableTarget writeTarget);
            if (writeTarget == BamEnvironmentVariableTarget.Invalid)
            {
                writeTarget = BamEnvironmentVariableTarget.File;
            }
            
            FileInfo fileInfo = new FileInfo(fileSystemPath);
            BuildVariables source = BuildVariables.FromEnvironmentVariables();
            if (read)
            {
                switch (readTarget)
                {
                    case BamEnvironmentVariableTarget.Invalid:
                    case BamEnvironmentVariableTarget.Environment:
                        break;
                    case BamEnvironmentVariableTarget.File:
                        source = BuildVariables.FromYaml(fileSystemPath);
                        break;
                    case BamEnvironmentVariableTarget.Directory:
                        source = BuildVariables.FromDirectory(fileSystemPath);
                        break;
                }
            }
            
            BuildVariables destination = BuildVariables.FromEnvironmentVariables();
            FileInfo fileToWrite = new FileInfo(EnvironmentVariable.DefaultVarFile);
            if (File.Exists(fileSystemPath))
            {
                fileToWrite = new FileInfo(fileSystemPath);
            }
            if (write)
            {
                switch (writeTarget)
                {
                    case BamEnvironmentVariableTarget.Invalid:
                    case BamEnvironmentVariableTarget.Environment:
                        EnvironmentVariable[] environmentVariables = source.ToEnvironmentVariables();
                        destination = environmentVariables.ToInstance<BuildVariables>();
                        break;
                    case BamEnvironmentVariableTarget.File:
                        source.ToYamlFile(fileToWrite.FullName);
                        destination = BuildVariables.FromYaml(fileToWrite.FullName);
                        break;
                    case BamEnvironmentVariableTarget.Directory:
                        if (!Directory.Exists(fileSystemPath))
                        {
                            Directory.CreateDirectory(fileSystemPath);
                        }

                        source.ToDirectory(fileSystemPath);
                        destination = BuildVariables.FromDirectory(fileSystemPath);
                        break;        
                }
            }

            Message.PrintDiff(source.ToYaml(), destination.ToYaml());
        }
    }
}