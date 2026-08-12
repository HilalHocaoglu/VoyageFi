namespace VoyageFi.Core.Interfaces;

/// <summary>
/// AI Brain'in davranış modunu ve sistem bağlamını (System Prompt)
/// Redis üzerinde okuyup yazan servis sözleşmesi.
///
/// FAZ 2 – Adım 2: Admin-Controlled AI Brain
/// </summary>
public interface IAiContextStore
{
    // ─── Davranış Modu ───────────────────────────────────────────────────────
    Task<string> GetBehaviorModeAsync();
    Task SetBehaviorModeAsync(string mode);

    // ─── Sistem Bağlamı (System Prompt / Kurallar) ───────────────────────────
    Task<string> GetSystemContextAsync();
    Task SetSystemContextAsync(string context);

    // ─── AI Yanıt Cache'i ───────────────────────────────────────────────────
    Task<long> GetCachedResponseCountAsync();
    Task ClearAiCacheAsync();
}
