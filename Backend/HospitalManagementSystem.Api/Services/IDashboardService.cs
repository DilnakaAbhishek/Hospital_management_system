using System.Threading.Tasks;
using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services
{
    public interface IDashboardService
    {
        Task<AdminDashboardDto> GetAdminDashboardStatsAsync();
    }
}
