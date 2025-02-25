Console.WriteLine("Hello, World!");

CancellationTokenSource turnMeOff = new ();
Console.CancelKeyPress += (s, e) =>
{
    Console.WriteLine("Goodbye...");
    turnMeOff.Cancel();
    e.Cancel = true;
};

await Common.ExampleEndpoint.CreateAndStartNServiceBusEndpoint("Beta", turnMeOff);

try
{
    await Task.Delay(Timeout.Infinite, turnMeOff.Token);
}
catch (TaskCanceledException)
{
}
