using System.Security;

namespace VoyageFi.Application.Security;

public static class PromptInjectionGuard
{
    private static readonly string[] BlockedPhrases =
    [
        "ignore all previous instructions",
        "forget your prompt",
        "ignore previous instructions",
        "disregard previous instructions",
        "system prompt",
        "system message",
        "bypass security",
        "you are now",
        "simulate a scenario",
        "developer mode"
    ];

    public static void Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        var lowerInput = input.ToLowerInvariant();

        foreach (var phrase in BlockedPhrases)
        {
            if (lowerInput.Contains(phrase))
            {
                throw new SecurityException($"Prompt injection detected. Request rejected due to containing malicious phrase: '{phrase}'.");
            }
        }
    }
}
