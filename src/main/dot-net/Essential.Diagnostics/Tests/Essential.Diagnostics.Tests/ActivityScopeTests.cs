﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Essential.Diagnostics.Tests
{
    [TestClass]
    public class ActivityScopeTests
    {
        [TestMethod]
        public void ScopeShouldChangeActivityId()
        {
            TraceSource source = new TraceSource("inmemory1Source");
            var listener = source.Listeners.OfType<InMemoryTraceListener>().First();
            listener.Clear();

            source.TraceEvent(TraceEventType.Warning, 1, "A");
            using (var scope = new ActivityScope())
            {
                source.TraceEvent(TraceEventType.Warning, 2, "B");
            }
            source.TraceEvent(TraceEventType.Warning, 3, "C");

            var events = listener.GetEvents();

            Assert.AreEqual(1, events[0].Id);
            Assert.AreEqual(Guid.Empty, events[0].ActivityId);

            Assert.AreEqual(2, events[1].Id);
            Assert.AreNotEqual(Guid.Empty, events[1].ActivityId);

            Assert.AreEqual(3, events[2].Id);
            Assert.AreEqual(Guid.Empty, events[2].ActivityId);
        }

        [TestMethod]
        public void ScopeShouldWriteActivities()
        {
            TraceSource source = new TraceSource("inmemory1Source");
            var listener = source.Listeners.OfType<InMemoryTraceListener>().First();
            listener.Clear();

            source.TraceEvent(TraceEventType.Warning, 1, "A");
            using (var scope = new ActivityScope(source, 11, 12, 13, 14))
            {
                source.TraceEvent(TraceEventType.Warning, 2, "B");
            }
            source.TraceEvent(TraceEventType.Warning, 3, "C");


            var events = listener.GetEvents();
            var innerActivityId = events[3].ActivityId;

            Assert.AreEqual(1, events[0].Id);
            Assert.AreEqual(Guid.Empty, events[0].ActivityId);

            Assert.AreEqual(11, events[1].Id);
            Assert.AreEqual(TraceEventType.Transfer, events[1].EventType);
            Assert.AreEqual(Guid.Empty, events[1].ActivityId);
            Assert.AreEqual(innerActivityId, events[1].RelatedActivityId);

            Assert.AreEqual(12, events[2].Id);
            Assert.AreEqual(TraceEventType.Start, events[2].EventType);
            Assert.AreEqual(innerActivityId, events[2].ActivityId);

            Assert.AreEqual(2, events[3].Id);
            Assert.AreEqual(TraceEventType.Warning, events[3].EventType);
            Assert.AreNotEqual(Guid.Empty, events[3].ActivityId);

            Assert.AreEqual(13, events[4].Id);
            Assert.AreEqual(TraceEventType.Transfer, events[4].EventType);
            Assert.AreEqual(innerActivityId, events[4].ActivityId);
            Assert.AreEqual(Guid.Empty, events[4].RelatedActivityId);

            Assert.AreEqual(14, events[5].Id);
            Assert.AreEqual(TraceEventType.Stop, events[5].EventType);
            Assert.AreEqual(innerActivityId, events[5].ActivityId);

            Assert.AreEqual(3, events[6].Id);
            Assert.AreEqual(Guid.Empty, events[6].ActivityId);
        }

    }
}
