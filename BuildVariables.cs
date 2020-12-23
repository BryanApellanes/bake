using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Bam.Net.Automation;

namespace Bam.Net.Bake
{
    public class BuildVariables 
    {
        [EnvironmentVariable("BAMARTIFACTS")]
        public string BamArtifacts { get; set; }
        
        [EnvironmentVariable("BAMARTIFACTSWINDOWS")]
        public string BamArtifactsWindows { get; set; }
        
        [EnvironmentVariable("BAMLIFECYCLE")]
        public string BamLifeCycle { get; set; }
        
        [EnvironmentVariable("BAMSRCROOT")]
        public string BamSrcRoot { get; set; }
        
        [EnvironmentVariable("BAMTOOLKITBIN")]
        public string BamToolkitBin { get; set; }
        
        [EnvironmentVariable("BAMTOOLKITHOME")]
        public string BamToolkitHome { get; set; }
        
        [EnvironmentVariable("BAMTOOLKITHOMEWINDOWS")]
        public string BamToolkitHomeWindows { get; set; }
        
        [EnvironmentVariable("BAMTOOLKITSYMLINKS")]
        public string BamToolkitSymLinks { get; set; }
        
        [EnvironmentVariable("DIST")]
        public string Dist { get; set; }
        
        [EnvironmentVariable("OUTPUTBIN")]
        public string OutputBin { get; set; }
        
        [EnvironmentVariable("OUTPUTBINWINDOWS")]
        public string OutputBinWindows { get; set; }
        
        [EnvironmentVariable("OUTPUTRECIPES")]
        public string OutputRecipes { get; set; }
        
        [EnvironmentVariable("TESTBIN")]
        public string TestBin { get; set; }
        
        [EnvironmentVariable("TESTBINWINDOWS")]
        public string TestBinWindows { get; set; }

        public string ToJson()
        {
            return Extensions.ToJson(this);
        }

        public void ToYamlFile(FileInfo file)
        {
            ToYamlFile(file.FullName);
        }
        
        public void ToYamlFile(string filePath)
        {
            ToYaml().SafeWriteToFile(filePath, true);
        }
        
        public string ToYaml()
        {
            return YamlExtensions.ToYaml(this);
        }

        public EnvironmentVariableDirectory ToDirectory(string directoryPath = EnvironmentVariableDirectory.DefaultName)
        {
            EnvironmentVariableDirectory directory = new EnvironmentVariableDirectory
            {
                EnvironmentVariables = EnvironmentVariable.FromInstance(this).ToArray()
            };
            directory.Create();
            return directory;
        }
        
        /// <summary>
        /// Sets the environment variables using values from this instance.
        /// </summary>
        public EnvironmentVariable[] ToEnvironmentVariables()
        {
            List<EnvironmentVariable> results = new List<EnvironmentVariable>();
            foreach (PropertyInfo propertyInfo in typeof(BuildVariables).GetProperties())
            {
                if (propertyInfo.HasCustomAttributeOfType<EnvironmentVariableAttribute>(out EnvironmentVariableAttribute attr))
                {
                    string value = (string) propertyInfo.GetValue(this);
                    attr.SetEnvironmentVariable(value);
                    results.Add(new EnvironmentVariable{Name = attr.Name, Value = value});
                }
            }

            return results.ToArray();
        }

        public static BuildVariables FromDirectory(string directoryPath)
        {
            EnvironmentVariable[] fromDirectory = EnvironmentVariable.FromDirectory(directoryPath);
            return fromDirectory.ToInstance<BuildVariables>();
        }

        public static BuildVariables FromEnvironmentVariables()
        {
            BuildVariables result = new BuildVariables();
            Type buildVariableType = typeof(BuildVariables);
            foreach (PropertyInfo propertyInfo in buildVariableType.GetProperties())
            {
                if (propertyInfo.HasCustomAttributeOfType<EnvironmentVariableAttribute>(out EnvironmentVariableAttribute attr))
                {
                    propertyInfo.SetValue(result, attr.Value);
                }
            }

            return result;
        }
        
        public static BuildVariables FromJson(string filePath)
        {
            return filePath.FromJsonFile<BuildVariables>();
        }

        public static BuildVariables FromYaml(string filePath)
        {
            return filePath.FromYamlFile<BuildVariables>();
        }
    }
}