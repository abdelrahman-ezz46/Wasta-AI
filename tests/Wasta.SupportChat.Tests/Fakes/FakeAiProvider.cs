using Wasta.Ai;

namespace Wasta.SupportChat.Tests.Fakes;

public class FakeAiProvider : IAiProvider
{
    public string Name { get; }
    public bool IsConfigured { get; set; } = true;
    public int CallCount { get; private set; }

    private readonly Queue<Func<string>> _responses = new();
    private readonly Queue<Exception> _exceptions = new();
    private readonly Queue<bool> _script = new();

    public FakeAiProvider(string name)
    {
        Name = name;
    }

    public FakeAiProvider EnqueueResponse(string response)
    {
        _responses.Enqueue(() => response);
        _script.Enqueue(true);
        return this;
    }

    public FakeAiProvider EnqueueThrow(Exception exception)
    {
        _exceptions.Enqueue(exception);
        _script.Enqueue(false);
        return this;
    }

    public Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AiChatTurn> turns, AiCallOptions? callOptions, CancellationToken ct)
    {
        CallCount++;
        LastSystemPrompt = systemPrompt;
        LastTurns = turns;

        if (_script.Count == 0)
        {
            throw new InvalidOperationException("FakeAiProvider has no more scripted responses.");
        }

        var respond = _script.Dequeue();
        if (respond)
        {
            return Task.FromResult(_responses.Dequeue()());
        }

        throw _exceptions.Dequeue();
    }

    public string? LastSystemPrompt { get; private set; }
    public IReadOnlyList<AiChatTurn>? LastTurns { get; private set; }
}
