﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;

namespace Essential.Diagnostics
{
    /// <summary>
    /// Trace filter that evaluates a user-supplied boolean expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The initalizeData for the filter contains the C# expression that
    /// is evaluated. The expression must return a boolean result and
    /// can use any of the parameters: Source, EventType, Id, Format,
    /// Callstack, DateTime, LogicalOperationStack, ProcessId, ThreadId, 
    /// Timestamp.
    /// </para>
    /// <para>
    /// You can also use an C# expression, including accessing environment
    /// details. Note that expressions are case sensitive; the parameter names
    /// above must be cased correctly.
    /// </para>
    /// <para>
    /// Note that Format is the format template or message (if there are no
    /// format parameters).
    /// </para>
    /// </remarks>
    public class ExpressionFilter : TraceFilter
    {
        // Use some techniques from http://www.codeproject.com/KB/cs/ExpressionEval.aspx

        ExpressionBase compiledExpression;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="expression">A C# expression that is evaluated to determine if the event should be traced or not.</param>
        public ExpressionFilter(string expression)
        {
            if (expression == null) throw new ArgumentNullException("expression");
            compiledExpression = CompileExpression(expression);
        }

        /// <summary>
        /// Determines whether the event should be traced by the listener or not.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Portability", "CA1903:UseOnlyApiFromTargetedFramework", MessageId = "System.DateTimeOffset", Justification = "Deliberate dependency, .NET 2.0 SP1 required.")]
        public override bool ShouldTrace(TraceEventCache cache, string source, TraceEventType eventType, int id, string formatOrMessage, object[] args, object data1, object[] data)
        {
            var eventCache = cache ?? new TraceEventCache();
            var dateTimeOffset = (DateTimeOffset)eventCache.DateTime;
            // Note: eventCache is *not* in the publically documented API
            return compiledExpression.ShouldTrace(eventCache, source, eventType, id, formatOrMessage, eventCache.Callstack, 
                dateTimeOffset, eventCache.LogicalOperationStack, eventCache.ProcessId, eventCache.ThreadId, eventCache.Timestamp);
        }

        static ExpressionBase CompileExpression(string expression)
        {
            string className = "Class_" + Guid.NewGuid().ToString("N");

            var source = new StringBuilder();
            source.AppendLine("using System;");
            source.AppendLine("using System.Diagnostics;");
            source.AppendLine("using System.Text.RegularExpressions;");
            source.AppendLine("namespace Essential.Diagnostics.Dynamic {");
            source.AppendLine("  public class " + className + " : Essential.Diagnostics.ExpressionFilter.ExpressionBase {");
            source.AppendLine("    protected override bool ShouldTrace(TraceEventCache EventCache, string Source, TraceEventType EventType, int Id, string Format, string Callstack, DateTimeOffset DateTime, System.Collections.Stack LogicalOperationStack, int ProcessId, string ThreadId, long Timestamp) {");
            source.AppendLine("      return " + expression + ";");
            source.AppendLine("    }"); // End of method
            source.AppendLine("  }"); // End of class
            source.AppendLine("}"); // End of namespace

            CompilerResults results = null;
            using (var csprovider = new CSharpCodeProvider())
            {
                var options = new CompilerParameters()
                {
                    GenerateInMemory = true,
                    TreatWarningsAsErrors = true
                };
                options.ReferencedAssemblies.Add("System.dll");
                options.ReferencedAssemblies.Add(typeof(ExpressionBase).Assembly.Location);
                results = csprovider.CompileAssemblyFromSource(options, source.ToString());
            }

            if (results.Errors.Count > 0)
            {
                var message = new StringBuilder();
                message.AppendLine(string.Format(CultureInfo.CurrentCulture, "Failed to compile filter expression [{0}].", expression));
                foreach (var error in results.Errors)
                {
                    message.AppendLine(error.ToString());
                }
                throw new ArgumentException(message.ToString());
            }
        
            var assembly = results.CompiledAssembly;
            var dynamicType = assembly.GetType("Essential.Diagnostics.Dynamic." + className);
            return (ExpressionBase)Activator.CreateInstance(dynamicType);
        }

        /// <summary>
        /// Base class used internally by ExpressionFilter to compile dynamic methods.
        /// This class is not intended to be used outside this filter.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public abstract class ExpressionBase
        {
            /// <summary>
            /// Evaluates the filter expression to determine if an event should be traced.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Callstack", Justification = "Consistency with TraceEventCache.Callstack parameter casing.")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Portability", "CA1903:UseOnlyApiFromTargetedFramework", MessageId = "System.DateTimeOffset", Justification = "Deliberate dependency, .NET 2.0 SP1 required.")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", Justification = "Deliberate so that title casing is used in the filter expression.")]
            protected internal abstract bool ShouldTrace(TraceEventCache EventCache, string Source, TraceEventType EventType, int Id, string Format, string Callstack, DateTimeOffset DateTime, System.Collections.Stack LogicalOperationStack, int ProcessId, string ThreadId, long Timestamp);
        }
    }
}
