using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

namespace CopilotMockProvider;

public static class ToolsExtensions
{
    extension(ICopilotMockBuilder builder)
    {
        public ICopilotMockBuilder AddTool(AIFunction function)
        {
            builder.Use((config, _) =>
            {
                config.Tools ??= [];
                config.Tools.Add(function);
            });

            return builder;
        }

        public ICopilotMockBuilder AddSystemMessage(string content)
        {
            builder.Use((config, _) =>
            {
                config.SystemMessage ??= new();
                config.SystemMessage.Content += content;
                config.SystemMessage.Mode = SystemMessageMode.Append;
            });
            return builder;
        }
    }
}
