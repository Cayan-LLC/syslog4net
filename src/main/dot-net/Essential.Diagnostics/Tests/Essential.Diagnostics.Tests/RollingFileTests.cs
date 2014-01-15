﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Essential.Diagnostics.Tests.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Essential.Diagnostics.Tests
{
    [TestClass]
    public class RollingFileTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void FileHandlesEventSentDirectly()
        {
            var mockFileSystem = new MockFileSystem();
            var listener = new RollingFileTraceListener(null);
            listener.FileSystem = mockFileSystem;

            listener.TraceEvent(null, "source", TraceEventType.Information, 1, "{0}-{1}", 2, "A");
            listener.Flush();

            Assert.AreEqual(1, mockFileSystem.OpenedItems.Count);
            var tuple0 = mockFileSystem.OpenedItems[0];
            // VS2012 process name (earlier name was "QTAgent32-")
            StringAssert.StartsWith(tuple0.Item1, "vstest.executionengine.x86-" + DateTimeOffset.Now.Year.ToString());
            var data = tuple0.Item2.GetBuffer();
            var output = Encoding.UTF8.GetString(data, 0, (int)tuple0.Item2.Length);
            StringAssert.Contains(output, "Information source 1: 2-A");
        }

        [TestMethod]
        public void FileHandlesEventFromTraceSource()
        {
            var mockFileSystem = new MockFileSystem();
            TraceSource source = new TraceSource("rollingFile1Source");
            var listener = source.Listeners.OfType<RollingFileTraceListener>().First();
            listener.FileSystem = mockFileSystem;

            source.TraceEvent(TraceEventType.Warning, 2, "{0}-{1}", 3, "B");
            source.Flush(); // or have AutoFlush configured

            Assert.AreEqual(1, mockFileSystem.OpenedItems.Count);
            var tuple0 = mockFileSystem.OpenedItems[0];
            // VS2012 process name (earlier name was "QTAgent32-")
            StringAssert.StartsWith(tuple0.Item1, "vstest.executionengine.x86-" + DateTimeOffset.Now.Year.ToString());
            var output = Encoding.UTF8.GetString(tuple0.Item2.GetBuffer(), 0, (int)tuple0.Item2.Length);
            StringAssert.Contains(output, "Warning rollingFile1Source 2: 3-B");
        }

        [TestMethod]
        public void FileRollOverTest()
        {
            var mockFileSystem = new MockFileSystem();
            var listener = new RollingFileTraceListener("Log{DateTime:HHmmss}");
            listener.FileSystem = mockFileSystem;

            listener.TraceEvent(null, "souce", TraceEventType.Information, 1, "A");
            Thread.Sleep(TimeSpan.FromSeconds(2));
            listener.TraceEvent(null, "souce", TraceEventType.Information, 2, "B");
            listener.Flush();

            Assert.AreEqual(2, mockFileSystem.OpenedItems.Count);
        }

        [TestMethod]
        public void FileConfigParametersLoadedCorrectly()
        {
            TraceSource source = new TraceSource("rollingFile2Source");
            var listener = source.Listeners.OfType<RollingFileTraceListener>().First();

            Assert.AreEqual("rollingFile2", listener.Name);
            Assert.AreEqual("{DateTime},{EventType},{Message}", listener.Template);
            Assert.AreEqual("Trace{DateTime:yyyyMMdd}.log", listener.FilePathTemplate);
        }

    }
}
