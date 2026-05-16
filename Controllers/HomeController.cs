using KerzelPay.Models;
using KerzelPay.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KerzelPay.Controllers
{
    public class HomeController : Controller
    {
        private readonly IRepository<Currency> _currencyRepo;

        public HomeController(IRepository<Currency> currencyRepo)
        {
            _currencyRepo = currencyRepo;
        }

        public async Task<IActionResult> Index()
        {
            // Pull all currencies through the repository — proves DI works
            var currencies = await _currencyRepo.GetAllAsync();
            ViewBag.Currencies = currencies;
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}