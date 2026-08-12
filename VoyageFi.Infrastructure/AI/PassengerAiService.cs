using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.Infrastructure.AI
{
    /// <summary>
    /// Faz 3 – Adım 1: Autonomous AI Concierge (Stateful + Compensating Transactions)
    ///
    /// Yenilikler:
    ///  - PassengerSessionStore (Singleton) üzerinden her PNR'a ait ChatHistory saklanır.
    ///  - Gemini her çağrıda tam konuşma geçmişini görür → gerçek bağlam farkındalığı.
    ///  - Yolcu fikir değiştirirse (otel → iade) asistan önce telafi sorar.
    ///  - Yeni ACTION tag'leri: CANCEL_HOTEL, CANCEL_LOUNGE, CANCEL_REBOOK.
    /// </summary>
    public class PassengerAiService : IPassengerAiService
    {
        private readonly Kernel _kernel;
        private readonly IAiContextStore _contextStore;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly PassengerSessionStore _sessionStore;
        private readonly ILogger<PassengerAiService> _logger;

        // Mock uçuş veri havuzu — Saga entegrasyonunda gerçek DB/Redis'e taşınır
        private static readonly Dictionary<string, (string Flight, int DelayMin)> MockFlights = new()
        {
            ["VF34S2"] = ("TK2024 İstanbul→Londra", 180),
            ["VFM5UQ"] = ("TK1234 Ankara→İzmir",    75),
            ["VFVTRL"] = ("TK5678 İstanbul→Dubai",  240),
        };

        // Aksiyon → Türkçe ad eşlemesi (telafi mesajları için)
        private static readonly Dictionary<string, string> ActionLabels = new()
        {
            ["HOTEL_BOOK"]    = "otel konaklaması",
            ["LOUNGE_ACCESS"] = "lounge erişimi",
            ["REFUND_INIT"]   = "para iadesi",
            ["REBOOK_FLIGHT"] = "yeniden rezervasyon",
        };

        public PassengerAiService(
            Kernel kernel,
            IAiContextStore contextStore,
            PassengerSessionStore sessionStore,
            ILogger<PassengerAiService> logger)
        {
            _kernel = kernel;
            _contextStore = contextStore;
            _sessionStore = sessionStore;
            _logger = logger;
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        }

        // ──────────────────────────────────────────────────────────────────────
        // 1. Proaktif Karşılama — oturum başlatır, konuşma geçmişi oluşturur
        // ──────────────────────────────────────────────────────────────────────

        public async Task<string> GetProactiveGreetingAsync(string pnrCode)
        {
            _logger.LogInformation("[AI] Proactive greeting for PNR={Pnr}", pnrCode);

            var (flightInfo, delayMin) = GetFlightContext(pnrCode);
            var behaviorMode  = await _contextStore.GetBehaviorModeAsync();
            var systemContext = await _contextStore.GetSystemContextAsync();
            var prompt        = BuildSystemPrompt(pnrCode, flightInfo, delayMin, behaviorMode, systemContext);

            // Yeni oturum başlat (önceki varsa temizle)
            _sessionStore.ClearSession(pnrCode);
            var session = _sessionStore.GetOrCreate(pnrCode, prompt);

            session.History.AddUserMessage(
                "[SYSTEM: Passenger just connected. Write a proactive, warm, SHORT greeting. " +
                "State the delay, list compensation options, ask which they prefer. Turkish only.]");

            var reply = await CallGeminiAsync(session.History, pnrCode);
            session.History.AddAssistantMessage(reply);

            return reply;
        }

        // ──────────────────────────────────────────────────────────────────────
        // 2. Mesaj İşleme — geçmiş bağlam + compensating transaction mantığı
        // ──────────────────────────────────────────────────────────────────────

        public async Task<string> ProcessPassengerMessageAsync(string pnrCode, string userMessage)
        {
            _logger.LogInformation("[AI] Message PNR={Pnr}: {Msg}", pnrCode, userMessage);

            var (flightInfo, delayMin) = GetFlightContext(pnrCode);
            var behaviorMode  = await _contextStore.GetBehaviorModeAsync();
            var systemContext = await _contextStore.GetSystemContextAsync();
            var prompt        = BuildSystemPrompt(pnrCode, flightInfo, delayMin, behaviorMode, systemContext);

            // Mevcut oturumu al (yoksa yeni başlat — güvenlik önlemi)
            var session = _sessionStore.GetOrCreate(pnrCode, prompt);

            // ── Telafi Kontrolü ────────────────────────────────────────────────
            var activeAction = _sessionStore.GetActiveAction(pnrCode);
            var incomingAction = DetectIntendedAction(userMessage);

            if (activeAction is not null
                && incomingAction is not null
                && activeAction != incomingAction)
            {
                // Yolcu fikir değiştirdi! Önce önceki aksiyonu telafi etmemiz gerekiyor.
                var activeLabel   = ActionLabels.GetValueOrDefault(activeAction,  activeAction);
                var incomingLabel = ActionLabels.GetValueOrDefault(incomingAction, incomingAction);

                var compensationNote = $"[SYSTEM: The passenger already has '{activeAction}' active. " +
                    $"They are now requesting '{incomingAction}'. You MUST ask for confirmation to cancel the previous action first. " +
                    $"Say: 'Daha önce {activeLabel} talep etmiştiniz. {incomingLabel} sürecini başlatabilmem için önce {activeLabel}nızı iptal etmem gerekiyor. Onaylıyor musunuz?' " +
                    $"Then append <ACTION:CANCEL_{activeAction}> at the end of your reply if they confirm cancellation.]";

                session.History.AddUserMessage(compensationNote);
                var compensateReply = await CallGeminiAsync(session.History, pnrCode);
                session.History.AddAssistantMessage(compensateReply);

                // Telafi onaylandıysa aksiyonu sıfırla
                if (IsConfirmation(userMessage))
                {
                    _sessionStore.SetActiveAction(pnrCode, null);
                }

                return InjectCompensationTag(compensateReply, activeAction);
            }

            // ── Normal Mesaj Akışı ─────────────────────────────────────────────
            session.History.AddUserMessage(userMessage);
            var raw = await CallGeminiAsync(session.History, pnrCode);
            session.History.AddAssistantMessage(raw);

            // Yeni aksiyonu kaydet (varsa)
            var finalAction = ExtractActionFromResponse(raw) ?? InjectActionTag(raw, userMessage);
            var detectedTag = ExtractActionTag(finalAction);
            if (detectedTag is not null)
            {
                _sessionStore.SetActiveAction(pnrCode, detectedTag);
                _logger.LogInformation("[AI] Active action set: {Action} for PNR={Pnr}", detectedTag, pnrCode);
            }

            return finalAction;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Yardımcı Metodlar
        // ──────────────────────────────────────────────────────────────────────

        private static (string FlightInfo, int DelayMin) GetFlightContext(string pnrCode)
        {
            if (MockFlights.TryGetValue(pnrCode.ToUpper(), out var data)) return data;
            return ($"Uçuşunuz ({pnrCode.ToUpper()})", 120);
        }

        private static string BuildSystemPrompt(
            string pnrCode, string flightInfo, int delayMin,
            string behaviorMode, string systemContext)
        {
            var tone = behaviorMode switch
            {
                "Aggressive" => "Yüksek tazminat tekliflerini ön plana çıkar, anında çözüm vaat et.",
                "Minimal"    => "Sadece zorunlu yasal hakları belirt.",
                _            => "Dengeli, empatik ol; seçenekleri net sun."
            };

            return $"""
You are 'VoyageFi Concierge', an elite AI assistant for a premium airline.
Always respond in Turkish. Be concise (max 3 short paragraphs), warm, and action-oriented.

=== ADMIN DIRECTIVES ===
Behavior Mode : {behaviorMode}
Tone          : {tone}
Rules:
{systemContext}

=== FLIGHT CONTEXT ===
PNR    : {pnrCode.ToUpper()}
Flight : {flightInfo}
Delay  : {delayMin} min ({(delayMin >= 120 ? "KRİTİK – otel hakkı var" : delayMin >= 60 ? "YÜKSEK" : "ORTA")})

=== AVAILABLE COMPENSATION ===
{(delayMin >= 120 ? "✅ Otel Konaklaması (1 gece, havayolu ödemeli)\n" : "")}✅ Lounge Erişimi (ücretsiz)
✅ Tam İade + %20 bonus voucher
✅ Ücretsiz Yeniden Rezervasyon

=== ACTION TAG RULES (CRITICAL) ===
When passenger clearly ACCEPTS an option, append ONE tag on a NEW LINE at the end of your reply:
  <ACTION:HOTEL_BOOK>     – otel kabul
  <ACTION:LOUNGE_ACCESS>  – lounge kabul
  <ACTION:REFUND_INIT>    – iade kabul
  <ACTION:REBOOK_FLIGHT>  – yeniden rezervasyon kabul
  <ACTION:CANCEL_HOTEL>   – otel iptali onaylandı
  <ACTION:CANCEL_LOUNGE>  – lounge iptali onaylandı
  <ACTION:CANCEL_REFUND>  – iade iptali onaylandı
  <ACTION:CANCEL_REBOOK>  – rezervasyon iptali onaylandı
NEVER explain the tag. Only append when acceptance/cancellation is unambiguous.
""";
        }

        /// <summary>Kullanıcı mesajından hangi aksiyonu istediğini tespit eder.</summary>
        private static string? DetectIntendedAction(string message)
        {
            var lower = message.ToLowerInvariant();
            if (lower.Contains("otel") && IsAcceptance(lower))        return "HOTEL_BOOK";
            if (lower.Contains("iade") || lower.Contains("para"))      return "REFUND_INIT";
            if (lower.Contains("lounge") || lower.Contains("salon"))   return "LOUNGE_ACCESS";
            if (lower.Contains("başka uçuş") || lower.Contains("değiştir")) return "REBOOK_FLIGHT";
            return null;
        }

        private static bool IsAcceptance(string lower) =>
            lower.Contains("kabul") || lower.Contains("evet") || lower.Contains("tamam") || lower.Contains("istiyorum") || lower.Contains("olur");

        private static bool IsConfirmation(string message)
        {
            var lower = message.ToLowerInvariant();
            return lower.Contains("evet") || lower.Contains("onaylıyorum") || lower.Contains("tamam") || lower.Contains("olur");
        }

        /// <summary>Gemini ACTION tag koymadıysa keyword matching ile ekle.</summary>
        private static string InjectActionTag(string aiResponse, string userMessage)
        {
            if (aiResponse.Contains("<ACTION:")) return aiResponse;
            var lower = userMessage.ToLowerInvariant();
            string? tag = DetectIntendedAction(lower) is { } action ? $"<ACTION:{action}>" : null;
            return tag is null ? aiResponse : $"{aiResponse}\n{tag}";
        }

        private static string InjectCompensationTag(string aiResponse, string cancelledAction)
        {
            if (aiResponse.Contains("<ACTION:CANCEL_")) return aiResponse;
            return $"{aiResponse}\n<ACTION:CANCEL_{cancelledAction}>";
        }

        private static string? ExtractActionFromResponse(string raw)
        {
            return raw.Contains("<ACTION:") ? raw : null;
        }

        private static string? ExtractActionTag(string response)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                response, @"<ACTION:([A-Z_]+)>");
            return match.Success ? match.Groups[1].Value : null;
        }

        private async Task<string> CallGeminiAsync(ChatHistory history, string pnrCode)
        {
            try
            {
                var response = await _chatCompletionService.GetChatMessageContentAsync(history, kernel: _kernel);
                _logger.LogInformation("[AI] Gemini OK for PNR={Pnr}", pnrCode);
                return response.Content ?? "Üzgünüm, şu anda yanıt oluşturulamadı.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AI ERROR] Gemini failed for PNR={Pnr}: {Msg}", pnrCode, ex.Message);
                throw;
            }
        }
    }
}
