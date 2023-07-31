using CryptoBlade.Services;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Wallet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CryptoBlade.Pages
{
    public class WalletModel : PageModel
    {
        private readonly ILogger<IndexModel> m_logger;
        private readonly IWalletManager m_strategyManager;

        public Balance Contract { get; set; }

        public WalletModel(ILogger<IndexModel> logger, IWalletManager strategyManager)
        {
            m_logger = logger;
            m_strategyManager = strategyManager;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Contract = m_strategyManager.Contract;
            return Page();
        }
    }
}