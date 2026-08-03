using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoyageFi.Application.Security;
using VoyageFi.Core.Interfaces;
using System.Security;

namespace VoyageFi.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly IVoyageFiAgentService _agentService;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(IVoyageFiAgentService agentService, ILogger<AssistantController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    public class ResolveRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }

    [HttpPost("resolve")]
    public async Task<IActionResult> ResolveAsync([FromBody] ResolveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. AI Security: Prompt Injection Guard
            PromptInjectionGuard.Validate(request.Prompt);

            // 2. AI Security: PII Masking
            var safePrompt = PiiMaskingHelper.MaskPii(request.Prompt);
            _logger.LogInformation("Gelen prompt maskelendi: {SanitizedPrompt}", safePrompt);

            // 3. Agentic Workflow: Call the Agent Orchestrator
            var result = await _agentService.ResolveAsync(safePrompt, cancellationToken);

            return Ok(new { Response = result });
        }
        catch (SecurityException ex)
        {
            _logger.LogWarning(ex, "Security violation detected in AssistantController.");
            return BadRequest(new { Error = ex.Message });
        }
    }
}
