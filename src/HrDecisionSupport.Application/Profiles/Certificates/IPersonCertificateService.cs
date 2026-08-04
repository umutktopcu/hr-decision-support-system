using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Profiles.Certificates;

public interface IPersonCertificateService
{
    Task<Result<IReadOnlyList<PersonCertificateDto>>> ListByPersonAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<Result<PersonCertificateDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PersonCertificateDto>> CreateAsync(CreatePersonCertificateRequest request, CancellationToken cancellationToken = default);
    Task<Result<PersonCertificateDto>> UpdateAsync(Guid id, UpdatePersonCertificateRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
