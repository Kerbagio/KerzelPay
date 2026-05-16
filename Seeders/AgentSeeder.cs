using KerzelPay.Data;
using KerzelPay.Models;
using Microsoft.AspNetCore.Identity;

namespace KerzelPay.Seeders
{
    public static class AgentSeeder
    {
        public static async Task SeedAgentsAsync(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            if (db.Agents.Any()) return;

            var demoAgents = new[]
            {
                new { Email = "agent1@kerzelpay.com", Store = "Hamra Express",      Address = "Hamra Street, Beirut",    Hours = "Mon-Sat 9:00-20:00", Lat = 33.8959m, Lng = 35.4789m },
                new { Email = "agent2@kerzelpay.com", Store = "Achrafieh Pay",      Address = "Sassine Square, Beirut",  Hours = "Daily 10:00-22:00",  Lat = 33.8862m, Lng = 35.5170m },
                new { Email = "agent3@kerzelpay.com", Store = "Jounieh Cash Point", Address = "Maameltein, Jounieh",     Hours = "Mon-Fri 8:00-18:00", Lat = 33.9817m, Lng = 35.6178m },
            };

            foreach (var d in demoAgents)
            {
                var user = await userManager.FindByEmailAsync(d.Email);
                if (user == null) continue;

                db.Agents.Add(new Agent
                {
                    UserId = user.Id,
                    StoreName = d.Store,
                    Address = d.Address,
                    WorkingHours = d.Hours,
                    Latitude = d.Lat,
                    Longitude = d.Lng,
                    Status = AgentStatus.Approved,
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                });
            }

            await db.SaveChangesAsync();
        }
    }
}