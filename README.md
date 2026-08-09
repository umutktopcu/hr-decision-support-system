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

### 2. Veritabanı Kurulumu
1. `src` altındaki projenin `appsettings.json` dosyasını açın.
2. `ConnectionStrings` bölümündeki PostgreSQL bağlantı bilgilerini kendi bilgisayarınıza göre düzenleyin.
3. Terminal üzerinden veritabanını oluşturmak için EF Core migration komutunu çalıştırın:
   `dotnet ef database update`

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
