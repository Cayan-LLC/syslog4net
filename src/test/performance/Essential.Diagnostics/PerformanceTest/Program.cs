﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using log4net.Config;

namespace PerformanceTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var log4netConfig = "log4net.config";
            XmlConfigurator.Configure(new System.IO.FileInfo(log4netConfig));

            var iterations = 1000000;
            //var iterations = 1000;
            var nullRunner = new NullRunner();
            var runners = new RunnerBase[] {
                new CountingRunner(),
                new SystemDiagnosticsRunner("None", "NoSource1", "NoSource2"),
                new SystemDiagnosticsRunner("None (switch)", "SwitchOffSource1", "SwitchOffSource2"),
                new SystemDiagnosticsRunner("None (clear)", "ClearSource1", "ClearSource2"),
                new SystemDiagnosticsRunner("One Filtered", "WarningSource1", "NoSource2"),
                new SystemDiagnosticsRunner("Two Filtered", "WarningSource1", "WarningSource2"),
                new SystemDiagnosticsRunner("One Full", "FullSource1", "WarningSource2"),
                new SystemDiagnosticsRunner("No Clear - One Filt.", "NoClearWarningSource1", "NoSource2"),
                new SystemDiagnosticsRunner("No Clear - Two Filt.", "NoClearWarningSource1", "NoClearWarningSource2"),
                new SystemDiagnosticsRunner("No Clear - One Full", "NoClearFullSource1", "NoClearWarningSource2"),
                new SystemDiagnosticsRunner("Essential - One Filt.", "EssentialWarningSource1", "NoSource2"),
                new SystemDiagnosticsRunner("Essential - Two Filt.", "EssentialWarningSource1", "EssentialWarningSource2"),
                new SystemDiagnosticsRunner("Essential - One Full", "EssentialFullSource1", "EssentialWarningSource2"),
                new SystemDiagnosticsRunner("Events - One Filt.", "EventsWarningSource1", "NoSource2"),
                new SystemDiagnosticsRunner("Events - Two Filt.", "EventsWarningSource1", "EventsWarningSource2"),
                new SystemDiagnosticsRunner("Events - One Full", "EventsFullSource1", "EventsWarningSource2"),
                new log4netCheckRunner(),
                new log4netRunner("None", "None.NoSource1", "None.NoSource2"),
                new log4netRunner("One Filtered", "Warn.WarningSource1", "None.NoSource2"),
                new log4netRunner("Two Filtered", "Warn.WarningSource1", "Warn.WarningSource2"),
                new log4netRunner("One Full", "Full.FullSource1", "Warn.WarningSource2"),
                new NLogRunner("None", "None.NoSource1", "None.NoSource2"),
                new NLogRunner("One Filtered", "Warn.WarningSource1", "None.NoSource2"),
                new NLogRunner("Two Filtered", "Warn.WarningSource1", "Warn.WarningSource2"),
                new NLogRunner("One Full", "Full.FullSource1", "Warn.WarningSource2"),
                new EntLibRunner("None", "NoneCategory1", "NoneCategory2"),
                new EntLibRunner("One Filtered", "WarningCategory1", "NoneCategory2"),
                new EntLibRunner("Two Filtered", "WarningCategory1", "WarningCategory2"),
                new EntLibRunner("One Full", "FullCategory1", "WarningCategory2"),
                new EntLibNoFormatRunner("One Full", "FullCategory1", "WarningCategory2"),
                //new SystemDiagnosticsRunner("(bad source)", "BadSource", "BadSource"),
            };

            Console.WriteLine("Logging performance tester.");
            Console.WriteLine("- Sends 10 warmup trace messages.");
            Console.WriteLine("- Then times the send of {0} trace messages.", iterations);
            Console.WriteLine("");
            Console.WriteLine("Typical configurations tested for each logging system:");
            Console.WriteLine("- Logging turned off (ignore all messages)", iterations);
            Console.WriteLine("- Warnings logged for source 1", iterations);
            Console.WriteLine("- Warnings logged for both sources", iterations);
            Console.WriteLine("- All source 1, warnings for source 2", iterations);
            Console.WriteLine("");
            Console.WriteLine("Times (in milliseconds) show difference compared to a NullRunner that does nothing,");
            Console.WriteLine("i.e. to eliminate the overhead time taken for the actual looping.");

            Console.WriteLine("");
            Console.WriteLine("Warming up...");
            nullRunner.Output = false;
            nullRunner.Iterations = 10;
            nullRunner.Run();
            foreach (var runner in runners)
            {
                runner.Output = false;
                runner.Iterations = 10;
                runner.Run();
            }

            Console.WriteLine("Running performance tests:");
            Console.WriteLine("");
            nullRunner.Output = true;
            nullRunner.Iterations = iterations;
            nullRunner.Run();
            Console.WriteLine("");

            foreach (var runner in runners)
            {
                runner.BaseTime = nullRunner.Elapsed;
                runner.Output = true;
                runner.Iterations = iterations;
                runner.Run();
            }

            Console.WriteLine();
            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
