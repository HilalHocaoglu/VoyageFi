"""
VoyageFi RAG Ingestion Pipeline
================================
Bu script, dışarıdan alınan banka kampanyaları ve SHGM yolcu hakları
metinlerini OpenAI embedding modeli kullanarak vektöre çevirir ve
PostgreSQL pgvector tablosuna (Campaigns) yükler.

Gereksinimler (requirements.txt içeriği):
  openai>=1.0.0
  psycopg2-binary>=2.9.0
  pgvector>=0.2.0
  python-dotenv>=1.0.0

Kurulum:
  pip install openai psycopg2-binary pgvector python-dotenv

Çalıştırma:
  python scripts/ingest_data.py

Ortam Değişkenleri (.env dosyası veya shell):
  DATABASE_URL=postgresql://user:pass@localhost:5432/voyagefi
  OPENAI_API_KEY=sk-...
"""

import os
import json
import uuid
import logging
from datetime import datetime, timezone
from dataclasses import dataclass
from typing import Optional

import psycopg2
from psycopg2.extras import execute_values
from pgvector.psycopg2 import register_vector
from openai import OpenAI
from dotenv import load_dotenv

# ---------------------------------------------------------------------------
# Konfigürasyon
# ---------------------------------------------------------------------------
load_dotenv()

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
logger = logging.getLogger(__name__)

# OpenAI: text-embedding-3-small → 1536 boyut, düşük maliyet, yüksek kalite
EMBEDDING_MODEL = "text-embedding-3-small"
EMBEDDING_DIMENSIONS = 1536

DATABASE_URL = os.getenv("DATABASE_URL", "postgresql://localhost/voyagefi")
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

# ---------------------------------------------------------------------------
# Veri Modeli
# ---------------------------------------------------------------------------
@dataclass
class RawDocument:
    """Kaynaktan okunan ham belge."""
    title: str
    content: str
    source_type: str  # "BankCampaign" | "PassengerRight" | "AirlinePolicy"


@dataclass
class EnrichedDocument:
    """Embedding hesaplandıktan sonra oluşan zenginleştirilmiş belge."""
    id: str
    title: str
    content: str
    source_type: str
    embedding: list[float]
    created_at: datetime

# ---------------------------------------------------------------------------
# Veri Kaynakları — gerçek kullanımda bu veriler JSON dosyasından,
# API'den veya web scraper'dan gelir.
# ---------------------------------------------------------------------------
SAMPLE_DOCUMENTS: list[dict] = [
    {
        "title": "Garanti BBVA Bonus Kart — THY'de %20 Nakit İadesi",
        "content": (
            "Garanti BBVA Bonus Kart sahipleri, Temmuz–Ağustos 2024 tarihleri arasında "
            "thy.com üzerinden yapacakları uçak bileti alımlarında %20 nakit iade kazanır. "
            "Kampanya yurt içi ve yurt dışı uçuşlarda geçerlidir. Minimum harcama tutarı "
            "500 TL, maksimum iade tutarı 2.000 TL'dir. Yılda 3 kez kullanılabilir."
        ),
        "source_type": "BankCampaign",
    },
    {
        "title": "İş Bankası Maximum Kart — 3 Taksit Avantajı",
        "content": (
            "İş Bankası Maximum Kart ile Pegasus ve SunExpress biletlerinde "
            "3 taksit imkânı sunulmaktadır. Kampanya 31 Aralık 2024 tarihine kadar geçerlidir. "
            "Tek seferlik alımlarda ek %5 indirim uygulanmaktadır. "
            "Taksitli alımlarda kart faizi alınmamaktadır."
        ),
        "source_type": "BankCampaign",
    },
    {
        "title": "SHGM Yolcu Hakları — Rötar Durumunda Tazminat",
        "content": (
            "Sivil Havacılık Genel Müdürlüğü (SHGM) yönetmeliği kapsamında; "
            "2 saatten fazla rötar yapan iç hat uçuşlarında yolcular yemek ve içecek hakkına, "
            "5 saatten fazla rötarda ise ücret iadesi talep etme hakkına sahiptir. "
            "Dış hat uçuşlarında 250 km'ye kadar 250€, 1500 km'ye kadar 400€, "
            "1500 km üzeri için 600€ tazminat talep edilebilir. "
            "Hava koşulları ve olağandışı durumlar bu hakkın istisnasını oluşturur."
        ),
        "source_type": "PassengerRight",
    },
    {
        "title": "SHGM Yolcu Hakları — İptal Durumunda Yolcunun Seçenekleri",
        "content": (
            "Uçuş iptali durumunda hava yolu şirketi yolculara şu seçenekleri sunmak zorundadır: "
            "1) Bilet ücretinin tamamının iadesi, "
            "2) En yakın alternatif uçuşa ücretsiz aktarım, "
            "3) Uçuş 3 günden fazla erteleniyorsa ücretsiz konaklama ve ulaşım. "
            "İptal bildirimi 2 haftadan kısa sürede yapılırsa ek tazminat hakkı doğar."
        ),
        "source_type": "PassengerRight",
    },
    {
        "title": "Pegasus Yolcu Hakları Politikası — Bagaj Kayıp Prosedürü",
        "content": (
            "Pegasus Airlines, kayıp bagaj durumlarında yolculara 21 gün içinde "
            "başvuru yapma zorunluluğu bulunduğunu belirtir. "
            "Kayıp bagaj için Pegasus'un sorumluluk sınırı Montréal Konvansiyonu kapsamında "
            "1.288 SDR (yaklaşık 1.600 USD) ile sınırlıdır. "
            "Değerli eşyalar için ek beyan ücreti ödenerek sigorta kapsamı artırılabilir."
        ),
        "source_type": "AirlinePolicy",
    },
]


# ---------------------------------------------------------------------------
# Embedding Üretimi
# ---------------------------------------------------------------------------
def create_embeddings(client: OpenAI, texts: list[str]) -> list[list[float]]:
    """
    OpenAI API'ye toplu istek atarak metinlerin embedding'lerini döndürür.
    Toplu işlem, API çağrı sayısını ve maliyeti önemli ölçüde azaltır.
    """
    logger.info(f"{len(texts)} metin için embedding üretiliyor...")

    response = client.embeddings.create(
        input=texts,
        model=EMBEDDING_MODEL,
        dimensions=EMBEDDING_DIMENSIONS,
    )

    embeddings = [item.embedding for item in response.data]
    logger.info(f"Embedding boyutu: {len(embeddings[0])} (beklenen: {EMBEDDING_DIMENSIONS})")
    return embeddings


# ---------------------------------------------------------------------------
# Veri Dönüşümü (Transform)
# ---------------------------------------------------------------------------
def enrich_documents(
    raw_docs: list[RawDocument],
    embeddings: list[list[float]],
) -> list[EnrichedDocument]:
    """Ham belgelere embedding'leri ekleyerek veritabanına yazmaya hazır hale getirir."""
    now = datetime.now(timezone.utc)
    return [
        EnrichedDocument(
            id=str(uuid.uuid4()),
            title=doc.title,
            content=doc.content,
            source_type=doc.source_type,
            embedding=emb,
            created_at=now,
        )
        for doc, emb in zip(raw_docs, embeddings)
    ]


# ---------------------------------------------------------------------------
# Veritabanı Yükleme (Load)
# ---------------------------------------------------------------------------
def load_to_database(docs: list[EnrichedDocument], db_url: str) -> None:
    """
    Zenginleştirilmiş belgeleri PostgreSQL Campaigns tablosuna yükler.
    Var olan kayıtları atlamak için ON CONFLICT DO NOTHING kullanılır.
    """
    conn = psycopg2.connect(db_url)
    register_vector(conn)  # pgvector tipini psycopg2'ye tanıtır

    try:
        with conn.cursor() as cur:
            rows = [
                (
                    doc.id,
                    doc.title,
                    doc.content,
                    doc.source_type,
                    doc.embedding,
                    doc.created_at,
                )
                for doc in docs
            ]

            execute_values(
                cur,
                """
                INSERT INTO "Campaigns" ("Id", "Title", "Content", "SourceType", "Embedding", "CreatedAt")
                VALUES %s
                ON CONFLICT ("Id") DO NOTHING
                """,
                rows,
            )

        conn.commit()
        logger.info(f"{len(docs)} belge başarıyla Campaigns tablosuna yüklendi.")

    except Exception as e:
        conn.rollback()
        logger.error(f"Veritabanı hatası: {e}")
        raise
    finally:
        conn.close()


# ---------------------------------------------------------------------------
# Yardımcı: JSON dosyasından belge okuma
# ---------------------------------------------------------------------------
def load_documents_from_json(file_path: str) -> list[RawDocument]:
    """
    JSON formatındaki belge dosyasını okur.
    Beklenen format:
    [
      {"title": "...", "content": "...", "source_type": "BankCampaign"},
      ...
    ]
    """
    with open(file_path, encoding="utf-8") as f:
        data = json.load(f)
    return [RawDocument(**item) for item in data]


# ---------------------------------------------------------------------------
# Ana Pipeline
# ---------------------------------------------------------------------------
def run_ingestion_pipeline(documents: Optional[list[RawDocument]] = None) -> None:
    """
    ETL Pipeline:
    1. Extract  → Belgeler yüklenir (parametre veya SAMPLE_DOCUMENTS)
    2. Transform → OpenAI ile embedding'e çevrilir
    3. Load      → pgvector tablosuna yazılır
    """
    if not OPENAI_API_KEY:
        raise EnvironmentError(
            "OPENAI_API_KEY ortam değişkeni tanımlı değil. "
            ".env dosyasına ekleyin veya shell'den set edin."
        )

    # 1. EXTRACT
    raw_docs = documents or [RawDocument(**d) for d in SAMPLE_DOCUMENTS]
    logger.info(f"Pipeline başlatıldı. Toplam {len(raw_docs)} belge işlenecek.")

    # 2. TRANSFORM
    client = OpenAI(api_key=OPENAI_API_KEY)
    texts = [f"{doc.title}\n\n{doc.content}" for doc in raw_docs]
    embeddings = create_embeddings(client, texts)

    enriched_docs = enrich_documents(raw_docs, embeddings)

    # 3. LOAD
    load_to_database(enriched_docs, DATABASE_URL)

    logger.info("Pipeline başarıyla tamamlandı ✓")


# ---------------------------------------------------------------------------
# Giriş Noktası
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    import sys

    # Opsiyonel: Komut satırından JSON dosyası belirtilebilir
    # Örnek: python scripts/ingest_data.py scripts/campaigns.json
    if len(sys.argv) > 1:
        json_path = sys.argv[1]
        logger.info(f"JSON kaynağı kullanılıyor: {json_path}")
        docs = load_documents_from_json(json_path)
        run_ingestion_pipeline(docs)
    else:
        logger.info("Kaynak belirtilmedi — örnek veri (SAMPLE_DOCUMENTS) kullanılıyor.")
        run_ingestion_pipeline()
