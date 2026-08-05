# HR Decision Support System — Güncel Proje Yol Haritası v4

> Sistem tek şirket için tasarlanır. Temel amaç; çalışan ve aday profillerini yönetmek, İnsan Kaynaklarının internal işe alım talepleri oluşturmasını sağlamak, mevcut CV havuzundaki adayları iş gereksinimi uyumu ve uzun çalışma potansiyeli açısından analiz ederek İK’ya sıralı bir karar destek ekranı sunmaktır.

## Ürün Tanımı

Sistem bir public kariyer portalı veya uçtan uca aday takip sistemi değildir. Adaylar sisteme girerek ilana başvurmaz. İnsan Kaynakları mevcut Candidate/CV havuzunu kullanır.

### Temel Kullanım Akışı

1. İnsan Kaynakları bir `JobRequisition` oluşturur.
2. `JobRequisitionRequirement` kayıtlarıyla pozisyonun aranan özelliklerini tanımlar.
3. Candidate/CV havuzundaki adaylar seçilen işe alım talebi için analiz edilir.
4. Sistem her aday için iki ayrı karar desteği sonucu üretir:
   - İş gereksinimi uyumu: sayısal skor ve/veya PASS–FAIL sonucu.
   - Uzun çalışma potansiyeli: analistin belirleyeceği sınıflandırma çıktısı; örneğin uzun çalışma potansiyeli veya kısa çalışma riski.
5. İK sonuçları filtreler, sıralar ve adayların CV/profil detaylarını inceler.
6. İK dikkatini çeken adayları işe alım talebi bazında shortlist’e kaydeder ve daha sonra tekrar görüntüler.
7. Nihai değerlendirme İK’ya aittir; model otomatik red, onay veya işe alma kararı vermez.

### MVP Kapsamı

- Employee ve Candidate profil yönetimi.
- CV havuzunun oluşturulması ve yönetilmesi.
- Internal `JobRequisition` ve `JobRequisitionRequirement` yönetimi.
- İş gereksinimi uyumu analizi.
- Uzun çalışma potansiyeli sınıflandırması.
- Adayların ayrı analiz sonuçlarıyla filtrelenmesi ve sıralanması.
- Shortlist/kaydedilen adaylar özelliği.
- Model versiyonu ve analiz girdisi izlenebilirliği.
- Rol bazlı hassas veri erişimi, test, dokümantasyon ve demo.

### MVP Kapsamı Dışında

- Public kariyer veya ilan portalı.
- Adayın sisteme girip ilana başvurması.
- `JobApplication`, `Application` veya `ApplicationStatusHistory` akışı.
- Mülakat, teklif, onay, red veya işe alım süreci yönetimi.
- Model tarafından otomatik aday eleme, otomatik red veya otomatik işe alma.
- Analist tarafından henüz belirlenmemiş algoritma, eşik, skor formülü veya başarı metriğinin yazılım ekibi tarafından varsayılması.

## Temel Kararlar

- Tek şirket: `Company` entity/table oluşturulmaz.
- Department ve Position tek şirkete ait kabul edilir.
- `JobRequisition`, İK tarafından oluşturulan internal işe alım talebidir; public ilan değildir.
- Candidate kayıtları mevcut CV havuzundan gelir.
- İş gereksinimi uyumu ve retention çıktıları birbirinden ayrı gösterilir; varsayılan olarak tek bir birleşik skorda gizlenmez.
- Retention çıktısı şimdilik puan olmak zorunda değildir. Model sözleşmesi sınıf etiketi ve model sağlıyorsa opsiyonel güven değeri taşıyabilecek şekilde esnek tasarlanır.
- Uzun çalışma/retention metodolojisini analist belirler.
- Hesaplanmış alanlar core employee/candidate tablolarına sabit gerçek olarak yazılmaz.
- Model yalnızca karar desteği sağlar; nihai karar İnsan Kaynaklarına aittir.
- Shortlist kaydı aday profilini kopyalamaz; Candidate kimliğini, ilgili JobRequisition’ı ve mümkünse kaynak analiz çalışmasını referans eder.
- Aynı adayın aynı JobRequisition için birden fazla shortlist kaydı oluşturması engellenir.

## Milestone 0 — Project Foundation

**Durum:** TAMAMLANDI

**Amaç:** Derlenebilir ve Git ile takip edilen katmanlı proje iskeletini kurmak.

### Yapılacaklar

1. GitHub repository oluşturmak ve clone etmek.
2. Domain, Application, Infrastructure, Web ve Tests projelerini oluşturmak.
3. Projeleri solution’a eklemek ve project reference bağlantılarını kurmak.
4. Solution build işlemini başarıyla tamamlamak.
5. `develop` branch oluşturmak ve mevcut durumu commit etmek.

### Tamamlanma Kriterleri

- [x] Solution build başarılıdır.
- [x] `develop` branch ve başlangıç commit’i mevcuttur.
- [x] Working tree temizdir.

## Milestone 1 — PostgreSQL Persistence Foundation

**Durum:** TAMAMLANDI

**Amaç:** PostgreSQL ve Entity Framework Core bağlantı altyapısını kurmak.

### Yapılacaklar

1. PostgreSQL ve pgAdmin kurulumunu doğrulamak.
2. `hr_decision_support_db` database’ini oluşturmak.
3. Npgsql ve EF Core Design paketlerini eklemek.
4. `dotnet-ef` local tool kurulumunu yapmak.
5. `HrDecisionSupportDbContext` sınıfını oluşturmak.
6. `Program.cs` içinde `AddDbContext` ve `UseNpgsql` yapılandırmasını yapmak.
7. Connection string’i User Secrets içinde saklamak.
8. Secret bilgisinin Git’e girmediğini doğrulamak.
9. Build çalıştırmak.

### Tamamlanma Kriterleri

- [x] DbContext ve UseNpgsql hazırdır.
- [x] Database bağlantı bilgisi User Secrets içindedir.
- [x] Build başarılıdır.

## Milestone 2 — Tam Domain ve Database Tasarımı

**Durum:** TAMAMLANDI

**Amaç:** Excel’deki çalışan/CV alanları ile aday, internal işe alım talebi ve analiz süreçlerini destekleyen ilişkisel modeli C# tarafında tasarlamak.

### Yapılacaklar

1. Department ve Position entity’lerini oluşturmak; Company entity’si oluşturmamak.
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
13. CandidateDocument ile CV dosyası metadata’sını saklamak.
14. JobRequisition ve JobRequisitionRequirement yapılarını oluşturmak.
15. Enum, foreign key, unique constraint, index ve delete davranışlarını tanımlamak.
16. PostgreSQL tablo/kolon adlarını snake_case yapılandırmak.
17. DbSet ve EntityTypeConfiguration sınıflarını tamamlamak.
18. İlk kapsamda geliştirilen CandidateEvaluationCase yapısını domain modeline eklemek.

### Tamamlanma Kriterleri

- [x] Excel’deki profil alanlarının tamamı uygun tablolara eşlenmiştir.
- [x] Tek şirket tasarımı uygulanmıştır; Company bağımlılığı yoktur.
- [x] Internal işe alım talebi ve aday profili ilişkileri desteklenmektedir.
- [x] Build ve testler başarılıdır.

## Milestone 3 — Initial Migration ve Database Oluşturma

**Durum:** TAMAMLANDI

**Amaç:** Milestone 2’deki C# entity modelini gerçek PostgreSQL tablolarına dönüştürmek.

### Yapılacaklar

1. Migration öncesi build çalıştırmak.
2. `InitialCore` adlı ilk migration’ı oluşturmak.
3. Migration dosyasındaki tablo, foreign key, index ve constraint’leri incelemek.
4. Hatalı model varsa migration uygulanmadan önce düzeltmek.
5. Migration’ı PostgreSQL’e uygulamak.
6. pgAdmin üzerinden tabloları ve ilişkileri doğrulamak.
7. `__EFMigrationsHistory` tablosunu kontrol etmek.
8. Basit SQL sorguları ile veri erişimini test etmek.

### Tamamlanma Kriterleri

- [x] InitialCore migration oluşturulmuştur.
- [x] Bütün tablolar PostgreSQL’de görünmektedir.
- [x] İlişkiler ve constraint’ler doğrudur.
- [x] `__EFMigrationsHistory` mevcuttur.

## Milestone 4 — Seed Data ve Kontrollü Örnek Profil

**Durum:** ERTELENDİ / OPSİYONEL

**Amaç:** İhtiyaç oluşursa boş database’i küçük ve tutarlı örnek verilerle doldurmak.

### Yapılacaklar

1. Örnek departman ve pozisyon kayıtları eklemek.
2. Competency, sektör, dil, sertifika ve work mode sözlüklerini seed etmek.
3. Sınırlı sayıda örnek Employee ve Candidate profili oluşturmak.
4. Örnek JobRequisition ve JobRequisitionRequirement kayıtları oluşturmak.
5. Seed işleminin tekrar çalıştırıldığında duplicate üretmesini engellemek.
6. 300 kişilik Excel’i bu aşamada import etmemek; Excel import milestone’unu beklemek.

### Tamamlanma Kriterleri

- [ ] Örnek profiller ilişkileriyle sorgulanabilmektedir.
- [ ] Duplicate oluşmamaktadır.
- [ ] Seed data’ya gerçekten ihtiyaç olduğu ekip tarafından doğrulanmıştır.

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
2. İlk ürün kapsamında CandidateEvaluationCase use-case’leri tamamlandı.
3. JobRequisition internal işe alım talebi olarak modellendi.

### Güncel Kapsam Notu

`CandidateEvaluationCase` Milestone 5 sırasında geliştirilmiştir. Ürün kapsamı daha sonra CV havuzu sıralama ve shortlist sistemine sadeleştirildiği için aktif MVP akışında CandidateEvaluationCase ekranı, mülakat yönetimi veya onay/red süreci geliştirilmeyecektir. Mevcut kodun korunması ya da kontrollü biçimde kaldırılması, aktif Milestone 6 çalışması tamamlandıktan sonra yapılacak etki analizinde kararlaştırılacaktır.

### Tamamlanma Kriterleri

- [x] Application Layer foundation ve use-case’ler hazırdır.
- [x] DTO, Entity–DTO dönüşümleri ve servis kayıtları hazırdır.
- [x] Package-free validation mevcuttur.
- [x] Scoped dependency injection ve TimeProvider tabanlı audit timestamp’leri kullanılmaktadır.
- [x] Toplam 548 test başarılıdır.

## Milestone 6 — Employee ve Candidate Profile Frontend

**Durum:** DEVAM EDİYOR

**Amaç:** Employee ve Candidate kayıtlarının zengin profil yapısını web ekranlarında yönetmek ve ilerideki analiz ekranlarına güvenilir CV/profil verisi sağlamak.

### Yapılacaklar

1. Dashboard ve navigation oluşturmak.
2. Employee listesi ve detay ekranı oluşturmak.
3. Candidate listesi ve detay ekranı oluşturmak.
4. Profil sekmeleri oluşturmak: Genel, İş Geçmişi, Beceriler, Teknolojiler, Projeler, Sektörler, Eğitim, Sertifikalar, Diller ve Çalışma Şekli.
5. CV dosyası yükleme ve görüntüleme alanı oluşturmak.
6. Profil ekleme/güncelleme formları oluşturmak.
7. Arama, filtreleme, validation ve boş veri ekranlarını hazırlamak.
8. Controller’ların DbContext yerine Application use-case’lerini kullanmasını sağlamak.

### Tamamlanma Kriterleri

- [ ] Employee ve Candidate profilleri bütün gerekli alt alanlarıyla görüntülenebilmektedir.
- [ ] Profil ekleme ve düzenleme akışları çalışmaktadır.
- [ ] CV dosyası yetkili kullanıcı tarafından görüntülenebilmektedir.
- [ ] Arama, filtreleme ve boş veri durumları çalışmaktadır.
- [ ] Web katmanı Application katmanı üzerinden işlem yapmaktadır.

## Milestone 7 — Job Requisition ve Requirement Frontend

**Durum:** PLANLANDI

**Amaç:** İnsan Kaynaklarının internal işe alım taleplerini ve bu taleplerde aranan gereksinimleri web arayüzünden yönetmesini sağlamak.

### Yapılacaklar

1. JobRequisition liste ve detay ekranlarını oluşturmak.
2. JobRequisition oluşturma ve güncelleme formlarını hazırlamak.
3. Geçerli JobRequisition status transition’larını arayüzden yönetmek.
4. JobRequisitionRequirement listeleme, ekleme, güncelleme ve silme ekranlarını hazırlamak.
5. Talep detayında departman, pozisyon, açıklama, açık pozisyon sayısı ve gereksinimleri göstermek.
6. Terminal durumdaki requisition kurallarını arayüzde uygulamak.
7. Validation, hata mesajı, authorization hazırlığı ve empty state ekranlarını tamamlamak.
8. Gelecekteki analiz ekranına geçiş için requisition detayında açık bir giriş noktası hazırlamak; bu milestone’da gerçek model çalıştırmamak.

### Tamamlanma Kriterleri

- [ ] Internal işe alım talepleri UI üzerinden yönetilebilmektedir.
- [ ] Talep gereksinimleri UI üzerinden yönetilebilmektedir.
- [ ] Status transition ve terminal durum kuralları doğru uygulanmaktadır.
- [ ] Public başvuru akışı bulunmamaktadır; CandidateEvaluationCase aktif MVP kullanıcı akışında kullanılmayacaktır.

## Milestone 8 — Excel Import, Veri Kalitesi ve CV Havuzu

**Durum:** PLANLANDI

**Amaç:** Çalışan ve Candidate verilerini kontrollü biçimde normalize tablolara aktarmak, veri kalitesini izlemek ve analizde kullanılacak CV havuzunu oluşturmak.

### Yapılacaklar

1. ExternalSystem, ImportBatch, ImportRow ve ImportError yapılarını oluşturmak.
2. Excel kolonları için versiyonlu mapping tanımlamak.
3. Teknik beceriler ve teknoloji/araç alanlarını Competency kayıtlarına ayırmak.
4. Proje deneyimlerini PersonProject kayıtlarına ayırmak.
5. Sektör deneyimini sektör ve süre olarak parse etmek.
6. Eğitim, sertifika, dil ve çalışma şekli alanlarını ilgili tablolara aktarmak.
7. Önceki pozisyonları mevcut veri sınırlarıyla import etmek; tarih veya işveren yoksa kaynak metni korumak.
8. Candidate/CV havuzu kayıtlarını oluşturmak; CV dosyası metadata’sı ile yapılandırılmış profil verisini ilişkilendirmek.
9. Duplicate aday ve çalışan kayıtlarını tespit etmek; kesin olmayan eşleşmeleri manuel incelemeye bırakmak.
10. Hatalı satırları ve alan bazlı parse sorunlarını raporlamak.
11. Import önizleme, dry-run ve sonuç ekranı oluşturmak.
12. Core alanlar, derived analiz özellikleri ve model hedefini birbirinden ayırmak.
13. Retention modeli için geçmiş çalışan verisi ile runtime Candidate/CV havuzunun farklı amaçlara hizmet ettiğini açık biçimde ayırmak.
14. Runtime aday analizinde yalnızca değerlendirme anında erişilebilir özelliklerin kullanılmasını sağlamak; hedef veya gelecek bilgisi sızıntısını önlemek.

### Tamamlanma Kriterleri

- [ ] Excel kayıtları normalize tablolara kontrollü biçimde aktarılmaktadır.
- [ ] CV havuzu Candidate profilleriyle birlikte oluşturulabilmektedir.
- [ ] Duplicate ve hatalı kayıtlar izlenebilmektedir.
- [ ] Core, derived, model input ve model target alanları ayrılmıştır.
- [ ] Import tekrarlanabilir ve mümkün olduğunca idempotent çalışmaktadır.

## Milestone 9 — ERP-Ready Integration

**Durum:** OPSİYONEL / MVP SONRASI

**Amaç:** Gerçek ERP entegrasyonu istenirse mevcut import altyapısına bağlanabilecek veri kaynağı mimarisini hazırlamak.

### Yapılacaklar

1. `IEmployeeDataSource` interface’i oluşturmak.
2. Canonical `ExternalEmployeeRecord` modelini oluşturmak.
3. `ExcelEmployeeDataSource` ve `MockErpEmployeeDataSource` implementasyonlarını geliştirmek.
4. Sync servisi, external ID eşleştirmesi ve log yapısını kurmak.
5. Yeni kayıt, güncelleme ve çakışma ayrımını yapmak.
6. Manuel senkronizasyon ekranı oluşturmak.

### Tamamlanma Kriterleri

- [ ] ERP entegrasyonuna gerçekten ihtiyaç olduğu doğrulanmıştır.
- [ ] Mock ERP akışı çalışmaktadır.
- [ ] Yeni ve güncellenen kayıtlar ayrılmaktadır.
- [ ] Sync işlemleri loglanmaktadır.

## Milestone 10 — CV Havuzu Aday Sıralama ve Model Entegrasyonu

**Durum:** PLANLANDI

**Amaç:** Bir JobRequisition ve gereksinimlerine göre CV havuzundaki Candidate kayıtlarını analiz ederek İK’ya ayrı, açıklanabilir ve izlenebilir karar desteği sonuçları sunmak.

### 10.1 Analiz Sözleşmesi ve İzlenebilirlik

1. Versiyonlu `AnalysisContract` tanımlamak.
2. `AnalysisVersion`, `AnalysisRun`, `FeatureSnapshot` ve `AnalysisResult` yapılarını tasarlamak.
3. Model girdisinde JobRequisition, JobRequisitionRequirement ve Candidate profil verilerini kullanabilecek açık bir contract oluşturmak.
4. Her çalıştırmada model versiyonunu, çalışma zamanını ve input snapshot’ını saklamak.
5. Gerçek model hazır değilken kullanılabilecek mock analysis service geliştirmek.
6. Analiz başarısızlığı, yetersiz veri, uygulanamaz analiz ve model erişim hatalarını desteklemek.

### 10.2 İş Gereksinimi Uyumu

1. Adayın pozisyon gereksinimleriyle uyumunu ayrı bir sonuç olarak üretmek.
2. İş gereksinimi uyumu için sayısal skor ve/veya PASS–FAIL sonucu desteklemek.
3. Kullanılan eşik, ağırlık ve hesaplama politikasını versiyonlanabilir kılmak.
4. Zorunlu ve tercih edilen gereksinimlerin katkısını açıklanabilir biçimde göstermek.
5. Eksik veri nedeniyle değerlendirilemeyen gereksinimleri FAIL ile karıştırmamak.

### 10.3 Uzun Çalışma Potansiyeli

1. Retention modelinin çıktı formatını analistin belirleyeceği esnek bir sözleşmeyle desteklemek.
2. İlk hedef olarak uzun çalışma potansiyeli ve kısa çalışma riski sınıflarını desteklemek.
3. Model güven/olasılık değeri sağlıyorsa opsiyonel olarak saklamak ve göstermek; model üretmiyorsa yapay skor oluşturmamak.
4. Veri yetersizliği veya değerlendirilemez durumunu ayrı göstermek.
5. Retention çıktısını iş gereksinimi skoruyla varsayılan olarak tek skorda birleştirmemek.

### 10.4 Sonuç Ekranı, Filtreleme ve Sıralama

1. İnsan Kaynaklarının bir requisition için “Adayları Değerlendir” işlemini başlatmasını sağlamak.
2. Her aday için iş gereksinimi sonucu ve retention sınıfını ayrı sütunlarda göstermek.
3. İş uyumu PASS olanlar, retention sonucu belirli sınıfta olanlar ve iki koşulu birlikte sağlayanlar için filtreler sunmak.
4. İş gereksinimi skoruna, retention sınıfına ve analiz zamanına göre sıralama seçenekleri sağlamak.
5. Adayın CV ve profil detayına sonuç ekranından erişmek.
6. Model açıklamalarını ve eksik veri uyarılarını görünür kılmak.
7. Modelin otomatik red, onay veya işe alma kararı vermemesini sağlamak.

### 10.5 Shortlist / Kaydedilen Adaylar

1. İK’nın analiz sonuçlarındaki adayları JobRequisition bazında kaydedebilmesini sağlamak.
2. Shortlist kaydında en az CandidateId, JobRequisitionId, SavedAtUtc ve mümkünse SourceAnalysisRunId tutmak.
3. SavedByUserId desteğini authentication hazır olduğunda eklemek.
4. Opsiyonel İK notu eklenebilmesini sağlamak.
5. Aynı Candidate–JobRequisition ikilisi için duplicate shortlist kaydını engellemek.
6. Yeni analiz çalıştırıldığında mevcut shortlist kayıtlarını korumak.
7. Kaydedildiği andaki skor, sınıf veya sıra bilgisinin snapshot olarak tutulup tutulmayacağını model sözleşmesi tasarımında kararlaştırmak.
8. İK’nın adayı shortlist’ten çıkarabilmesini sağlamak.
9. Shortlist’i mülakat, onay, red veya işe alım durumundan bağımsız tutmak.

### Tamamlanma Kriterleri

- [ ] Bir JobRequisition için CV havuzu analiz edilebilmektedir.
- [ ] İş gereksinimi uyumu ve retention sonucu ayrı gösterilmektedir.
- [ ] Retention çıktısı sınıf etiketi olarak çalışabilmekte; güven değeri zorunlu tutulmamaktadır.
- [ ] Adaylar filtrelenip sıralanabilmektedir.
- [ ] Model versiyonu ve input snapshot izlenebilmektedir.
- [ ] Adayların CV ve profil detaylarına erişilebilmektedir.
- [ ] Adaylar requisition bazında shortlist’e kaydedilip yeniden açılabilmektedir.
- [ ] Model yalnızca karar desteği sağlamaktadır.

## Milestone 11 — Kimlik Doğrulama, Yetkilendirme ve Audit

**Durum:** PLANLANDI

**Amaç:** Hassas HR ve CV verisini rol bazlı güvenli hâle getirmek ve kritik kullanıcı işlemlerini izlemek.

### Yapılacaklar

1. ASP.NET Core Identity kurmak.
2. Admin, HR Manager, HR Viewer ve Analyst rollerini tanımlamak.
3. Login/logout ve yetkisiz erişim ekranlarını oluşturmak.
4. Controller/action yetkilerini tanımlamak.
5. CV dosyası ve kişisel veri erişimlerini yetkilendirmek.
6. JobRequisition oluşturma, analiz başlatma, shortlist ekleme/çıkarma ve kritik profil değişikliklerini audit etmek.
7. `SavedByUserId` ve kullanıcı bazlı shortlist görünürlüğü kararını uygulamak.
8. Authentication ve authorization testleri yazmak.

### Tamamlanma Kriterleri

- [ ] Roller doğru çalışmaktadır.
- [ ] Yetkisiz erişim engellenmektedir.
- [ ] Hassas CV verisi yalnız yetkili kullanıcılar tarafından görüntülenmektedir.
- [ ] Kritik işlemler audit edilmektedir.

## Milestone 12 — Kalite, PostgreSQL Integration Tests, Dokümantasyon ve Release

**Durum:** PLANLANDI

**Amaç:** Sistemi gerçek PostgreSQL davranışlarıyla doğrulanmış, kurulabilir, açıklanabilir ve sunulabilir hâle getirmek.

### Yapılacaklar

1. Unit test kapsamını tamamlamak.
2. Gerçek PostgreSQL üzerinde integration test altyapısı kurmak.
3. Migration, foreign key, unique index ve check constraint’leri PostgreSQL üzerinde doğrulamak.
4. Kritik Application sorgularının Npgsql ile gerçek database üzerinde çalıştığını doğrulamak.
5. Import, analiz çalıştırma ve shortlist akışları için integration testleri yazmak.
6. Global exception handling ve logging eklemek.
7. Pagination, arama ve filtreleme özelliklerini tamamlamak.
8. Secret, kişisel veri ve güvenlik kontrolü yapmak.
9. Kullanılmayan veya kapsam dışı kalan yapılar için kod envanteri çıkarmak; CandidateEvaluationCase’in korunması ya da kontrollü kaldırılması kararını migration ve test etkileriyle birlikte uygulamak.
10. README ve proje kurulum dokümantasyonunu tamamlamak.
11. Database migration, Excel import, model entegrasyonu ve demo dokümantasyonunu hazırlamak.
12. Sunum için mimari akış, temel entity ilişkileri, model çıktıları, iş kuralları ve test sonuçlarını özetlemek.
13. Release build ve final testleri çalıştırmak.
14. Git tag ve final release oluşturmak.

### Demo Senaryosu

```text
Veri importu
→ Employee/Candidate profilleri ve CV havuzu
→ JobRequisition oluşturma
→ JobRequisitionRequirement tanımlama
→ CV havuzunu modele gönderme
→ İş uyumu ve retention sonuçlarını ayrı görüntüleme
→ Sonuçları filtreleme ve sıralama
→ Aday CV/profil detayını inceleme
→ Adayı shortlist’e kaydetme
→ Kaydedilen adayları tekrar görüntüleme
```

### Tamamlanma Kriterleri

- [ ] Build, unit test ve PostgreSQL integration testleri başarılıdır.
- [ ] README ile proje kurulabilmektedir.
- [ ] Kritik mimari ve model kararları dokümante edilmiştir.
- [ ] Demo akışı baştan sona tamamlanmaktadır.
- [ ] Kapsam dışı public application ve süreç yönetimi özellikleri sisteme eklenmemiştir.

## Excel Alanlarının Database Eşlemesi

- **Anonim çalışan numarası** → `persons.anonymous_code` / `employees.external_employee_code` — `Core`
- **Mevcut pozisyon** → `positions` + `employee_assignments` — `Core`
- **Toplam deneyim** → Tarihlerden hesaplanır veya feature snapshot’ta saklanır — `Derived`
- **Backend deneyimi** → `person_competencies.experience_months` veya tanımı netleştirilmiş kariyer metriği — `Core/Derived`
- **Önceki pozisyonlar** → `employment_histories.position_title`; kaynak metin ayrıca korunabilir — `Core`
- **Teknik beceriler** → `competencies(category=skill)` + `person_competencies` — `Core`
- **Teknoloji ve araçlar** → `competencies(category=technology/tool)` + `person_competencies` — `Core`
- **Proje deneyimleri** → `projects` + `person_projects` — `Core`
- **Sektör deneyimi** → `sectors` + `person_sector_experiences` — `Core`
- **Eğitim seviyesi / alanı** → `education_records` — `Core`
- **Sertifikalar** → `certificates` + `person_certificates` — `Core`
- **Yabancı diller** → `languages` + `person_languages` — `Core`
- **Çalışma şekli deneyimi** → `work_modes` + `person_work_mode_experiences` — `Core`
- **İşe giriş / çıkış tarihi** → `employees` veya `employee_assignments` — `Core`
- **Şirkette çalışma süresi** → Tarihlerden hesaplanır — `Derived`
- **Önceki ortalama/min/max/son kalış** → EmploymentHistory’den hesaplanır veya FeatureSnapshot’ta saklanır — `Derived`
- **Toplam şirket değişimi** → EmploymentHistory’den hesaplanır — `Derived`
- **İş değiştirme oranı** → Analysis/FeatureSnapshot — `Derived`
- **uzun_calisan** → Retention model target/output; runtime Candidate profilinde gerçek alan olarak tutulmaz — `Analysis target/output`
- **İş gereksinimi uyumu** → JobRequisitionRequirement ve Candidate profilinden analiz zamanında üretilir — `Analysis output`
- **Shortlist durumu** → Candidate + JobRequisition ilişkili kullanıcı tercihi — `Operational decision-support data`

## MVP Tamamlanma Tanımı

MVP aşağıdaki yetenekler birlikte çalıştığında tamamlanmış kabul edilir:

1. Employee ve Candidate profilleri ile CV havuzu yönetilebilmektedir.
2. İK internal JobRequisition ve requirement kayıtlarını oluşturabilmektedir.
3. Bir requisition için CV havuzu analiz edilebilmektedir.
4. İş gereksinimi uyumu ve retention tahmini ayrı gösterilmektedir.
5. Adaylar filtrelenip sıralanabilmektedir.
6. İK aday CV/profil detayını açabilmekte ve adayı shortlist’e kaydedebilmektedir.
7. Model versiyonu ve input snapshot izlenebilmektedir.
8. Hassas verilere erişim yetkilendirilmiştir.
9. Kritik akışlar unit ve PostgreSQL integration testleriyle doğrulanmıştır.
10. Proje README üzerinden kurulabilmekte ve demo senaryosu baştan sona çalışmaktadır.

## Mevcut Durum

- Milestone 0 — TAMAMLANDI
- Milestone 1 — TAMAMLANDI
- Milestone 2 — TAMAMLANDI (tek şirket varsayımıyla, Company tablosu olmadan)
- Milestone 3 — TAMAMLANDI
- Milestone 4 — ERTELENDİ / OPSİYONEL
- Milestone 5 — TAMAMLANDI (548 test)
- Milestone 6 — DEVAM EDİYOR
- Milestone 7 — PLANLANDI
- Milestone 8 — PLANLANDI
- Milestone 9 — OPSİYONEL / MVP SONRASI
- Milestone 10 — PLANLANDI
- Milestone 11 — PLANLANDI
- Milestone 12 — PLANLANDI
- Sıradaki aktif geliştirme Milestone 6’dır; Milestone 7 ve sonrası paralel planlama için hazırdır.
