using GitHub.Copilot.SDK;
using Microsoft.Extensions.DependencyInjection;

namespace CopilotMockProvider
{
    public interface ICopilotMockBuilder
    {
        public void Use(Action<SessionConfig, IServiceProvider> configure);

        public IServiceCollection Services { get; }
    }
}
