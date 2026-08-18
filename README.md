# 🚀 İK Karar Destek Sistemi (HR Decision Support System)

Bu proje, İnsan Kaynakları süreçlerini dijitalleştirmek, aday eşleştirmelerini yapay zeka destekli anlamsal (semantik) arama ile optimize etmek ve mevcut çalışanların şirkette kalıcılık (retention) sürelerini tahmin etmek amacıyla geliştirilmiş kapsamlı bir kurumsal web uygulamasıdır. 

Sistem, .NET Core Clean Architecture (Temiz Mimari) backend yapısı ile çoklu Python ML (Makine Öğrenmesi) mikroservislerinin entegrasyonuyla kurgulanmıştır.

---

## 📂 Proje Klasör Yapısı

*   **`src/` & `tests/`:** Projenin ana omurgası. ASP.NET Core tabanlı Backend, Web arayüzü ve Birim Testlerini içerir.
*   **`ml/`:** Semantik eşleştirme işlemlerini yapan doğal dil işleme (NLP) servisleridir. Qwen Embedding (`qwen_embedding_service`) ve Reranker (`qwen_reranker_service`) modellerini barındırır.
*   **`ML2/`:** Çalışan kalıcılık/risk tahmini (Retention) yapan bağımsız makine öğrenmesi servisidir. FastAPI üzerinden hizmet verir (`ml_api.py`, `train_model.py`).
*   **`database/`:** Projenin veritabanı şemaları, yedekleri veya yapılandırma dosyalarını içerir.

---

## 🏗️ Sistem Mimarisi ve Kullanılan Teknolojiler

*   **Backend & Arayüz:** C# 12, ASP.NET Core MVC, Entity Framework Core, PostgreSQL.
*   **NLP ve Semantik Arama (AI):** Python, Qwen Modelleri (Özgeçmişler ve iş ilanları arasındaki anlamsal bağı kurar).
*   **Tahmin Modeli (AI):** Python, Scikit-Learn (Random Forest vb.), FastAPI, Uvicorn (Personel verilerinden yola çıkarak şirkette kalma riskini hesaplar).

### ⚙️ İş Akışı
1. Kullanıcı arayüzden bir işlem başlattığında (Örn: Aday Eşleştirme veya Çalışan Detayı görüntüleme), C# (Web/Application) katmanındaki servisler tetiklenir.
2. İşlem özgeçmiş eşleştirme ise; sistem `ml` klasöründeki Qwen servislerine HTTP isteği atarak metinlerin anlamsal vektörlerini (embedding) ve sıralamalarını (rerank) alır.
3. İşlem çalışan riski tahmini ise; sistem çalışanın geçmiş deneyim sürelerini PostgreSQL veritabanından çeker ve `ML2` klasöründe çalışan FastAPI servisine iletir.
4. Python servislerinden dönen yapay zeka analiz sonuçları, kullanıcının ekranında dinamik raporlar ve rozetler olarak gösterilir.

---

## 🚀 Projeyi Bilgisayarda Başlatma (Kurulum Rehberi)

Projeyi tam kapasiteyle (Yapay zeka servisleri dahil) yerel ortamınızda ayağa kaldırmak için aşağıdaki adımları sırasıyla izleyin.

### 1. Gereksinimler
*   .NET 8.0+ SDK
*   Python 3.9+
*   PostgreSQL Sunucusu
*   PostgreSQL sunucu kurulumuyla uyumlu pgvector eklentisi

### 2. Veritabanı Kurulumu
PostgreSQL zorunludur. Aday ve iş talebi embedding'leri `vector(1024)` olarak saklandığı için pgvector da gereklidir. Uygulama pgvector'ı ANN/vektör araması için değil, yalnızca mevcut embedding vektörlerini kalıcı olarak saklamak için kullanır. EF migration `vector` eklentisini veritabanında etkinleştirir; ancak önce pgvector sunucu dosyalarının PostgreSQL kurulumunda bulunması gerekir.

Kurulumdan önce pgvector'ın sunucuda kullanılabilir olup olmadığını kontrol edin:

```sql
SELECT name, default_version, installed_version
FROM pg_available_extensions
WHERE name = 'vector';
```

Sonuç dönmüyorsa pgvector PostgreSQL sunucusuna kurulmamıştır. Satır dönüyor ancak `installed_version` değeri `NULL` ise eklenti sunucuda kullanılabilir, fakat ilgili veritabanında henüz etkin değildir; projenin EF migration'ı eklentiyi etkinleştirir.

#### Windows ve PostgreSQL 18

Doğrulanan yerel yapılandırma PostgreSQL 18.x x64 ve pgvector 0.8.6'dır. Windows'ta Visual Studio C++ x64 build tools, Windows SDK, Git ve PostgreSQL 18 kurulu olmalıdır. x64 Visual Studio geliştirici ortamında resmi pgvector derleme yöntemi:

```bat
set "PGROOT=C:\Program Files\PostgreSQL\18"

git clone --branch v0.8.6 --depth 1 https://github.com/pgvector/pgvector.git
cd pgvector

nmake /F Makefile.win
nmake /F Makefile.win install
```

`nmake install` adımı için yönetici yetkisi gerekebilir.

Yeni bir ortamda veritabanını şu sırayla hazırlayın:

1. PostgreSQL'i kurun ve sunucuyu başlatın.
2. pgvector'ı aynı PostgreSQL sunucu kurulumuna yükleyin.
3. Mevcut veritabanı yedeğini kullanacaksanız `database/database_dump.backup` dosyasını geri yükleyin.
4. `ConnectionStrings:PostgreSql` bağlantı dizesini Web projesinin user-secrets desteği veya mevcut yapılandırma mekanizmasıyla tanımlayın.
5. Depo kökünde EF migration'larını uygulayın:

```powershell
dotnet ef database update `
  --project ".\src\HrDecisionSupport.Infrastructure\HrDecisionSupport.Infrastructure.csproj" `
  --startup-project ".\src\HrDecisionSupport.Web\HrDecisionSupport.Web.csproj" `
  --context HrDecisionSupportDbContext
```

Migration sonrasında eklentiyi ve isteğe bağlı olarak embedding tablolarını doğrulayın:

```sql
SELECT extname, extversion
FROM pg_extension
WHERE extname = 'vector';

SELECT to_regclass('public.candidate_embeddings'),
       to_regclass('public.job_requisition_embeddings');
```

### 3. NLP Servislerini Başlatma (`ml` Klasörü)
Aday eşleştirme ve semantik arama özelliklerinin çalışması için bu servislerin aktif olması gerekir.
1. Terminalde `ml/qwen_embedding_service` klasörüne gidin, kütüphaneleri yükleyin (`pip install -r requirements.txt`) ve servisi ayağa kaldırın.
2. Aynı işlemi `ml/qwen_reranker_service` klasörü için de tekrarlayın.

### 4. Tahmin Servisini Başlatma (`ML2` Klasörü)
Çalışanların kalıcılık (retention) risk tahminlerinin çalışabilmesi için bu ML API'sinin başlatılması gerekir.
1. Yeni bir terminal açıp `ML2` klasörüne gidin.
2. Gerekli paketleri yükleyin: `pip install fastapi uvicorn scikit-learn pandas` (veya `requirements.txt` varsa onu kullanın).
3. API sunucusunu başlatın:
   ```bash
   python -m uvicorn ml_api:app --reload --port 8000
   
### 5. Web Uygulamasını Başlatma (C#)
1. Visual Studio'da HrDecisionSupport.slnx çözüm dosyasını açın.

2. HrDecisionSupport.Web projesini Başlangıç Projesi (Startup Project) olarak ayarlayın.

3. F5 tuşuna basarak projeyi derleyip çalıştırın.

4. Tarayıcınızda açılan İK Karar Destek Sistemi üzerinden tüm özellikleri test edebilirsiniz!
