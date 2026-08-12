using System.Threading.Tasks;

namespace VoyageFi.Core.Interfaces
{
    public interface IPassengerAiService
    {
        /// <summary>
        /// İlk bağlantıda proaktif karşılama mesajı üretir.
        /// Redis'teki bağlam + simüle edilmiş uçuş verisiyle kişiselleştirilir.
        /// </summary>
        Task<string> GetProactiveGreetingAsync(string pnrCode);

        /// <summary>
        /// Yolcunun mesajını işler, ACTION tag'leri ile yapısal yanıt döner.
        /// </summary>
        Task<string> ProcessPassengerMessageAsync(string pnrCode, string userMessage);
    }
}
