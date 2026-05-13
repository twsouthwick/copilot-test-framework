namespace CopilotMockProvider;

public class PhaseChecks(CopilotTestFixture copilot) : IClassFixture<CopilotTestFixture>
{
    [Fact]
    public async Task StepsListed()
    {
        var list = new List<string>();

        var mock = copilot.CreateAgentMock(builder =>
        {
            builder.AddPhaseTransitionCallback((name, summary) =>
            {
                list.Add(name);
            });
        });

        const string Prompt = """
            
            Solve this logic puzzle step by step:

            Three developers (Alice, Bob, Carol) work with three languages (C#, Python, Java). 
            - Alice doesn't use Python
            - The person who uses Java sits next to Bob
            - Carol uses C#

            Step 1: List what we know and what we need to find out.

            Step 2: Use the first clue to eliminate possibilities for Alice.

            Step 3: Use the second and third clues to determine Bob's language.

            Step 4: Conclude who uses which language and verify all clues are satisfied.
            
            """;


        await mock.Agent.RunAsync(Prompt);

        Assert.Equal(new[] { "Step 1", "Step 2", "Step 3", "Step 4" }, list);
    }
}
