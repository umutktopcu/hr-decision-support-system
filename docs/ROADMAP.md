# HR Decision Support System — Tam Proje Yol Haritası v3

> Sistem tek şirket için tasarlanır. Company tablosu yoktur. Yol haritası çalışan/CV, internal işe alım talebi, aday değerlendirme, Excel/ERP, frontend ve analiz entegrasyonunun tamamını kapsar.

## Temel Kararlar

- Tek şirket: `Company` entity/table oluşturulmaz.
- Department ve Position tek şirkete ait kabul edilir.
- Uzun çalışma/retention metodolojisini analist belirler.
- Hesaplanmış alanlar core employee tablosuna sabit gerçek olarak yazılmaz.

## Milestone 0 — Project Foundation

**Durum:** TAMAMLANDI

**Amaç:** Derlenebilir ve Git ile takip edilen katmanlı proje iskeletini kurmak.

### Yapılacaklar

1. GitHub repository oluşturmak ve clone etmek.
2. Domain, Application, Infrastructure, Web ve Tests projelerini oluşturmak.
3. Projeleri solution’a eklemek ve project reference bağlantılarını kurmak.
4. Solution build işlemini başarıyla tamamlamak.
5. develop branch oluşturmak ve mevcut durumu commit etmek.

### Tamamlanma Kriterleri

- [x] Solution build başarılıdır.
- [x] develop branch ve başlangıç commit’i mevcuttur.
- [x] Working tree temizdir.

## Milestone 1 — PostgreSQL Persistence Foundation

**Durum:** TAMAMLANDI

**Amaç:** PostgreSQL ve Entity Framework Core bağlantı altyapısını kurmak.

### Yapılacaklar

1. PostgreSQL ve pgAdmin kurulumunu doğrulamak.
2. hr_decision_support_db database’ini oluşturmak.
3. Npgsql ve EF Core Design paketlerini eklemek.
4. dotnet-ef local tool kurulumunu yapmak.
5. HrDecisionSupportDbContext sınıfını oluşturmak.
6. Program.cs içinde AddDbContext ve UseNpgsql yapılandırmasını yapmak.
7. Connection string’i User Secrets içinde saklamak.
8. Secret bilgisinin Git’e girmediğini doğrulamak.
9. Build çalıştırmak.

### Tamamlanma Kriterleri

- [x] DbContext ve UseNpgsql hazırdır.
- [x] Database bağlantı bilgisi User Secrets içindedir.
- [x] Build başarılıdır.

## Milestone 2 — Tam Domain ve Database Tasarımı

**Durum:** TAMAMLANDI

**Amaç:** Excel’deki çalışan/CV alanları ile aday, internal işe alım talebi ve aday değerlendirme süreçlerini destekleyen tam ilişkisel modeli C# tarafında tasarlamak. Bu aşamada henüz PostgreSQL tabloları oluşturulmaz.

### Yapılacaklar

1. Department ve Position entity’lerini oluşturmak. Company entity’si oluşturulmamalıdır; sistem tek şirketlidir.
2. Person temel entity’sini oluşturmak; Employee ve Candidate rollerini Person’a bağlamak.
3. EmployeeAssignment ile şirket içi pozisyon/departman geçmişini modellemek.
4. EmploymentHistory ile aday ve çalışanların önceki iş geçmişini modellemek.
5. Competency kataloğu oluşturmak; beceri, programlama dili, framework, database, teknoloji ve araçları Category ile ayırmak.
6. PersonCompetency ile kişinin competency ilişkisini, seviye ve deneyim süresini saklamak.
7. Project ve PersonProject tablolarını oluşturmak.
8. Sector ve PersonSectorExperience tablolarını oluşturmak.
9. EducationRecord tablosunu oluşturmak.
10. Certificate ve PersonCertificate tablolarını oluşturmak.
11. Language ve PersonLanguage tablolarını oluşturmak; dil seviyesi ve ana dil bilgisini saklamak.
12. WorkMode ve PersonWorkModeExperience tablolarını oluşturmak.
13. CandidateDocument tablosuyla CV dosyası metadata’sını saklamak.
14. JobRequisition, JobRequisitionRequirement ve CandidateEvaluationCase tablolarını oluşturmak.
15. CandidateEvaluationCase ile HR tarafından açılan internal değerlendirme dosyasını ve durumunu saklamak.
16. Enum, foreign key, unique constraint, index ve delete davranışlarını tanımlamak.
17. PostgreSQL tablo/kolon adlarını snake_case yapılandırmak.
18. DbSet ve EntityTypeConfiguration sınıflarını tamamlamak.

### Tamamlanma Kriterleri

- [x] Excel’deki profil alanlarının tamamı uygun tablolara eşlenmiştir.
- [x] Tek şirket tasarımı uygulanmıştır; Company bağımlılığı yoktur.
- [x] Internal işe alım talebi ve aday değerlendirme akışı desteklenmektedir.
- [x] Build ve testler başarılıdır.

## Milestone 3 — Initial Migration ve Database Oluşturma

**Durum:** TAMAMLANDI

**Amaç:** Milestone 2’deki C# entity modelini ilk defa gerçek PostgreSQL tablolarına dönüştürmek.

### Yapılacaklar

1. Migration öncesi build çalıştırmak.
2. InitialCore adlı ilk migration’ı oluşturmak.
3. Migration dosyasındaki tablo, foreign key, index ve constraint’leri incelemek.
4. Hatalı model varsa migration’ı uygulamadan önce düzeltmek.
5. Migration’ı PostgreSQL’e uygulamak.
6. pgAdmin üzerinden tabloları ve ilişkileri doğrulamak.
7. __EFMigrationsHistory tablosunu kontrol etmek.
8. Basit SQL sorguları ile veri erişimini test etmek.

### Tamamlanma Kriterleri

- [x] InitialCore migration oluşturulmuştur.
- [x] Bütün tablolar PostgreSQL’de görünmektedir.
- [x] İlişkiler ve constraint’ler doğrudur.
- [x] __EFMigrationsHistory mevcuttur.

## Milestone 4 — Seed Data ve Kontrollü Örnek Profil

**Durum:** ERTELENDİ / OPSİYONEL

**Amaç:** Boş database’i küçük, tutarlı ve ilişkileri tamamlanmış örnek verilerle doldurmak.

### Yapılacaklar

1. Örnek departman ve pozisyon kayıtları eklemek.
2. Skill/technology, sektör, dil, sertifika ve work mode sözlüklerini seed etmek.
3. 3–5 örnek çalışan ve aday profili oluşturmak.
4. Örnek internal işe alım talebi ve JobRequisitionRequirement kayıtları oluşturmak.
5. Örnek CandidateEvaluationCase kayıtları oluşturmak.
6. Seed işleminin tekrar çalıştırıldığında duplicate üretmesini engellemek.
7. İlişkili sorgularla kişi, profil, internal işe alım talebi ve aday değerlendirme bağlantılarını doğrulamak.
8. 300 kişilik Excel’i bu aşamada import etmemek; Excel import milestone’unu beklemek.

### Tamamlanma Kriterleri

- [ ] Örnek profiller bütün ilişkileriyle sorgulanabilmektedir.
- [ ] Duplicate oluşmamaktadır.
- [ ] Excel importundan önce temel model doğrulanmıştır.

## Milestone 5 — Application Layer ve Temel Servisler

**Durum:** TAMAMLANDI

**Amaç:** Web katmanından bağımsız use-case, DTO ve validation katmanını kurmak; Controller’ların DbContext’e doğrudan bağlanmasını engellemek.

### Tamamlananlar

#### 5.1 Application Foundation — TAMAMLANDI

1. Application Layer foundation, use-case sözleşmeleri ve DTO yapısı oluşturuldu.
2. Package-free validation ile validation ve hata sonuçları standartlaştırıldı.
3. Application servisleri scoped dependency injection ile kaydedildi.
4. Audit timestamp’leri TimeProvider tabanlı hâle getirildi.
5. Async metotlar ve CancellationToken kullanıldı.

#### 5.2 Employee/Candidate Use Cases — TAMAMLANDI

1. Employee ve Candidate use-case’leri tamamlandı.
2. Listeleme, detay, oluşturma ve güncelleme DTO’ları ile Entity–DTO dönüşümleri hazırlandı.

#### 5.3 Profile, History and Assignment Use Cases — TAMAMLANDI

1. Structured profile use-case’leri tamamlandı.
2. EmploymentHistory, PersonProject, PersonSectorExperience ve PersonWorkModeExperience use-case’leri tamamlandı.
3. EmployeeAssignment use-case’leri tamamlandı.

#### 5.4 Requisition and Candidate Evaluation Use Cases — TAMAMLANDI

1. JobRequisition ve JobRequisitionRequirement use-case’leri tamamlandı.
2. CandidateEvaluationCase use-case’leri tamamlandı.
3. JobRequisition internal işe alım talebi, CandidateEvaluationCase ise HR tarafından açılan internal değerlendirme dosyası olarak modellendi; sistem public aday başvuru sistemi değildir.

### Tamamlanma Kriterleri

- [x] Application Layer foundation ve use-case’ler hazırdır.
- [x] DTO, Entity–DTO dönüşümleri ve servis kayıtları hazırdır.
- [x] Package-free validation mevcuttur.
- [x] Scoped dependency injection ve TimeProvider tabanlı audit timestamp’leri kullanılmaktadır.
- [x] Toplam 548 test başarılıdır.

## Milestone 6 — Çalışan ve CV Profil Yönetimi Frontend’i

**Durum:** PLANLANDI

**Amaç:** Çalışan ve adayların Excel’deki zengin profil yapısını web ekranlarında yönetmek.

### Yapılacaklar

1. Dashboard ve navigation oluşturmak.
2. Çalışan listesi ve detay ekranı oluşturmak.
3. Aday listesi ve detay ekranı oluşturmak.
4. Profil sekmeleri oluşturmak: Genel, İş Geçmişi, Beceriler, Teknolojiler, Projeler, Sektörler, Eğitim, Sertifikalar, Diller, Çalışma Şekli.
5. CV dosyası yükleme ve görüntüleme alanı oluşturmak.
6. Profil ekleme/güncelleme formları oluşturmak.
7. Arama, filtreleme ve boş veri ekranlarını hazırlamak.

### Tamamlanma Kriterleri

- [ ] Çalışan ve aday profilleri bütün alt alanlarıyla görüntülenebilmektedir.
- [ ] Profil düzenleme akışı çalışmaktadır.

## Milestone 7 — İş İlanı Gereksinimleri

**Durum:** PLANLANDI

**Amaç:** Pozisyon ilanlarını ve CV eşleştirmesinde kullanılabilecek gereksinimleri yönetmek.

### Yapılacaklar

1. İş ilanı oluşturma, güncelleme ve kapatma işlemlerini yapmak.
2. Zorunlu ve tercih edilen competency gereksinimleri eklemek.
3. Minimum deneyim, eğitim, dil, sertifika ve çalışma şekli gereksinimlerini desteklemek.
4. Gereksinim ağırlığı/önem derecesi alanını opsiyonel hazırlamak.
5. İlan detay ekranında bütün gereksinimleri göstermek.

### Tamamlanma Kriterleri

- [ ] İlan ve gereksinimleri yönetilebilmektedir.
- [ ] Gereksinimler normalize tablolarda saklanmaktadır.

## Milestone 8 — Aday Başvurusu ve Süreç Yönetimi

**Durum:** PLANLANDI

**Amaç:** Bir adayın belirli bir ilana başvurabilmesini ve başvuru sürecinin izlenmesini sağlamak.

### Yapılacaklar

1. Application oluşturma akışını geliştirmek.
2. Aynı adayın farklı ilanlara başvurmasını desteklemek.
3. Başvuru durumlarını tanımlamak: Received, Screening, Interview, Rejected, Offered, Hired.
4. ApplicationStatusHistory ile her durum değişimini kaydetmek.
5. Başvuru detay ekranında aday profili ve ilan gereksinimlerini birlikte göstermek.
6. Not ve manuel değerlendirme alanlarını eklemek.
7. Aday işe alındığında Candidate–Employee dönüşüm akışını tasarlamak.

### Tamamlanma Kriterleri

- [ ] Başvuru oluşturulabilmektedir.
- [ ] Durum geçmişi izlenmektedir.
- [ ] Aday ve ilan bilgisi tek ekranda görülebilmektedir.

## Milestone 9 — Excel Import ve Staging

**Durum:** PLANLANDI

**Amaç:** Gönderilen çalışan Excel’ini kontrollü biçimde normalize tablolara aktarmak.

### Yapılacaklar

1. ExternalSystem, ImportBatch, ImportRow ve ImportError tablolarını oluşturmak.
2. Excel kolonları için mapping tanımlamak.
3. Teknik beceriler ve teknoloji/araçlar alanlarını Competency kayıtlarına ayırmak.
4. Proje deneyimlerini PersonProject kayıtlarına ayırmak.
5. Sektör deneyimini sektör + süre olarak parse etmek.
6. Eğitim, sertifika, dil ve çalışma şekli alanlarını ilgili tablolara aktarmak.
7. Önceki pozisyonları mevcut veri sınırlarıyla import etmek; tarih/işveren yoksa raw kaynak metnini korumak.
8. Derived analiz alanlarını core tablolara yazmamak.
9. Hatalı satırları raporlamak ve duplicate kontrolü yapmak.
10. Import sonuç ekranı oluşturmak.

### Tamamlanma Kriterleri

- [ ] Excel kayıtları normalize tablolara aktarılmaktadır.
- [ ] Hatalar izlenebilmektedir.
- [ ] Core ve derived alanlar ayrılmıştır.

## Milestone 10 — ERP-Ready Integration

**Durum:** PLANLANDI

**Amaç:** Gerçek ERP olmadan gelecekte bağlanabilecek veri kaynağı mimarisini hazırlamak.

### Yapılacaklar

1. IEmployeeDataSource interface’i oluşturmak.
2. Canonical ExternalEmployeeRecord modelini oluşturmak.
3. ExcelEmployeeDataSource ve MockErpEmployeeDataSource implementasyonlarını geliştirmek.
4. Sync servisi, external ID eşleştirmesi ve log yapısını kurmak.
5. Yeni kayıt/güncelleme ayrımını yapmak.
6. Manuel senkronizasyon ekranı oluşturmak.

### Tamamlanma Kriterleri

- [ ] Mock ERP akışı çalışmaktadır.
- [ ] Yeni ve güncellenen kayıtlar ayrılmaktadır.
- [ ] Sync loglanmaktadır.

## Milestone 11 — Uzun Çalışma / Retention Analizi Entegrasyon İskeleti

**Durum:** PLANLANDI

**Amaç:** Analistin belirleyeceği yöntemi yazılıma bağlamak; metodolojiyi yazılım ekibi adına sabitlememek.

### Yapılacaklar

1. Analistin kullanacağı input alanları için versiyonlu AnalysisContract tanımlamak.
2. AnalysisVersion, AnalysisRun, FeatureSnapshot ve AnalysisResult tablolarını oluşturmak.
3. IEmployeeRetentionAnalysisService veya ICandidateAnalysisService interface’ini oluşturmak.
4. Mock analiz servisi geliştirmek.
5. Algoritma, eşik, formül ve metrik için placeholder bırakmak; bu kararları analist verecektir.
6. Analiz sonucunun esnek JSON çıktısı yanında ortak alanlarını saklamak.
7. Hangi veri snapshot’ı ve hangi analiz versiyonuyla sonuç üretildiğini kaydetmek.
8. Başvuru detayında analiz sonucu için ayrı bölüm oluşturmak.
9. Analiz başarısız, uygulanamaz veya yetersiz veri durumlarını desteklemek.

### Tamamlanma Kriterleri

- [ ] Yazılım gerçek metodolojiye bağımlı değildir.
- [ ] Mock analiz bağlanmıştır.
- [ ] Versiyon ve input snapshot izlenebilmektedir.

## Milestone 12 — Kimlik Doğrulama, Yetkilendirme ve Audit

**Durum:** PLANLANDI

**Amaç:** Hassas HR ve CV verisini rol bazlı güvenli hâle getirmek.

### Yapılacaklar

1. ASP.NET Core Identity kurmak.
2. Admin, HR Manager, HR Viewer ve Analyst rollerini tanımlamak.
3. Login/logout ve yetkisiz erişim ekranlarını oluşturmak.
4. Controller/action yetkilerini tanımlamak.
5. Audit log ile kritik değişiklikleri kaydetmek.
6. CV dosyası ve kişisel veri erişimlerini yetkilendirmek.
7. Authentication ve authorization testleri yazmak.

### Tamamlanma Kriterleri

- [ ] Roller doğru çalışmaktadır.
- [ ] Yetkisiz erişim engellenmektedir.
- [ ] Kritik işlemler audit edilmektedir.

## Milestone 13 — Kalite, Test, Dokümantasyon ve Release

**Durum:** PLANLANDI

**Amaç:** Projeyi kurulabilir, test edilmiş ve sunulabilir hâle getirmek.

### Yapılacaklar

1. Unit ve integration test kapsamını tamamlamak.
2. Global exception handling ve logging eklemek.
3. Pagination, arama ve filtreleme özelliklerini tamamlamak.
4. Secret ve güvenlik kontrolü yapmak.
5. README, migration, import ve demo dokümantasyonunu yazmak.
6. Demo senaryosu hazırlamak: çalışan importu → ilan → aday/CV → başvuru → mock analiz.
7. Release build, Git tag ve final release oluşturmak.

### Tamamlanma Kriterleri

- [ ] Build ve testler başarılıdır.
- [ ] README ile kurulum yapılmaktadır.
- [ ] Demo akışı baştan sona tamamlanmaktadır.

## Excel Alanlarının Database Eşlemesi

- **Anonim çalışan numarası** → persons.anonymous_code / employees.external_employee_code — `Core`
- **Mevcut pozisyon** → positions + employee_assignments — `Core`
- **Toplam deneyim** → Tarihlerden hesaplanır veya feature snapshot’ta saklanır — `Derived`
- **Backend deneyimi** → person_competencies.experience_months veya tanımı netleştirilmiş kariyer metriği — `Core/Derived`
- **Önceki pozisyonlar** → employment_histories.position_title; kaynak metin ayrıca korunabilir — `Core`
- **Teknik beceriler** → competencies(category=skill) + person_competencies — `Core`
- **Teknoloji ve araçlar** → competencies(category=technology/tool) + person_competencies — `Core`
- **Proje deneyimleri** → projects + person_projects — `Core`
- **Sektör deneyimi** → sectors + person_sector_experiences — `Core`
- **Eğitim seviyesi / alanı** → education_records — `Core`
- **Sertifikalar** → certificates + person_certificates — `Core`
- **Yabancı diller** → languages + person_languages — `Core`
- **Çalışma şekli deneyimi** → work_modes + person_work_mode_experiences — `Core`
- **İşe giriş / çıkış tarihi** → employees veya employee_assignments — `Core`
- **Şirkette çalışma süresi** → Tarihlerden hesaplanır — `Derived`
- **Önceki ortalama/min/max/son kalış** → EmploymentHistory’den hesaplanır veya feature snapshot — `Derived`
- **Toplam şirket değişimi** → EmploymentHistory’den hesaplanır — `Derived`
- **İş değiştirme oranı** → Analysis/FeatureSnapshot — `Derived`
- **uzun_calisan** → analysis_results — `Analysis output`

## Mevcut Durum

- Milestone 0 — TAMAMLANDI
- Milestone 1 — TAMAMLANDI
- Milestone 2 — TAMAMLANDI (tek şirket varsayımıyla, Company tablosu olmadan)
- Milestone 3 — TAMAMLANDI
- Milestone 4 — ERTELENDİ / OPSİYONEL (sentetik/reference/demo veri üretimi uygulanmadı)
- Milestone 5 — TAMAMLANDI
- Sıradaki aktif aşama Milestone 6’dır.
