namespace VoyageFi.Core.Entities;

/// <summary>
/// Banka kampanyalarını ve SHGM yolcu haklarını hem metin hem de
/// vektör (embedding) olarak depolayan entity. RAG pipeline'ının
/// kalbidir; AI, kullanıcı sorularını bu tablodaki embedding'lerle
/// karşılaştırarak en alakalı bilgiyi bulur.
/// </summary>
public class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Kampanya veya hakkın insan-okunabilir başlığı. Örn: "THY Miles&Smiles %20 İndirim"</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Kampanya metninin tamamı. LLM'e context olarak gönderilir.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Kaynağın türü. Filtreli RAG aramaları için kullanılır.
    /// Örn: "BankCampaign", "PassengerRight", "AirlinePolicy"
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI text-embedding-3-small modeli ile üretilmiş 1536 boyutlu vektör.
    /// EF Core entity'sinde string olarak tutulur; migration sonrasında
    /// manuel SQL ile vector(1536) tipine dönüştürülür.
    /// Python ingestion scripti direkt olarak pgvector formatında yazar.
    /// </summary>
    public string? EmbeddingRaw { get; set; }

    /// <summary>Verinin sisteme eklendiği UTC tarih.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
