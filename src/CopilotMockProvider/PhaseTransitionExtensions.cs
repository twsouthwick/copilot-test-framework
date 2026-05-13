using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CopilotMockProvider;

public static class PhaseTransitionExtensions
{
    extension(ICopilotMockBuilder builder)
    {
        public ICopilotMockBuilder AddPhaseTransitionCallback(PhaseTransition callback)
        {
            var tools = new TransitionTools(callback);
            var toolName = nameof(TransitionTools.PhaseTransitionInterceptor);

            return builder
                .AddTool(AIFunctionFactory.Create(tools.PhaseTransitionInterceptor))
                .AddSystemMessage(
                    $"""
                    CRITICAL INSTRUCTION — Phase Transition Protocol:

                    Before you begin working through any numbered or labeled sequential sections
                    (steps, phases, stages, tasks, parts, or similar), first COUNT the total
                    number of sections. You MUST call the {toolName} tool exactly that many
                    times — once after completing EACH section, including the very first AND
                    the very last.

                    Example for a 4-step problem:
                      Complete Step 1 → call {toolName} (1 of 4)
                      Complete Step 2 → call {toolName} (2 of 4)
                      Complete Step 3 → call {toolName} (3 of 4)
                      Complete Step 4 → call {toolName} (4 of 4) ← DO NOT SKIP THIS

                    The call after the LAST section is the most critical — never omit it.
                    You must call {toolName} for the final section before writing any
                    concluding remarks or summary.

                    Do not call {toolName} for non-section reasoning — only once per
                    numbered/labeled section, immediately after finishing that section's work.
                    """);
        }
    }

    private sealed class TransitionTools(PhaseTransition Callback)
    {
        [Description("Record completion of a numbered or labeled section. Must be called once per section, especially the final one.")]
        public string PhaseTransitionInterceptor(
            [Description("The section label exactly as numbered, e.g. 'Step 1', 'Phase 2', 'Stage A'.")] string phaseName,
            [Description("A brief summary of what was accomplished in this section.")] string phaseSummary)
        {
            Callback(phaseName, phaseSummary);
            return $"Phase '{phaseName}' recorded. Continue to the next section, or finalize if this was the last one.";
        }
    }

    public delegate void PhaseTransition(string name, string summary);
}
