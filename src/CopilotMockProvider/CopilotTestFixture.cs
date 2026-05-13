using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CopilotMockProvider;

public class CopilotTestFixture : IAsyncLifetime
{
    private CopilotClient? _copilot;

    public ICopilotMock CreateAgentMock(Action<ICopilotMockBuilder>? configure = null)
    {
        if (_copilot is null)
        {
            throw new InvalidOperationException("Copilot client not initialized.");
        }

        var config = new SessionConfig
        {
            Model = "claude-sonnet-4.6",
            OnPermissionRequest = PermissionHandler.ApproveAll,
        };

        var builder = new Builder(config);

        configure?.Invoke(builder);

        return builder.Build(_copilot);
    }

    private sealed record Mock(AIAgent Agent, IServiceProvider Services) : ICopilotMock;

    private sealed record Builder(SessionConfig Config) : ICopilotMockBuilder
    {
        private readonly List<Action<SessionConfig, IServiceProvider>> _list = [];
        private readonly ServiceCollection _services = new();

        public IServiceCollection Services => _services;

        public void Use(Action<SessionConfig, IServiceProvider> configure)
        {
            _list.Add(configure);
        }

        public ICopilotMock Build(CopilotClient client)
        {
            var serviceProvider = _services.BuildServiceProvider();

            foreach (var configure in _list)
            {
                configure(Config, serviceProvider);
            }

            return new Mock(client.AsAIAgent(Config), serviceProvider);
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_copilot is { } copilot)
        {
            _copilot = null;

            await copilot.StopAsync();
            await copilot.DisposeAsync();
        }
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        var options = new CopilotClientOptions();

        _copilot = new CopilotClient(options);

        await _copilot.StartAsync();
    }
}
