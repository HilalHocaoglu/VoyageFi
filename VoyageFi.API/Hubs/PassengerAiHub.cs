using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.API.Hubs
{
    public class PassengerAiHub : Hub
    {
        private readonly IPassengerAiService _passengerAiService;
        private readonly ILogger<PassengerAiHub> _logger;

        public PassengerAiHub(IPassengerAiService passengerAiService, ILogger<PassengerAiHub> logger)
        {
            _passengerAiService = passengerAiService;
            _logger = logger;
        }

        /// <summary>
        /// Yolcu bağlandığında frontend bu metodu çağırarak proaktif karşılama alır.
        /// </summary>
        public async Task RequestGreeting(string pnrCode)
        {
            _logger.LogInformation("[HUB] Greeting requested for PNR={Pnr}", pnrCode);
            try
            {
                var greeting = await _passengerAiService.GetProactiveGreetingAsync(pnrCode);
                await Clients.Caller.SendAsync("ReceiveAiResponse", greeting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HUB ERROR] Greeting failed for PNR={Pnr}: {Msg}", pnrCode, ex.Message);
                await Clients.Caller.SendAsync("ReceiveAiResponse",
                    "Merhaba! Sistem şu an yükleniyor, lütfen bir mesaj yazarak başlayın.");
            }
        }

        /// <summary>
        /// Yolcu mesaj gönderdiğinde çağrılır.
        /// Yanıtta <ACTION:*> tag varsa, frontend yakalayıp Saga'yı tetikler.
        /// </summary>
        public async Task SendMessage(string pnrCode, string message)
        {
            _logger.LogInformation("[HUB] Message from PNR={Pnr}: {Msg}", pnrCode, message);
            try
            {
                var response = await _passengerAiService.ProcessPassengerMessageAsync(pnrCode, message);
                await Clients.Caller.SendAsync("ReceiveAiResponse", response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HUB ERROR] Message failed for PNR={Pnr}: {Msg} | Inner: {Inner}",
                    pnrCode, ex.Message, ex.InnerException?.Message ?? "none");
                await Clients.Caller.SendAsync("ReceiveAiResponse",
                    $"[HATA] {ex.Message}");
            }
        }
    }
}
