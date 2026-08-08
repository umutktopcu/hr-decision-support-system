using System.Linq;
using HrDecisionSupport.Application.CandidateImports.Models;
using HrDecisionSupport.Application.CandidateImports.Processing;
using Xunit;

namespace HrDecisionSupport.Tests.CandidateImports;

public class CandidateImportValidatorTests
{
    private readonly CandidateImportValidator _validator = new();

    [Fact]
    public void Validate_MissingTitle_ReturnsError()
    {
        var record = new CandidateInputRecord(1, "A1", "Backend", null, 5, 3, null, null, null, null, null, null, null, null, null, null, null, null, 14);
        var result = _validator.Validate(record);
        Assert.Contains(result, e => e.PropertyName == "ProfessionalTitle");
    }

    [Fact]
    public void Validate_NegativeExperience_ReturnsError()
    {
        var record = new CandidateInputRecord(1, "A1", "Backend", "Dev", -1, 3, null, null, null, null, null, null, null, null, null, null, null, null, 14);
        var result = _validator.Validate(record);
        Assert.Contains(result, e => e.PropertyName == "TotalExperienceYears");
    }

    [Fact]
    public void Validate_BackendGreaterThanTotal_ReturnsError()
    {
        var record = new CandidateInputRecord(1, "A1", "Backend", "Dev", 5, 6, null, null, null, null, null, null, null, null, null, null, null, null, 14);
        var result = _validator.Validate(record);
        Assert.Contains(result, e => e.PropertyName == "BackendExperienceYears");
    }

    [Fact]
    public void Validate_ValidRecord_ReturnsEmpty()
    {
        var record = new CandidateInputRecord(1, "A1", "Backend", "Dev", 5, 3, null, null, null, null, null, null, null, null, null, null, null, null, 14);
        var result = _validator.Validate(record);
        Assert.Empty(result);
    }
}
