using Bam.Net.Application;
using Bam.Net.CommandLine;
using Bam.Net.Testing;
using System;
using System.Diagnostics;
using Bam.Net.Bake;

namespace Bam.Net.Bake
{
    [Serializable]
    partial class Program : CommandLineTool
    {
        static void Main(string[] args)
        {
            AddArguments();
            
            DefaultMethod = typeof(Program).GetMethod("Start");

            Initialize(args);
        }
        
        #region do not modify
        public static void Start()
        {
            ConsoleLogger logger = new ConsoleLogger
            {
                AddDetails = false
            };
            logger.StartLoggingThread();
            if (ExecuteSwitches(Arguments, typeof(ConsoleActions), false, logger))
            {
                logger.BlockUntilEventQueueIsEmpty();
            }
            else
            {
                Interactive();
            }
        }
        #endregion
    }
}