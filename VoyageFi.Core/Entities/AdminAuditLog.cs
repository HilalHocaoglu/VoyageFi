using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoyageFi.Core.Entities;

/// <summary>
/// Admin Dashboard üzerinden tetiklenen her komutun değişmez (immutable) kaydı.
/// PostgreSQL'de "admin_audit_log" tablosuna yazılır.
///
/// FAZ 2 – Adım 3: Real-Time OCC Audit Trail
/// </summary>
public class AdminAuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Komutu tetikleyen admin (ör: "admin@voyagefi.com").</summary>
    [Required, MaxLength(200)]
    public string Actor { get; set; } = string.Empty;

    /// <summary>Komut tipi: "SMS_NOTIFY" | "LOUNGE_GRANT" | "AI_NOTIFY"</summary>
    [Required, MaxLength(100)]
    public string Command { get; set; } = string.Empty;

    /// <summary>Komutla birlikte gelen uçuş verisi (FlightNumber, PnrCode, vb.)</summary>
    [Column(TypeName = "jsonb")]
    public string Payload { get; set; } = "{}";

    /// <summary>"SUCCESS" | "FAILED" | "QUEUED"</summary>
    [MaxLength(50)]
    public string Result { get; set; } = "QUEUED";

    /// <summary>Komutun kaç ms'de işlendiği. Consumer tarafından ölçülür.</summary>
    public int DurationMs { get; set; }

    /// <summary>Kaydın oluşturulma zamanı (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
