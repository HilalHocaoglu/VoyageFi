using System.ComponentModel;
using Microsoft.SemanticKernel;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.Infrastructure.AI;

public class VoyageFiAgentService : IVoyageFiAgentService
{
    private readonly Kernel _kernel;
    private readonly IRedisCacheService _cache;

    public VoyageFiAgentService(IRedisCacheService cache)
    {
        var builder = Kernel.CreateBuilder();
        
        // Add our plugin containing the pgvector search tool
        builder.Plugins.AddFromType<KnowledgeBasePlugin>("VoyageFiKB");
        
        _kernel = builder.Build();
        _cache = cache;
    }

    public async Task<string> ResolveAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var lowerPrompt = prompt.ToLowerInvariant();

        // Cache Key Oluşturma (Prompt Hash'i)
        var promptHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(lowerPrompt)));
        var cacheKey = $"ai_prompt:{promptHash}";

        // Redis Cache Kontrolü
        var cachedResponse = await _cache.GetAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedResponse))
        {
            return cachedResponse;
        }

        // SIMULATION OF LLM FUNCTION CALLING
        // Since we have a fake API key, the LLM won't be able to return a tool call response directly.
        // We simulate the Agent's decision-making process here.

        // 1. Intent Recognition: The Agent decides if it needs to use a tool.
        if (lowerPrompt.Contains("rötar") || lowerPrompt.Contains("gecikti") || lowerPrompt.Contains("delay"))
        {
            // 2. Tool Calling: The Agent invokes the search tool (function calling).
            var plugin = _kernel.Plugins["VoyageFiKB"];
            
            var args = new KernelArguments
            {
                { "query", "flight delay rules and lounge campaigns" }
            };

            // 3. Tool Execution: Calling the actual method
            var functionResult = await plugin["SearchPgVectorAsync"].InvokeAsync(_kernel, args, cancellationToken);
            
            // 4. Synthesis: The Agent synthesizes the retrieved documents to provide a final response.
            var synthesizedResponse = $@"AI Ajanı Yanıtı: Yaşadığınız durum için üzgünüz. Sistemimizdeki kuralları ve kampanyaları sizin için taradım.

Bulduğum Bilgiler:
{functionResult.GetValue<string>()}

Özetle, havayolu şirketinizden yemek talep edebilir ve beklerken Garanti BBVA kartınızla Lounge'dan ücretsiz yararlanabilirsiniz.";

            // Sonucu Redis'e kaydet (1 saat geçerli)
            await _cache.SetAsync(cacheKey, synthesizedResponse, TimeSpan.FromHours(1));

            return synthesizedResponse;
        }

        // Generic fallback response for other prompts
        var fallbackResponse = $"AI Ajanı Yanıtı: '{prompt}' talebinizi anladım. Size nasıl daha fazla yardımcı olabilirim?";
        
        // Sonucu Redis'e kaydet (1 saat geçerli)
        await _cache.SetAsync(cacheKey, fallbackResponse, TimeSpan.FromHours(1));

        return fallbackResponse;
    }
}

public class KnowledgeBasePlugin
{
    [KernelFunction("SearchPgVectorAsync"), Description("Searches the pgvector database for airline rules (e.g. SHY-YOLCU) and bank campaigns.")]
    public async Task<string> SearchPgVectorAsync(
        [Description("The search query")] string query)
    {
        // Mock PgVector Search execution
        await Task.Delay(500); // Simulate DB delay
        
        return """
        * [SHY-YOLCU Kuralları]: İç hat uçuşlarında 3 saati aşan rötarlarda yolcuya sıcak yemek ve içecek ikramı zorunludur.
        * [Banka Lounge Kampanyası]: Garanti BBVA kredi kartınızla havaalanındaki anlaşmalı Lounge hizmetlerinden, uçuşunuz 2 saatten fazla rötar yaptığında ücretsiz yararlanabilirsiniz.
        """;
    }
}
