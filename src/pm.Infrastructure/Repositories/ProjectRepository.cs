using Dapper;
using pm.Application.Interfaces;
using pm.Domain.Entities;

namespace pm.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly DapperContext _context;

    public ProjectRepository(DapperContext context)
    {
        _context = context;
    }

    private const string SelectColumns = """
        id,
        user_id             AS UserId,
        client_id           AS ClientId,
        name,
        description,
        agreed_scope        AS AgreedScope,
        status::text        AS Status,
        pricing_type::text  AS PricingType,
        agreed_amount       AS AgreedAmount,
        currency,
        vat_rate            AS VatRate,
        payment_terms_days  AS PaymentTermsDays,
        starts_on           AS StartsOn,
        due_on              AS DueOn,
        work_completed_at   AS WorkCompletedAt,
        invoiced_at         AS InvoicedAt,
        completed_at        AS CompletedAt,
        cancelled_at        AS CancelledAt,
        created_at          AS CreatedAt,
        updated_at          AS UpdatedAt,
        deleted_at          AS DeletedAt
        """;

    public async Task<IReadOnlyList<Project>> GetAllByUserIdAsync(Guid userId)
    {
        using var conn = _context.CreateConnection();
        var result = await conn.QueryAsync<Project>(
            $"""
            SELECT {SelectColumns}
            FROM projects
            WHERE user_id = @UserId AND deleted_at IS NULL
            ORDER BY created_at DESC
            """,
            new { UserId = userId });
        return result.ToList();
    }

    public async Task<Project?> GetByIdAsync(Guid userId, Guid id)
    {
        using var conn = _context.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Project>(
            $"""
            SELECT {SelectColumns}
            FROM projects
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new { Id = id, UserId = userId });
    }

    public async Task<Project> CreateAsync(Project project)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            INSERT INTO projects (
                id, user_id, client_id, name, description, agreed_scope,
                status, pricing_type, agreed_amount, currency, vat_rate,
                payment_terms_days, starts_on, due_on,
                work_completed_at, invoiced_at, completed_at, cancelled_at,
                created_at, updated_at
            ) VALUES (
                @Id, @UserId, @ClientId, @Name, @Description, @AgreedScope,
                @Status::project_status, @PricingType::project_pricing_type,
                @AgreedAmount, @Currency, @VatRate,
                @PaymentTermsDays, @StartsOn, @DueOn,
                @WorkCompletedAt, @InvoicedAt, @CompletedAt, @CancelledAt,
                @CreatedAt, @UpdatedAt
            )
            """, project);
        return project;
    }

    public async Task UpdateAsync(Project project)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE projects SET
                client_id           = @ClientId,
                name                = @Name,
                description         = @Description,
                agreed_scope        = @AgreedScope,
                status              = @Status::project_status,
                pricing_type        = @PricingType::project_pricing_type,
                agreed_amount       = @AgreedAmount,
                currency            = @Currency,
                vat_rate            = @VatRate,
                payment_terms_days  = @PaymentTermsDays,
                starts_on           = @StartsOn,
                due_on              = @DueOn,
                work_completed_at   = @WorkCompletedAt,
                invoiced_at         = @InvoicedAt,
                completed_at        = @CompletedAt,
                cancelled_at        = @CancelledAt,
                updated_at          = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """, project);
    }

    public async Task SoftDeleteAsync(Guid userId, Guid id)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE projects
            SET deleted_at = NOW(), updated_at = NOW()
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new { Id = id, UserId = userId });
    }

    public async Task AddStatusHistoryAsync(Guid projectId, Guid changedByUserId, string? fromStatus, string toStatus)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            INSERT INTO project_status_history (id, project_id, changed_by_user_id, from_status, to_status, created_at)
            VALUES (@Id, @ProjectId, @ChangedByUserId,
                    @FromStatus::project_status,
                    @ToStatus::project_status,
                    NOW())
            """,
            new
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ChangedByUserId = changedByUserId,
                FromStatus = fromStatus,
                ToStatus = toStatus
            });
    }
}
