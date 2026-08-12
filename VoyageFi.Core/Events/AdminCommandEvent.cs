namespace VoyageFi.Core.Events;

/// <summary>
/// Admin Dashboard'dan tetiklenen her komutun RabbitMQ üzerinden taşıdığı event.
/// MassTransit bu event'i ilgili Consumer'a (AdminCommandConsumer) yönlendirir.
/// </summary>
public record AdminCommandEvent(
    /// <summary>Her komuta ait idempotency / takip ID'si.</summary>
    Guid CommandId,

    /// <summary>Komutu çalıştıran admin kullanıcısının ID'si (ileride JWT claim'den gelecek).</summary>
    string ActorId,

    /// <summary>
    /// Komut tipi.
    /// Sabit değerler: "SMS_NOTIFY" | "LOUNGE_GRANT" | "AI_NOTIFY"
    /// Adım 3'te (Audit Trail) bu alan log kaydına dönüşecek.
    /// </summary>
    string CommandType,

    /// <summary>Rötarlı uçuş numarası (örn: TK0601).</summary>
    string FlightNumber,

    /// <summary>PNR kodu (örn: VFZWSS).</summary>
    string PnrCode,

    /// <summary>Gecikme süresi (dakika). Consumer'ın bağlamı anlaması için eklendi.</summary>
    int DelayDurationMinutes,

    /// <summary>Komutun oluşturulduğu UTC zamanı.</summary>
    DateTime IssuedAt
);
