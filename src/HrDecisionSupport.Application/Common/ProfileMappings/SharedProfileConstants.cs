using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HrDecisionSupport.Domain.Enums;
using System;

namespace HrDecisionSupport.Application.Common.ProfileMappings;

public static class SharedProfileConstants
{
    public static readonly IReadOnlyDictionary<string, string> CanonicalCompetencyCodeOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) 
    { 
        ["C#"] = "C_SHARP", ["C++"] = "CPP", [".NET"] = "DOTNET", [".NET Core"] = "DOTNET_CORE", ["ASP.NET Core"] = "ASP_NET_CORE", ["Vue.js"] = "VUE_JS", ["Node.js"] = "NODE_JS", ["CI/CD"] = "CI_CD", ["AWS"] = "AWS", ["Linux"] = "LINUX", ["Olay güdümlü mimari"] = "OLAY_GUDUMLU_MIMARI", ["SOLID prensipleri"] = "SOLID_PRENSIPLERI", ["Postman"] = "POSTMAN", ["REST API geliştirme"] = "REST_API_GELISTIRME", ["Veri tabanı tasarımı"] = "VERI_TABANI_TASARIMI", ["GitLab CI"] = "GITLAB_CI", ["Tasarım desenleri"] = "TASARIM_DESENLERI", ["Dağıtık sistemler"] = "DAGITIK_SISTEMLER", ["Nginx"] = "NGINX", ["Grafana"] = "GRAFANA", ["Elasticsearch"] = "ELASTICSEARCH", ["API güvenliği"] = "API_GUVENLIGI", ["Swagger"] = "SWAGGER", ["Güvenli kodlama"] = "GUVENLI_KODLAMA", ["Önbellekleme stratejileri"] = "ONBELLEKLEME_STRATEJILERI", ["Clean Code"] = "CLEAN_CODE", ["Loglama ve izleme"] = "LOGLAMA_VE_IZLEME", ["Prometheus"] = "PROMETHEUS", ["GitHub Actions"] = "GITHUB_ACTIONS", ["Azure"] = "AZURE", ["Performans optimizasyonu"] = "PERFORMANS_OPTIMIZASYONU", ["Sistem tasarımı"] = "SISTEM_TASARIMI", ["Test otomasyonu"] = "TEST_OTOMASYONU", ["Asenkron programlama"] = "ASENKRON_PROGRAMLAMA", ["Ktor"] = "KTOR", ["Express.js"] = "EXPRESS_JS", ["FastAPI"] = "FASTAPI", ["LINQ"] = "LINQ", ["Fiber"] = "FIBER", ["Entity Framework"] = "ENTITY_FRAMEWORK", ["SQLAlchemy"] = "SQLALCHEMY", ["GORM"] = "GORM", ["Celery"] = "CELERY", ["Sequelize"] = "SEQUELIZE", ["Gin"] = "GIN", ["Symfony"] = "SYMFONY", ["Laravel"] = "LARAVEL", ["Maven"] = "MAVEN", ["Spring Security"] = "SPRING_SECURITY", ["Composer"] = "COMPOSER", ["Hibernate"] = "HIBERNATE", ["Gradle"] = "GRADLE", ["NestJS"] = "NESTJS", ["TypeORM"] = "TYPEORM", ["Prisma"] = "PRISMA" 
    };
    
    public static readonly Dictionary<string, (string Name, CompetencyCategory Category)> Competencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = ("C", CompetencyCategory.ProgrammingLanguage),
        ["C#"] = ("C#", CompetencyCategory.ProgrammingLanguage),
        ["C Sharp"] = ("C#", CompetencyCategory.ProgrammingLanguage),
        ["Java"] = ("Java", CompetencyCategory.ProgrammingLanguage),
        ["Python"] = ("Python", CompetencyCategory.ProgrammingLanguage),
        ["JavaScript"] = ("JavaScript", CompetencyCategory.ProgrammingLanguage),
        ["JS"] = ("JavaScript", CompetencyCategory.ProgrammingLanguage),
        ["TypeScript"] = ("TypeScript", CompetencyCategory.ProgrammingLanguage),
        ["TS"] = ("TypeScript", CompetencyCategory.ProgrammingLanguage),
        ["PHP"] = ("PHP", CompetencyCategory.ProgrammingLanguage),
        ["Go"] = ("Go", CompetencyCategory.ProgrammingLanguage),
        ["C++"] = ("C++", CompetencyCategory.ProgrammingLanguage),
        ["Kotlin"] = ("Kotlin", CompetencyCategory.ProgrammingLanguage),
        ["ASP.NET Core"] = ("ASP.NET Core", CompetencyCategory.Framework),
        ["ASP.NET"] = ("ASP.NET Core", CompetencyCategory.Framework),
        [".NET"] = (".NET", CompetencyCategory.Framework),
        [".NET Core"] = (".NET Core", CompetencyCategory.Framework),
        ["Spring Boot"] = ("Spring Boot", CompetencyCategory.Framework),
        ["Django"] = ("Django", CompetencyCategory.Framework),
        ["Flask"] = ("Flask", CompetencyCategory.Framework),
        ["React"] = ("React", CompetencyCategory.Framework),
        ["Angular"] = ("Angular", CompetencyCategory.Framework),
        ["Vue.js"] = ("Vue.js", CompetencyCategory.Framework),
        ["Node.js"] = ("Node.js", CompetencyCategory.Framework),
        ["PostgreSQL"] = ("PostgreSQL", CompetencyCategory.Database),
        ["Postgres"] = ("PostgreSQL", CompetencyCategory.Database),
        ["SQL Server"] = ("SQL Server", CompetencyCategory.Database),
        ["MSSQL"] = ("SQL Server", CompetencyCategory.Database),
        ["MySQL"] = ("MySQL", CompetencyCategory.Database),
        ["Oracle"] = ("Oracle", CompetencyCategory.Database),
        ["MongoDB"] = ("MongoDB", CompetencyCategory.Database),
        ["Redis"] = ("Redis", CompetencyCategory.Database),
        ["Git"] = ("Git", CompetencyCategory.Tool),
        ["Docker"] = ("Docker", CompetencyCategory.Tool),
        ["Kubernetes"] = ("Kubernetes", CompetencyCategory.Tool),
        ["K8s"] = ("Kubernetes", CompetencyCategory.Tool),
        ["Kafka"] = ("Kafka", CompetencyCategory.Tool),
        ["RabbitMQ"] = ("RabbitMQ", CompetencyCategory.Tool),
        ["Jenkins"] = ("Jenkins", CompetencyCategory.Tool),
        ["Azure DevOps"] = ("Azure DevOps", CompetencyCategory.Tool),
        ["Jira"] = ("Jira", CompetencyCategory.Tool),
        ["Mikroservis mimarisi"] = ("Mikroservis mimarisi", CompetencyCategory.TechnicalConcept),
        ["Nesne yönelimli programlama"] = ("Nesne yönelimli programlama", CompetencyCategory.TechnicalConcept),
        ["REST API"] = ("REST API", CompetencyCategory.TechnicalConcept),
        ["Mesajlaşma sistemleri"] = ("Mesajlaşma sistemleri", CompetencyCategory.TechnicalConcept),
        ["Event-driven architecture"] = ("Event-driven architecture", CompetencyCategory.TechnicalConcept),
        ["CI/CD"] = ("CI/CD", CompetencyCategory.TechnicalConcept),
        ["AWS"] = ("AWS", CompetencyCategory.Tool), ["Linux"] = ("Linux", CompetencyCategory.Tool), ["Olay güdümlü mimari"] = ("Olay güdümlü mimari", CompetencyCategory.TechnicalConcept), ["SOLID prensipleri"] = ("SOLID prensipleri", CompetencyCategory.TechnicalConcept), ["Postman"] = ("Postman", CompetencyCategory.Tool), ["REST API geliştirme"] = ("REST API geliştirme", CompetencyCategory.TechnicalConcept), ["Veri tabanı tasarımı"] = ("Veri tabanı tasarımı", CompetencyCategory.TechnicalConcept), ["GitLab CI"] = ("GitLab CI", CompetencyCategory.Tool), ["Tasarım desenleri"] = ("Tasarım desenleri", CompetencyCategory.TechnicalConcept), ["Dağıtık sistemler"] = ("Dağıtık sistemler", CompetencyCategory.TechnicalConcept), ["Nginx"] = ("Nginx", CompetencyCategory.Tool), ["Grafana"] = ("Grafana", CompetencyCategory.Tool), ["Elasticsearch"] = ("Elasticsearch", CompetencyCategory.Tool), ["API güvenliği"] = ("API güvenliği", CompetencyCategory.TechnicalConcept), ["Swagger"] = ("Swagger", CompetencyCategory.Tool), ["Güvenli kodlama"] = ("Güvenli kodlama", CompetencyCategory.TechnicalConcept), ["Önbellekleme stratejileri"] = ("Önbellekleme stratejileri", CompetencyCategory.TechnicalConcept), ["Clean Code"] = ("Clean Code", CompetencyCategory.TechnicalConcept), ["Loglama ve izleme"] = ("Loglama ve izleme", CompetencyCategory.TechnicalConcept), ["Prometheus"] = ("Prometheus", CompetencyCategory.Tool), ["GitHub Actions"] = ("GitHub Actions", CompetencyCategory.Tool), ["Azure"] = ("Azure", CompetencyCategory.Tool), ["Performans optimizasyonu"] = ("Performans optimizasyonu", CompetencyCategory.TechnicalConcept), ["Sistem tasarımı"] = ("Sistem tasarımı", CompetencyCategory.TechnicalConcept), ["Test otomasyonu"] = ("Test otomasyonu", CompetencyCategory.TechnicalConcept), ["Asenkron programlama"] = ("Asenkron programlama", CompetencyCategory.TechnicalConcept), ["Ktor"] = ("Ktor", CompetencyCategory.Framework), ["Express.js"] = ("Express.js", CompetencyCategory.Framework), ["FastAPI"] = ("FastAPI", CompetencyCategory.Framework), ["LINQ"] = ("LINQ", CompetencyCategory.Tool), ["Fiber"] = ("Fiber", CompetencyCategory.Framework), ["Entity Framework"] = ("Entity Framework", CompetencyCategory.Framework), ["SQLAlchemy"] = ("SQLAlchemy", CompetencyCategory.Framework), ["GORM"] = ("GORM", CompetencyCategory.Framework), ["Celery"] = ("Celery", CompetencyCategory.Framework), ["Sequelize"] = ("Sequelize", CompetencyCategory.Framework), ["Gin"] = ("Gin", CompetencyCategory.Framework), ["Symfony"] = ("Symfony", CompetencyCategory.Framework), ["Laravel"] = ("Laravel", CompetencyCategory.Framework), ["Maven"] = ("Maven", CompetencyCategory.Tool), ["Spring Security"] = ("Spring Security", CompetencyCategory.Framework), ["Composer"] = ("Composer", CompetencyCategory.Tool), ["Hibernate"] = ("Hibernate", CompetencyCategory.Framework), ["Gradle"] = ("Gradle", CompetencyCategory.Tool), ["NestJS"] = ("NestJS", CompetencyCategory.Framework), ["TypeORM"] = ("TypeORM", CompetencyCategory.Framework), ["Prisma"] = ("Prisma", CompetencyCategory.Framework)
    };
    
    public static readonly Dictionary<string, DegreeLevel> Degrees = new(StringComparer.OrdinalIgnoreCase) 
    { 
        ["Lise"] = DegreeLevel.HighSchool, ["Ön Lisans"] = DegreeLevel.Associate, ["Lisans"] = DegreeLevel.Bachelor, ["Yüksek Lisans"] = DegreeLevel.Master, ["Doktora"] = DegreeLevel.Doctorate, ["Diğer"] = DegreeLevel.Other, ["Other"] = DegreeLevel.Other 
    };
    
    public static readonly HashSet<string> CertificateSentinels = new(StringComparer.OrdinalIgnoreCase) 
    { 
        "Yok", "Sertifika yok", "Bulunmuyor", "None", "No certificate" 
    };
    
    public static readonly Dictionary<string, (string Code, string Name)> Languages = new(StringComparer.OrdinalIgnoreCase) 
    { 
        ["Türkçe"] = ("TR", "Türkçe"), ["English"] = ("EN", "English"), ["İngilizce"] = ("EN", "İngilizce"), ["Almanca"] = ("DE", "Almanca"), ["Fransızca"] = ("FR", "Fransızca"), ["İspanyolca"] = ("ES", "İspanyolca"), ["İtalyanca"] = ("IT", "İtalyanca") 
    };

    public static string Code(string name) => CanonicalCompetencyCodeOverrides.TryGetValue(name, out var code) 
        ? code 
        : Regex.Replace(name.Normalize().ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_'); 

    public static string Hash(string x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x)))[..8];
}
