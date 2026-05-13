using Microsoft.Agents.AI;

namespace CopilotMockProvider;

public interface ICopilotMock
{
    AIAgent Agent { get; }

    IServiceProvider Services { get; }
}
