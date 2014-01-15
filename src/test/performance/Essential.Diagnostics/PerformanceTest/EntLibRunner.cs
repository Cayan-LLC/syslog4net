﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using System.Diagnostics;

namespace PerformanceTest
{
    class EntLibRunner : RunnerBase
    {
        string description;
        string category1;
        string category2;

        public EntLibRunner(string description, string source1Name, string source2Name)
        {
            this.description = description;
            category1 = source1Name;
            category2 = source2Name;
        }

        public override string Name
        {
            get
            {
                return "EntLib:" + description;
            }
        }

        protected override void TraceCritical1(int id, string message, params object[] data)
        {
            Logger.Write(string.Format(message, data), category1, 1, id, TraceEventType.Critical);
        }

        protected override void TraceVerbose1(int id, string message, params object[] data)
        {
            Logger.Write(string.Format(message, data), category1, 3, id, TraceEventType.Verbose);
        }

        protected override void TraceVerbose2(int id, string message, params object[] data)
        {
            Logger.Write(string.Format(message, data), category2, 4, id, TraceEventType.Verbose);
        }

        protected override void TraceWarning2(int id, string message, params object[] data)
        {
            Logger.Write(string.Format(message, data), category2, 2, id, TraceEventType.Warning);
        }
    }
}
