using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Concurrent;

namespace VoyageFi.Infrastructure.AI
{
    /// <summary>
    /// Faz 3 – Saga Compensating Transaction için Yolcu Oturum Deposu.
    /// 
    /// Her PNR için şunları bellekte tutar:
    ///  - ChatHistory: Konuşma geçmişi (Gemini context-aware yanıtlar verebilsin)
    ///  - ActiveAction: Yolcunun en son onayladığı aksiyon (HOTEL_BOOK, REFUND_INIT vb.)
    /// 
    /// Singleton olarak kayıt edilir. Uygulama süresi boyunca hafızada kalır.
    /// Ölçeklendirmede (multi-pod) Redis'e taşınır.
    /// </summary>
    public class PassengerSessionStore
    {
        public record SessionState(
            ChatHistory History,
            string? ActiveAction     // Son onaylı aksiyon: null = henüz yok
        );

        // ConcurrentDictionary → thread-safe, çok sayıda eş zamanlı bağlantıda güvenli
        private readonly ConcurrentDictionary<string, SessionState> _sessions = new();

        public SessionState GetOrCreate(string pnrCode, string systemPrompt)
        {
            return _sessions.GetOrAdd(pnrCode.ToUpper(), _ =>
                new SessionState(new ChatHistory(systemPrompt), null));
        }

        public void SetActiveAction(string pnrCode, string? action)
        {
            _sessions.AddOrUpdate(
                pnrCode.ToUpper(),
                _ => new SessionState(new ChatHistory(""), action),   // fallback, normalde çağrılmaz
                (_, existing) => existing with { ActiveAction = action }
            );
        }

        public string? GetActiveAction(string pnrCode)
        {
            return _sessions.TryGetValue(pnrCode.ToUpper(), out var s) ? s.ActiveAction : null;
        }

        public void ClearSession(string pnrCode)
        {
            _sessions.TryRemove(pnrCode.ToUpper(), out _);
        }
    }
}
