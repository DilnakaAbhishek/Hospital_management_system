using System.Text.Json;
using HospitalManagementSystem.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public sealed class PlanningCoordinatorStore(ApplicationDbContext db) : IPlanningCoordinatorStore
{
    public async Task SaveAsync(PlanningWorkflowRecord record, CancellationToken cancellationToken = default)
    {
        record.UpdatedAt = DateTimeOffset.UtcNow;
        var row = await db.AgenticExecutions.FindAsync([record.WorkflowId], cancellationToken);
        if (row == null) { row = new() { WorkflowId = record.WorkflowId, PatientId = record.PatientId }; db.AgenticExecutions.Add(row); }
        if (row.PatientId != record.PatientId) throw new InvalidOperationException("Workflow ownership cannot change.");
        row.RecordJson = JsonSerializer.Serialize(record); row.UpdatedAt = record.UpdatedAt;
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<PlanningWorkflowRecord?> GetAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var row = await db.AgenticExecutions.FindAsync([workflowId], cancellationToken);
        return row == null ? null : JsonSerializer.Deserialize<PlanningWorkflowRecord>(row.RecordJson);
    }
    public async Task<IReadOnlyList<PlanningWorkflowRecord>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        var rows = await db.AgenticExecutions.AsNoTracking().Where(x => x.PatientId == patientId).OrderByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);
        return rows.Select(x => JsonSerializer.Deserialize<PlanningWorkflowRecord>(x.RecordJson)!).ToList();
    }
    public async Task AddAuditEventAsync(string workflowId, string eventType, string description, string? metadata = null, CancellationToken cancellationToken = default)
    {
        var record = await GetAsync(workflowId, cancellationToken);
        if (record == null) return;
        record.AuditEvents.Add(new() { EventType = eventType, Description = description, Metadata = metadata });
        await SaveAsync(record, cancellationToken);
    }
    public async Task UpdateStatusAsync(string workflowId, string status, string? completedStage = null, string? error = null, CancellationToken cancellationToken = default)
    {
        var record = await GetAsync(workflowId, cancellationToken);
        if (record == null) return;
        record.Status = status;
        if (completedStage != null && !record.CompletedStages.Contains(completedStage)) record.CompletedStages.Add(completedStage);
        if (error != null) record.Errors.Add(error);
        await SaveAsync(record, cancellationToken);
    }
}
