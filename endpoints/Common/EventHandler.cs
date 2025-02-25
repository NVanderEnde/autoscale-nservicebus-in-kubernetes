namespace Common;

public class EventHandler : IHandleMessages<SomeGreatEvent>
{
    public static int DelayMilliseconds { get; set; } = 3000;
    public async Task Handle(SomeGreatEvent message, IMessageHandlerContext context)
    {
        Console.WriteLine($"Received MyEvent with Id {message.Id}");
        await Task.Delay(DelayMilliseconds, context.CancellationToken);
    }
}
