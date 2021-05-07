using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AzurePipelines.TestLogger
{
    internal class AlternateLoggerQueue : ILoggerQueue
    {
        private readonly AsyncProducerConsumerCollection<ITestResult> _queue = new AsyncProducerConsumerCollection<ITestResult>();
        private readonly Task _consumeTask;
        private readonly CancellationTokenSource _consumeTaskCancellationSource = new CancellationTokenSource();

        private readonly IApiClient _apiClient;
        private readonly string _buildId;
        private readonly string _agentName;
        private readonly string _jobName;

        // Internal for testing
        internal DateTime StartedDate { get; } = DateTime.UtcNow;
        internal int RunId { get; set; }

        public AlternateLoggerQueue(IApiClient apiClient, string buildId, string agentName, string jobName)
        {
            _apiClient = apiClient;
            _buildId = buildId;
            _agentName = agentName;
            _jobName = jobName;

            _consumeTask = ConsumeItemsAsync(_consumeTaskCancellationSource.Token);
        }

        public void Enqueue(ITestResult testResult) => _queue.Add(testResult);

        public void Flush()
        {
            // Cancel any idle consumers and let them return
            _queue.Cancel();

            try
            {
                // Any active consumer will circle back around and batch post the remaining queue
                _consumeTask.Wait(TimeSpan.FromSeconds(60));

                // Update the run and parents to a completed state
                SendTestsCompleted(_consumeTaskCancellationSource.Token).Wait(TimeSpan.FromSeconds(60));

                // Cancel any active HTTP requests if still hasn't finished flushing
                _consumeTaskCancellationSource.Cancel();
                if (!_consumeTask.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Cancellation didn't happen quickly");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task ConsumeItemsAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                ITestResult[] nextItems = await _queue.TakeAsync().ConfigureAwait(false);

                if (nextItems == null || nextItems.Length == 0)
                {
                    // Queue is canceling and is empty
                    return;
                }

                await SendResultsAsync(nextItems, cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private async Task SendResultsAsync(ITestResult[] testResults, CancellationToken cancellationToken)
        {
            try
            {
                // Create a test run if we need it
                if (RunId == 0)
                {
                    RunId = await CreateTestRun(cancellationToken).ConfigureAwait(false);
                }

                await CreateTests(testResults, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Eat any communications exceptions
            }
        }

        // Internal for testing
        internal static string GetSource(ITestResult[] testResults)
        {
            string source = Array.Find(testResults, x => !string.IsNullOrEmpty(x.Source))?.Source;
            if (source != null)
            {
                source = Path.GetFileName(source);
                if (source.EndsWith(".dll"))
                {
                    return source.Substring(0, source.Length - 4);
                }
            }
            return source;
        }

        // Internal for testing
        internal static string GetSource(ITestResult testResult)
        {
            string source = testResult.Source;
            if (source != null)
            {
                source = Path.GetFileName(source);
                if (source.EndsWith(".dll"))
                {
                    return source.Substring(0, source.Length - 4);
                }
            }
            return source;
        }

        // Internal for testing
        internal async Task<int> CreateTestRun(CancellationToken cancellationToken)
        {
            string runName = $"VSTest Test Run (Job: {_jobName}, Agent: {_agentName})";

            TestRun testRun = new TestRun
            {
                Name = runName,
                BuildId = _buildId,
                StartedDate = StartedDate,
                IsAutomated = true
            };

            return await _apiClient.AddTestRun(testRun, cancellationToken).ConfigureAwait(false);
        }

        // Internal for testing
        internal async Task CreateTests(ITestResult[] testResults, CancellationToken cancellationToken)
        {
            if (testResults.Length > 0)
            {
                await _apiClient.AddTestResults(RunId, testResults, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SendTestsCompleted(CancellationToken cancellationToken)
        {
            DateTime completedDate = DateTime.UtcNow;

            // Mark all parents as completed (but only if we actually created a parent)
            if (RunId != 0)
            {
                await _apiClient.MarkTestRunCompleted(RunId, StartedDate, completedDate, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}