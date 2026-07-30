using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Infrastructure.Persistence;

public class HrDecisionSupportDbContext : DbContext
{
    public HrDecisionSupportDbContext(DbContextOptions<HrDecisionSupportDbContext> options)
        : base(options)
    {
    }
}
