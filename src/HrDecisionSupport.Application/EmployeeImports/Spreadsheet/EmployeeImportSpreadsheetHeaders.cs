namespace HrDecisionSupport.Application.EmployeeImports.Spreadsheet;

public static class EmployeeImportSpreadsheetHeaders
{
    public const string AnonymousEmployeeCode = "Anonim çalışan numarası";
    public const string CurrentPosition = "Mevcut pozisyon";
    public const string TotalExperience = "Toplam deneyim";
    public const string BackendExperience = "Backend deneyimi";
    public const string PreviousPositions = "Önceki pozisyonlar";
    public const string TechnicalSkills = "Teknik beceriler";
    public const string TechnologiesAndTools = "Teknoloji ve araçlar";
    public const string ProjectExperiences = "Proje deneyimleri";
    public const string SectorExperience = "Sektör deneyimi";
    public const string EducationLevel = "Eğitim seviyesi";
    public const string EducationField = "Eğitim alanı";
    public const string Certificates = "Sertifikalar";
    public const string ForeignLanguages = "Yabancı diller";
    public const string WorkModeExperience = "Çalışma şekli deneyimi";
    public const string HireDate = "İşe giriş tarihi";
    public const string TerminationDate = "İşten çıkış tarihi";
    public const string CompanyTenureYears = "Şirkette çalışma süresi (yıl)";
    public const string PreviousCompanyAverageStayMonths = "Önceki şirketlerde ortalama kalış (ay)";
    public const string ShortestPreviousJobMonths = "En kısa önceki iş (ay)";
    public const string LongestPreviousJobMonths = "En uzun önceki iş (ay)";
    public const string LastPreviousCompanyStayMonths = "Son önceki şirkette kalış (ay)";
    public const string CompanyChangeCount = "Toplam şirket değişimi sayısı";
    public const string JobChangeRate = "İş değiştirme sayısı / toplam deneyim";
    public const string StayLabel = "Kalış etiketi (0 Kısa, 1 Normal, 2 Uzun)";

    public static IReadOnlyList<string> Required { get; } =
    [
        AnonymousEmployeeCode, CurrentPosition, TotalExperience, BackendExperience,
        PreviousPositions, TechnicalSkills, TechnologiesAndTools, ProjectExperiences,
        SectorExperience, EducationLevel, EducationField, Certificates, ForeignLanguages,
        WorkModeExperience, HireDate, TerminationDate, CompanyTenureYears,
        PreviousCompanyAverageStayMonths, ShortestPreviousJobMonths, LongestPreviousJobMonths,
        LastPreviousCompanyStayMonths, CompanyChangeCount, JobChangeRate, StayLabel
    ];
}
