﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace AzurePipelines.TestLogger.Tests
{
    public class TestTestResult : ITestResult
    {
        public Guid Id { get; }

        public string Source { get; set; }

        public string FullyQualifiedName { get; set; }

        public string DisplayName { get; set; }

        public TestOutcome Outcome { get; set; }

        public DateTimeOffset StartTime { get; set; }

        public DateTimeOffset EndTime { get; set; }

        public TimeSpan Duration { get; set; }

        public string ErrorStackTrace { get; set; }

        public string ErrorMessage { get; set; }

        public IList<TestResultMessage> Messages { get; } = new List<TestResultMessage>();

        public string ComputerName { get; set; }

        public Collection<AttachmentSet> Attachments { get; set; }
    }
}
