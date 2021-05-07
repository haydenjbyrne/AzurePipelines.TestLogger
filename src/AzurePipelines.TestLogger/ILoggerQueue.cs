namespace AzurePipelines.TestLogger
{
    internal interface ILoggerQueue
    {
        void Enqueue(ITestResult testResult);
        void Flush();
    }
}