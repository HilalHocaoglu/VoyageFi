using Microsoft.AspNetCore.Mvc;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.API.Controllers;

/// <summary>
/// AI Brain konfigürasyonunu (Davranış Modu + Sistem Bağlamı) admin panelinden
/// Redis üzerinden dinamik olarak yönetmek için REST endpoint'leri.
///
/// FAZ 2 – Adım 2: Admin-Controlled AI Brain
/// [Authorize] Adım 2'de kasıtlı olarak kaldırıldı; Adım 3+ ile geri eklenecek.
/// </summary>
[ApiController]
[Route("api/admin/ai")]
public class AdminAiController : ControllerBase
{
    private readonly IAiContextStore _aiContext;
    private readonly ILogger<AdminAiController> _logger;

    public AdminAiController(IAiContextStore aiContext, ILogger<AdminAiController> logger)
    {
        _aiContext = aiContext;
        _logger    = logger;
    }

    // ─── GET: Mevcut AI konfigürasyonunu oku ─────────────────────────────────

    /// <summary>
    /// GET /api/admin/ai/context
    /// Mevcut davranış modu, sistem bağlamı ve cache istatistiklerini döner.
    /// Frontend modal'ı açıldığında bunu çağırarak Redis'ten güncel değerleri yükler.
    /// </summary>
    [HttpGet("context")]
    public async Task<IActionResult> GetContextAsync()
    {
        var mode    = await _aiContext.GetBehaviorModeAsync();
        var context = await _aiContext.GetSystemContextAsync();
        var cacheCount = await _aiContext.GetCachedResponseCountAsync();

        return Ok(new
        {
            BehaviorMode  = mode,
            SystemContext = context,
            CachedResponseCount = cacheCount,
        });
    }

    // ─── POST: Davranış modunu güncelle ──────────────────────────────────────

    /// <summary>
    /// POST /api/admin/ai/behavior-mode
    /// Body: { "mode": "Standard" | "Aggressive" | "Minimal" }
    /// </summary>
    [HttpPost("behavior-mode")]
    public async Task<IActionResult> SetBehaviorModeAsync([FromBody] SetModeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Mode))
            return BadRequest(new { Error = "Mode alanı boş olamaz." });

        try
        {
            await _aiContext.SetBehaviorModeAsync(request.Mode);
            _logger.LogInformation("🤖 [AIBrain] Davranış modu güncellendi: {Mode}", request.Mode);
            return Ok(new { Message = $"AI davranış modu '{request.Mode}' olarak güncellendi.", Mode = request.Mode });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    // ─── POST: Sistem bağlamını (System Prompt) güncelle ─────────────────────

    /// <summary>
    /// POST /api/admin/ai/system-context
    /// Body: { "context": "SHY-YOLCU kuralları..." }
    /// </summary>
    [HttpPost("system-context")]
    public async Task<IActionResult> SetSystemContextAsync([FromBody] SetContextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Context))
            return BadRequest(new { Error = "Sistem bağlamı boş olamaz." });

        try
        {
            await _aiContext.SetSystemContextAsync(request.Context);
            _logger.LogInformation("🧠 [AIBrain] Sistem bağlamı güncellendi. Uzunluk: {Length} karakter", request.Context.Length);
            return Ok(new { Message = "Sistem bağlamı Redis'e kaydedildi.", CharCount = request.Context.Length });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    // ─── POST: AI cache'ini temizle ───────────────────────────────────────────

    /// <summary>
    /// POST /api/admin/ai/clear-cache
    /// Sistem bağlamı değiştiğinde eski yanıtlar geçersiz olabilir.
    /// </summary>
    [HttpPost("clear-cache")]
    public async Task<IActionResult> ClearCacheAsync()
    {
        await _aiContext.ClearAiCacheAsync();
        _logger.LogInformation("🗑️ [AIBrain] AI yanıt cache'i temizlendi.");
        return Ok(new { Message = "AI yanıt cache'i temizlendi." });
    }

    // ─── Request modelleri ────────────────────────────────────────────────────
    public sealed class SetModeRequest    { public string Mode    { get; set; } = string.Empty; }
    public sealed class SetContextRequest { public string Context { get; set; } = string.Empty; }
}
