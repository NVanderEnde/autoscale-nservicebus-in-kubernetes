Console.WriteLine("Hello, World!");
// make this endpoint slower so that different critical times and message throughputs can be observed
Common.EventHandler.DelayMilliseconds = 10_000;

CancellationTokenSource turnMeOff = new ();
Console.CancelKeyPress += (s, e) =>
{
    Console.WriteLine("Goodbye...");
    turnMeOff.Cancel();
    e.Cancel = true;
};

await Common.ExampleEndpoint.CreateAndStartNServiceBusEndpoint("Alpha", turnMeOff);

try
{
    await Task.Delay(Timeout.Infinite, turnMeOff.Token);
}
catch (TaskCanceledException)
{
}
