using System.Reflection;
using HrDecisionSupport.Application;
using HrDecisionSupport.Application.Candidates;
using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Employees.Dtos;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Tests;

public class ApplicationFoundationTests
{
    [Fact]
    public void ResultSuccess_IsSuccessfulAndContainsNoErrors()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ResultFailure_CarriesCodeMessageAndFailureType()
    {
        var result = Result.Failure("employees.unavailable", "Employee data is unavailable.");

        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("employees.unavailable", error.Code);
        Assert.Equal("Employee data is unavailable.", error.Message);
        Assert.Equal(ErrorType.Failure, error.Type);
    }

    [Fact]
    public void ValidationFailure_CarriesMultipleValidationErrors()
    {
        var result = Result.ValidationFailure(
            new ValidationError("employee.code.required", "Employee code is required.", "EmployeeCode"),
            new ValidationError("employee.email.invalid", "Email is invalid.", "Email"));

        Assert.True(result.IsFailure);
        Assert.Collection(
            result.Errors,
            first =>
            {
                var error = Assert.IsType<ValidationError>(first);
                Assert.Equal("EmployeeCode", error.PropertyName);
                Assert.Equal(ErrorType.Validation, error.Type);
            },
            second =>
            {
                var error = Assert.IsType<ValidationError>(second);
                Assert.Equal("Email", error.PropertyName);
                Assert.Equal(ErrorType.Validation, error.Type);
            });
    }

    [Fact]
    public void GenericResultSuccess_CarriesValue()
    {
        var result = Result<string>.Success("employee-001");

        Assert.True(result.IsSuccess);
        Assert.Equal("employee-001", result.Value);
    }

    [Fact]
    public void GenericResultSuccess_RejectsNullReferenceValue()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));
    }

    [Fact]
    public void GenericResultFailure_ThrowsWhenValueIsRead()
    {
        var result = Result<string>.Failure("employees.not-found", "Employee was not found.");

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void ErrorNotFoundFactory_CarriesNotFoundType()
    {
        var error = Error.NotFound("employees.not-found", "Employee was not found.");

        Assert.Equal(ErrorType.NotFound, error.Type);
    }

    [Fact]
    public void ErrorConflictFactory_CarriesConflictType()
    {
        var error = Error.Conflict("employees.conflict", "Employee already exists.");

        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Error_RejectsNullOrWhiteSpaceCode(string? code)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Error(code!, "A valid message."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Error_RejectsNullOrWhiteSpaceMessage(string? message)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Error("error.code", message!));
    }

    [Fact]
    public void Error_RejectsUndefinedErrorType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Error("error.code", "A valid message.", (ErrorType)0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidationError_RejectsNullOrWhiteSpacePropertyName(string? propertyName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new ValidationError("employee.invalid", "Employee is invalid.", propertyName!));
    }

    [Fact]
    public void ValidationResultValid_HasNoErrors()
    {
        var validationResult = ValidationResult.Valid();

        Assert.True(validationResult.IsValid);
        Assert.Empty(validationResult.Errors);
    }

    [Fact]
    public void ValidationResultInvalid_CarriesMultipleErrors()
    {
        var validationResult = ValidationResult.Invalid(
            new ValidationError("employee.code.required", "Code is required.", "EmployeeCode"),
            new ValidationError("employee.email.invalid", "Email is invalid.", "Email"));

        Assert.False(validationResult.IsValid);
        Assert.Equal(2, validationResult.Errors.Count);
    }

    [Fact]
    public void ValidationResultInvalid_RejectsEmptyCollection()
    {
        Assert.Throws<ArgumentException>(() =>
            ValidationResult.Invalid(Array.Empty<ValidationError>()));
    }

    [Fact]
    public void ValidationFailures_RejectNullValidationErrorElement()
    {
        var errors = new ValidationError[] { null! };

        Assert.Throws<ArgumentException>(() => ValidationResult.Invalid(errors));
        Assert.Throws<ArgumentException>(() => Result.ValidationFailure(errors));
    }

    [Fact]
    public void ValidationResultToResult_CreatesValidationFailure()
    {
        var validationResult = ValidationResult.Invalid(
            new ValidationError("employee.code.required", "Code is required.", "EmployeeCode"));

        var result = validationResult.ToResult();

        Assert.True(result.IsFailure);
        var error = Assert.IsType<ValidationError>(Assert.Single(result.Errors));
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void ResultErrors_CannotBeModifiedExternally()
    {
        var result = Result.Failure("employees.unavailable", "Employee data is unavailable.");

        Assert.False(result.Errors.GetType().IsArray);
        var errors = Assert.IsAssignableFrom<IList<Error>>(result.Errors);
        Assert.True(errors.IsReadOnly);
        Assert.Throws<NotSupportedException>(() =>
            errors[0] = Error.Failure("replacement.error", "Replacement error."));
    }

    [Fact]
    public void ValidationResultErrors_CannotBeModifiedExternally()
    {
        var validationResult = ValidationResult.Invalid(
            new ValidationError("employee.code.required", "Code is required.", "EmployeeCode"));

        Assert.False(validationResult.Errors.GetType().IsArray);
        var errors = Assert.IsAssignableFrom<IList<ValidationError>>(validationResult.Errors);
        Assert.True(errors.IsReadOnly);
        Assert.Throws<NotSupportedException>(() =>
            errors[0] = new ValidationError(
                "replacement.error",
                "Replacement error.",
                "EmployeeCode"));
    }

    [Fact]
    public void ValidatorContract_ValidateReturnsValidationResult()
    {
        var validate = typeof(IValidator<string>).GetMethod(nameof(IValidator<string>.Validate));

        Assert.NotNull(validate);
        Assert.Equal(typeof(ValidationResult), validate.ReturnType);
        Assert.Equal(typeof(string), Assert.Single(validate.GetParameters()).ParameterType);
    }

    [Theory]
    [MemberData(nameof(EmployeeRequestTypesAndAllowedProperties))]
    public void EmployeeRequests_ContainOnlyAllowedInputFields(
        Type requestType,
        IReadOnlySet<string> allowedProperties)
    {
        var properties = requestType.GetProperties().Select(property => property.Name).ToArray();

        Assert.NotEmpty(properties);
        Assert.All(properties, property => Assert.Contains(property, allowedProperties));
        Assert.DoesNotContain("Id", properties);
        Assert.DoesNotContain("PersonId", properties);
        Assert.DoesNotContain("CreatedAtUtc", properties);
        Assert.DoesNotContain("UpdatedAtUtc", properties);
    }

    [Fact]
    public void CandidateDtos_DoNotExposeCandidateLevelStatusOrApplicationFields()
    {
        var candidateDtoTypes = new[]
        {
            typeof(CandidateListItemDto),
            typeof(CandidateDetailsDto),
            typeof(CreateCandidateRequest),
            typeof(UpdateCandidateRequest)
        };

        Assert.All(
            candidateDtoTypes,
            type =>
            {
                var properties = type.GetProperties().Select(property => property.Name).ToArray();
                Assert.DoesNotContain("Status", properties);
                Assert.DoesNotContain("ApplicationId", properties);
                Assert.DoesNotContain("HasApplied", properties);
                Assert.Contains("CandidateSource", properties);
                Assert.Contains("ExternalCandidateId", properties);
            });
    }

    [Fact]
    public void DbContextAbstraction_ContainsExpectedSetsAndSaveChangesContract()
    {
        var expectedSets = new Dictionary<string, Type>
        {
            [nameof(IHrDecisionSupportDbContext.People)] = typeof(Person),
            [nameof(IHrDecisionSupportDbContext.Employees)] = typeof(Employee),
            [nameof(IHrDecisionSupportDbContext.Candidates)] = typeof(Candidate),
            [nameof(IHrDecisionSupportDbContext.EmployeeAssignments)] = typeof(EmployeeAssignment),
            [nameof(IHrDecisionSupportDbContext.Departments)] = typeof(Department),
            [nameof(IHrDecisionSupportDbContext.Positions)] = typeof(Position),
            [nameof(IHrDecisionSupportDbContext.JobRequisitions)] = typeof(JobRequisition),
            [nameof(IHrDecisionSupportDbContext.CandidateEvaluationCases)] =
                typeof(CandidateEvaluationCase)
        };

        foreach (var (propertyName, entityType) in expectedSets)
        {
            var property = typeof(IHrDecisionSupportDbContext).GetProperty(propertyName);
            Assert.NotNull(property);
            Assert.Equal(typeof(DbSet<>).MakeGenericType(entityType), property.PropertyType);
            Assert.False(property.CanWrite);
        }

        var saveChanges = typeof(IHrDecisionSupportDbContext).GetMethod(
            nameof(IHrDecisionSupportDbContext.SaveChangesAsync));
        Assert.NotNull(saveChanges);
        Assert.Equal(typeof(Task<int>), saveChanges.ReturnType);
        var parameter = Assert.Single(saveChanges.GetParameters());
        Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
    }

    [Fact]
    public void ConcreteDbContext_ImplementsApplicationAbstraction()
    {
        Assert.True(typeof(IHrDecisionSupportDbContext).IsAssignableFrom(
            typeof(HrDecisionSupportDbContext)));
    }

    [Fact]
    public void DependencyInjection_RegistersContextAbstractionForConcreteContext()
    {
        var services = new ServiceCollection();

        services.AddApplication();
        services.AddInfrastructure(options => options.UseNpgsql());

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var concrete = scope.ServiceProvider.GetRequiredService<HrDecisionSupportDbContext>();
        var abstraction = scope.ServiceProvider
            .GetRequiredService<IHrDecisionSupportDbContext>();

        Assert.Same(concrete, abstraction);
    }

    [Theory]
    [InlineData(typeof(IEmployeeService))]
    [InlineData(typeof(ICandidateService))]
    public void ServiceContracts_UseCancellationTokensAndResultReturns(Type serviceType)
    {
        var methods = serviceType.GetMethods();
        Assert.Equal(
            ["CreateAsync", "GetByIdAsync", "ListAsync", "UpdateAsync"],
            methods.Select(method => method.Name).Order().ToArray());

        Assert.All(
            methods,
            method =>
            {
                var cancellationToken = method.GetParameters().Last();
                Assert.Equal(typeof(CancellationToken), cancellationToken.ParameterType);
                Assert.True(IsTaskOfResult(method.ReturnType));
            });
    }

    public static TheoryData<Type, IReadOnlySet<string>> EmployeeRequestTypesAndAllowedProperties =>
        new()
        {
            {
                typeof(CreateEmployeeRequest),
                new HashSet<string>
                {
                    "EmployeeCode", "AnonymousCode", "FirstName", "LastName", "Email",
                    "PhoneNumber", "HireDate", "EmploymentStatus", "InitialDepartmentId",
                    "InitialPositionId", "InitialAssignmentStartDate"
                }
            },
            {
                typeof(UpdateEmployeeRequest),
                new HashSet<string>
                {
                    "EmployeeCode", "FirstName", "LastName", "Email", "PhoneNumber",
                    "HireDate", "TerminationDate", "EmploymentStatus"
                }
            }
        };

    private static bool IsTaskOfResult(Type returnType)
    {
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            return false;
        }

        var taskValueType = returnType.GetGenericArguments()[0];
        return taskValueType == typeof(Result)
            || taskValueType.IsGenericType
                && taskValueType.GetGenericTypeDefinition() == typeof(Result<>);
    }
}
