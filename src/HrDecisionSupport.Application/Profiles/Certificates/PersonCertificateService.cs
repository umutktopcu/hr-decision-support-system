using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Profiles.Certificates;

public sealed class PersonCertificateService : IPersonCertificateService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreatePersonCertificateRequest> _createValidator;
    private readonly IValidator<UpdatePersonCertificateRequest> _updateValidator;

    public PersonCertificateService(IHrDecisionSupportDbContext dbContext,
        IValidator<CreatePersonCertificateRequest> createValidator,
        IValidator<UpdatePersonCertificateRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<PersonCertificateDto>>> ListByPersonAsync(
        Guid personId, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == personId, cancellationToken))
            return Result<IReadOnlyList<PersonCertificateDto>>.Failure(UseCaseErrors.PersonNotFound);
        var items = await _dbContext.PersonCertificates.AsNoTracking()
            .Where(item => item.PersonId == personId)
            .OrderBy(item => item.IssueDate == null)
            .ThenByDescending(item => item.IssueDate)
            .ThenBy(item => item.Certificate.Name)
            .ThenBy(item => item.Id)
            .Select(item => new PersonCertificateDto(item.Id, item.PersonId, item.CertificateId,
                item.Certificate.Code, item.Certificate.Name, item.Certificate.Issuer,
                item.IssueDate, item.ExpirationDate, item.CredentialCode))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PersonCertificateDto>>.Success(items);
    }

    public async Task<Result<PersonCertificateDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.PersonCertificates.AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new PersonCertificateDto(value.Id, value.PersonId, value.CertificateId,
                value.Certificate.Code, value.Certificate.Name, value.Certificate.Issuer,
                value.IssueDate, value.ExpirationDate, value.CredentialCode))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? Result<PersonCertificateDto>.Failure(UseCaseErrors.PersonCertificateNotFound)
            : Result<PersonCertificateDto>.Success(item);
    }

    public async Task<Result<PersonCertificateDto>> CreateAsync(CreatePersonCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonCertificateDto>.ValidationFailure(validation.Errors);
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == request.PersonId, cancellationToken))
            return Result<PersonCertificateDto>.Failure(UseCaseErrors.PersonNotFound);
        var certificate = await _dbContext.Certificates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CertificateId, cancellationToken);
        if (certificate is null)
            return Result<PersonCertificateDto>.Failure(UseCaseErrors.CertificateNotFound);

        var entity = new PersonCertificate
        {
            Id = Guid.NewGuid(), PersonId = request.PersonId, CertificateId = request.CertificateId,
            IssueDate = request.IssueDate, ExpirationDate = request.ExpirationDate,
            CredentialCode = request.CredentialCode?.Trim()
        };
        await _dbContext.PersonCertificates.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<PersonCertificateDto>.Success(Map(entity, certificate));
    }

    public async Task<Result<PersonCertificateDto>> UpdateAsync(Guid id, UpdatePersonCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.PersonCertificates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<PersonCertificateDto>.Failure(UseCaseErrors.PersonCertificateNotFound);
        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonCertificateDto>.ValidationFailure(validation.Errors);
        entity.IssueDate = request.IssueDate;
        entity.ExpirationDate = request.ExpirationDate;
        entity.CredentialCode = request.CredentialCode?.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PersonCertificates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(UseCaseErrors.PersonCertificateNotFound);
        _dbContext.PersonCertificates.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PersonCertificateDto Map(PersonCertificate entity, Certificate certificate) =>
        new(entity.Id, entity.PersonId, entity.CertificateId, certificate.Code, certificate.Name,
            certificate.Issuer, entity.IssueDate, entity.ExpirationDate, entity.CredentialCode);
}
