using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using HrDecisionSupport.Application.CandidateImports.Models;
using HrDecisionSupport.Application.CandidateImports.Spreadsheet;

namespace HrDecisionSupport.Infrastructure.CandidateImports;

public sealed class ClosedXmlCandidateSpreadsheetReader : ICandidateImportSourceReader
{
    public Task<IReadOnlyList<CandidateInputRecord>> ReadRecordsAsync(
        Stream sourceStream,
        CancellationToken cancellationToken = default)
    {
        var records = new List<CandidateInputRecord>();
        
        using var workbook = new XLWorkbook(sourceStream);
        if (!workbook.TryGetWorksheet("Aday CV Verisi", out var sheet))
        {
            throw new InvalidOperationException("Gerekli olan 'Aday CV Verisi' çalışma sayfası bulunamadı.");
        }

        var headerRow = sheet.FirstRowUsed();
        if (headerRow is null)
        {
            return Task.FromResult<IReadOnlyList<CandidateInputRecord>>(Array.Empty<CandidateInputRecord>());
        }

        var rows = sheet.RowsUsed().Skip(1); // Skip header

        foreach (var row in rows)
        {
            var rowNumber = row.RowNumber();
            
            // 18 Kolon mapping:
            // 1. Anonim aday numarası
            var anonymousCode = GetString(row, 1);
            if (string.IsNullOrWhiteSpace(anonymousCode)) continue; // Essential for identification
            
            // 2. Başvurulan pozisyon
            var targetPosition = GetString(row, 2);
            // 3. Mesleki unvan
            var professionalTitle = GetString(row, 3);
            // 4. Toplam deneyim yılı
            var totalExperience = GetDecimal(row, 4);
            // 5. Backend deneyim yılı
            var backendExperience = GetDecimal(row, 5);
            // 6. Önceki pozisyonlar
            var previousPositions = GetString(row, 6);
            // 7. Önceki şirketler
            var previousCompanies = GetString(row, 7);
            // 8. Önceki çalışma tarihleri
            var previousDates = GetString(row, 8);
            // 9. Teknik beceriler, araç ve teknolojiler
            var technicalSkills = GetString(row, 9);
            // 10. Framework ve veritabanı bilgisi
            var frameworksAndDbs = GetString(row, 10);
            // 11. Proje deneyimleri
            var projects = GetString(row, 11);
            // 12. Eğitim seviyesi
            var educationLevel = GetString(row, 12);
            // 13. Eğitim alanı
            var educationField = GetString(row, 13);
            // 14. Sertifikalar
            var certificates = GetString(row, 14);
            // 15. Yabancı diller
            var languages = GetString(row, 15);
            // 16. Sektör deneyimi
            var sectorExperience = GetString(row, 16);
            // 17. Çalışma şekli tercihi
            var workModePreference = GetString(row, 17);
            // 18. İşe başlayabilme süresi (gün)
            var availabilityDaysStr = GetString(row, 18);
            int? availabilityDays = null;
            if (int.TryParse(availabilityDaysStr, out var ad))
            {
                availabilityDays = ad;
            }

            records.Add(new CandidateInputRecord(
                rowNumber,
                anonymousCode,
                targetPosition,
                professionalTitle,
                totalExperience,
                backendExperience,
                previousPositions,
                previousCompanies,
                previousDates,
                technicalSkills,
                frameworksAndDbs,
                projects,
                educationLevel,
                educationField,
                certificates,
                languages,
                sectorExperience,
                workModePreference,
                availabilityDays
            ));
        }

        return Task.FromResult<IReadOnlyList<CandidateInputRecord>>(records);
    }

    private static string? GetString(IXLRow row, int column)
    {
        var cell = row.Cell(column);
        var val = cell.GetString()?.Trim();
        return string.IsNullOrEmpty(val) ? null : val;
    }

    private static decimal? GetDecimal(IXLRow row, int column)
    {
        var cell = row.Cell(column);
        if (cell.TryGetValue<decimal>(out var decimalValue))
        {
            return decimalValue;
        }
        
        var strVal = cell.GetString()?.Trim();
        if (string.IsNullOrEmpty(strVal)) return null;
        
        // Try parsing string manually
        if (decimal.TryParse(strVal.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        
        return null;
    }
}
