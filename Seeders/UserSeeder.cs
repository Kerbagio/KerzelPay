using KerzelPay.Constants;
using KerzelPay.Models;
using Microsoft.AspNetCore.Identity;

namespace KerzelPay.Seeders
{
    public static class UserSeeder
    {
        public static async Task SeedUsersAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // --- Default Admin ---
            const string adminEmail = "admin@kerzelpay.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Kerzel",
                    LastName = "Admin",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(admin, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, Roles.Admin);
                }
            }

            // --- Default test User ---
            const string userEmail = "user@kerzelpay.com";
            if (await userManager.FindByEmailAsync(userEmail) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = userEmail,
                    Email = userEmail,
                    FirstName = "John",
                    LastName = "Doe",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, "User@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, Roles.User);
                }
            }

            // --- Demo agents (Beirut area) ---
            var demoAgents = new[]
            {
    new { Email = "agent1@kerzelpay.com", First = "Hamra", Last = "Express",   Store = "Hamra Express",     Address = "Hamra Street, Beirut",      Lat = 33.8959m, Lng = 35.4789m },
    new { Email = "agent2@kerzelpay.com", First = "Achrafieh", Last = "Pay",    Store = "Achrafieh Pay",     Address = "Sassine Square, Beirut",    Lat = 33.8862m, Lng = 35.5170m },
    new { Email = "agent3@kerzelpay.com", First = "Jounieh", Last = "Cash",     Store = "Jounieh Cash Point", Address = "Maameltein, Jounieh",       Lat = 33.9817m, Lng = 35.6178m },
};

            foreach (var d in demoAgents)
            {
                if (await userManager.FindByEmailAsync(d.Email) == null)
                {
                    var u = new ApplicationUser
                    {
                        UserName = d.Email,
                        Email = d.Email,
                        FirstName = d.First,
                        LastName = d.Last,
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(u, "Agent@123");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(u, Roles.User);
                        await userManager.AddToRoleAsync(u, Roles.Agent);
                    }
                }
            }
        }
    }
}