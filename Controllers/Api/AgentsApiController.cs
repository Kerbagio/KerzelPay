using KerzelPay.Data;
using KerzelPay.Dtos;
using KerzelPay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Controllers.Api
{
    [ApiController]
    [Route("api/agents")]
    [Produces("application/json")]
    public class AgentsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public AgentsApiController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>Public — list approved agents for map display.</summary>
        [HttpGet("map")]
        [ProducesResponseType(typeof(IEnumerable<AgentDto>), 200)]
        public async Task<IActionResult> GetApprovedAgents()
        {
            var agents = await _db.Agents
                .Where(a => a.Status == AgentStatus.Approved)
                .Select(a => new AgentDto
                {
                    Id = a.Id,
                    StoreName = a.StoreName,
                    Address = a.Address,
                    Latitude = a.Latitude,
                    Longitude = a.Longitude,
                    WorkingHours = a.WorkingHours,
                    Status = a.Status.ToString()
                })
                .ToListAsync();

            return Ok(agents);
        }
    }
}