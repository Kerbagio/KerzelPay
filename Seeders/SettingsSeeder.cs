using KerzelPay.Data;
using KerzelPay.Models;

namespace KerzelPay.Seeders
{
    public static class SettingsSeeder
    {
        public static void SeedDefaults(ApplicationDbContext db)
        {
            if (!db.AppSettings.Any(s => s.Key == "CommissionPercent"))
            {
                db.AppSettings.Add(new AppSetting
                {
                    Key = "CommissionPercent",
                    Value = "1.0",
                    UpdatedAt = DateTime.UtcNow
                });
            }

            if (!db.AppSettings.Any(s => s.Key == "AgentCommissionPercent"))
            {
                db.AppSettings.Add(new AppSetting
                {
                    Key = "AgentCommissionPercent",
                    Value = "0.5",   // 0.5% default
                    UpdatedAt = DateTime.UtcNow
                });
            }

            db.SaveChanges();
        }
    }
}