using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using NUnit.Framework;
using Shouldly;

namespace AzurePipelines.TestLogger.Tests
{
    [TestFixture]
    public class AlternateLoggerQueueTests
    {
        private const string _dateFormatString = "yyyy-MM-ddTHH:mm:ss.FFFZ";

        [Test]
        public void CreateTestRun()
        {
            // Given
            TestApiClient apiClient = new TestApiClient(_ => "{ \"id\": 1234 }");
            AlternateLoggerQueue loggerQueue = new AlternateLoggerQueue(apiClient, "987", "foo", "bar");

            // When
            int id = loggerQueue.CreateTestRun(CancellationToken.None).Result;

            // Then
            id.ShouldBe(1234);
            apiClient.Messages.ShouldBe(new[]
            {
                new ClientMessage(
                    HttpMethod.Post,
                    null,
                    "5.0",
                    $@"{{
                        ""name"": ""VSTest Test Run (Job: bar, Agent: foo)"",
                        ""build"": {{""id"":""987""}},
                        ""startedDate"": ""{loggerQueue.StartedDate.ToString(_dateFormatString)}"",
                        ""isAutomated"": true
                    }}")
            });
        }
    }
}
