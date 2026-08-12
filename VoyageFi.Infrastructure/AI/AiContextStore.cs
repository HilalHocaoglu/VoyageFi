using Microsoft.Extensions.Caching.Distributed;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.Infrastructure.AI;

/// <summary>
/// AI Brain konfigürasyonunu Redis'e kalıcı olarak yazar ve okur.
///
/// Redis Key Şeması:
///   ai:behavior_mode   → "Standard" | "Aggressive" | "Minimal"
///   ai:system_context  → Admin tarafından düzenlenebilen sistem kuralları (System Prompt)
///   ai_prompt:*        → VoyageFiAgentService tarafından yazılan yanıt cache'leri
///
/// FAZ 2 – Adım 2: Admin-Controlled AI Brain
/// </summary>
public class AiContextStore : IAiContextStore
{
    private readonly IDistributedCache _cache;

    // Redis key sabitleri — magic string'den kaçınmak için
    private const string BehaviorModeKey  = "ai:behavior_mode";
    private const string SystemContextKey = "ai:system_context";
    private const string AiCachePrefix    = "ai_prompt:";

    // Kalıcı TTL (Redis'te süresiz yazmak için IDistributedCache 100 yıl kullanır)
    private static readonly DistributedCacheEntryOptions PersistentOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(36500) // ~100 yıl
    };

    private const string DefaultBehaviorMode = "Standard";
    private const string DefaultSystemContext =
        "SHY-YOLCU Kuralları (SHGM Yönetmeliği):\n" +
        "- İç hat uçuşlarında 2 saati aşan rötarlarda yolcuya sıcak yemek ve içecek ikramı zorunludur.\n" +
        "- 5 saati aşan rötarlarda yolcunun iptali seçme ve tam iade hakkı vardır.\n\n" +
        "Banka Kampanyaları:\n" +
        "- Garanti BBVA Kredi Kartı: Uçuş 2+ saat rötar yaparsa anlaşmalı Lounge'dan ücretsiz yararlanılır.\n" +
        "- Yapı Kredi Platinum: Rötar tazminat başvurusu için özel danışman hattı aktif.";

    public AiContextStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    // ─── Davranış Modu ────────────────────────────────────────────────────────

    public async Task<string> GetBehaviorModeAsync()
    {
        return await _cache.GetStringAsync(BehaviorModeKey) ?? DefaultBehaviorMode;
    }

    public async Task SetBehaviorModeAsync(string mode)
    {
        var validModes = new[] { "Standard", "Aggressive", "Minimal" };
        if (!validModes.Contains(mode))
            throw new ArgumentException($"Geçersiz davranış modu: '{mode}'. Geçerli değerler: {string.Join(", ", validModes)}");

        await _cache.SetStringAsync(BehaviorModeKey, mode, PersistentOptions);
    }

    // ─── Sistem Bağlamı ────────────────────────────────────────────────────────

    public async Task<string> GetSystemContextAsync()
    {
        return await _cache.GetStringAsync(SystemContextKey) ?? DefaultSystemContext;
    }

    public async Task SetSystemContextAsync(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
            throw new ArgumentException("Sistem bağlamı boş olamaz.");

        await _cache.SetStringAsync(SystemContextKey, context, PersistentOptions);
    }

    // ─── AI Cache Yönetimi ────────────────────────────────────────────────────

    /// <summary>
    /// NOT: IDistributedCache, key pattern ile sayma desteklemez.
    /// Gerçek üretimde StackExchange.Redis IDatabase ile SCAN kullanılır.
    /// Şimdilik -1 döndürüyoruz (Adım 4'te geliştirilecek).
    /// </summary>
    public Task<long> GetCachedResponseCountAsync()
    {
        return Task.FromResult(-1L); // Placeholder — Adım 4'te IDatabase.ScriptEvaluateAsync ile sayılacak
    }

    /// <summary>
    /// AI prompt cache'lerini temizler.
    /// IDistributedCache ile doğrudan key pattern silme mümkün değil;
    /// Bu metodun tam implementasyonu Adım 4'te IDatabase üzerinden yapılacak.
    /// </summary>
    public Task ClearAiCacheAsync()
    {
        // Adım 4: await _db.ExecuteAsync("SCAN 0 MATCH ai_prompt:* COUNT 1000") → DEL
        return Task.CompletedTask;
    }
}
