using CryptoBlade.Services;
using CryptoBlade.Strategies.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CryptoBlade.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> m_logger;
        private readonly ITradeStrategyManager m_strategyManager;

        public ITradingStrategy[] Strategies { get; set; }

        public IndexModel(ILogger<IndexModel> logger, ITradeStrategyManager strategyManager)
        {
            m_logger = logger;
            m_strategyManager = strategyManager;
            Strategies = Array.Empty<ITradingStrategy>();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var strategies = await m_strategyManager.GetStrategiesAsync(CancellationToken.None);
            Strategies = strategies.OrderBy(x => x.Symbol).ToArray();

            return Page();
        }
    }
}