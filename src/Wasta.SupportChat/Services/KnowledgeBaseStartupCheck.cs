using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wasta.SupportChat.Services;

/// <summary>
/// Reports the state of the knowledge base once at startup.
///
/// Stripping TODO drafts stops them leaking to users, but silently - and a
/// silent gap is how a knowledge base stays half-written for months while
/// the bot tells people it doesn't know things it should. This makes the
/// gap loud on every boot, and fails fast if the file is missing entirely
/// rather than degrading on the first customer question.
/// </summary>
public class KnowledgeBaseStartupCheck : IHostedService
{
    private readonly SupportChatOptions _options;
    private readonly ILogger<KnowledgeBaseStartupCheck> _logger;

    public KnowledgeBaseStartupCheck(IOptions<SupportChatOptions> options, ILogger<KnowledgeBaseStartupCheck> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var knowledge = await KnowledgeBaseLoader.LoadAsync(_options.KnowledgePath, cancellationToken);

            if (knowledge.UnresolvedTodoCount > 0)
            {
                _logger.LogWarning(
                    "Support chat knowledge base has {Count} unresolved TODO section(s) in {Path}. "
                    + "They are stripped before reaching the model, so the chatbot cannot answer those topics "
                    + "and will say it does not know.",
                    knowledge.UnresolvedTodoCount, _options.KnowledgePath);
            }
            else
            {
                _logger.LogInformation("Support chat knowledge base loaded with no unresolved TODOs.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Support chat knowledge base could not be loaded from {Path}. The chatbot will fail every message.",
                _options.KnowledgePath);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
