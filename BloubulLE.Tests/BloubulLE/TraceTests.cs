using System;
using Xunit;

namespace DH.BloubulLE.Tests.BloubulLE
{
    public class TraceTests
    {
        [Fact(DisplayName = "Trace should catch exceptions.")]
        public void Trace_should_catch_exceptions()
        {
            Boolean called = false;
            Action<String, Object[]> impl = (f, p) =>
            {
                called = true;
                throw new Exception();
            };

            Trace.TraceImplementation = impl;

            Trace.Message("Test", 1);
            Assert.True(called);
        }
    }
}