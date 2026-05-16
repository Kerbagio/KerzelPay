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
                    Value = "1.0",   // 1% default
                    UpdatedAt = DateTime.UtcNow
                });
            }

            db.SaveChanges();
        }
    }
}