using Common;

Console.WriteLine("Hello, World!");
CancellationTokenSource stop = new();
Console.CancelKeyPress += (s, e) =>
{
    Console.WriteLine("Goodbye...");
    stop.Cancel();
    e.Cancel = true;
};

var endpointInstance = await ExampleEndpoint.CreateAndStartNServiceBusEndpoint("Event_Producer", stop, c => c.SendOnly());

while (!stop.IsCancellationRequested)
{
    Console.WriteLine("Provide an integer amount of events you would like to publish - for example, 1 or 20:");
    var userInput = Console.ReadLine();
    if (!stop.IsCancellationRequested && int.TryParse(userInput, out int amount))
    {
        for (int i = 0; i < amount; i++)
        {
            await endpointInstance.Publish(new SomeGreatEvent(Guid.NewGuid()));
        }
    }
    else
    {
        Console.Error.WriteLine($"{userInput} is not valid input. Please provide an integer.");
    }
}
