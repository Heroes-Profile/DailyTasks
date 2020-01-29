using System.Threading.Tasks;
using DailyTasks.Models;
using Microsoft.Extensions.DependencyInjection;
using static DailyTasks.Program;

namespace DailyTasks
{
    public class ConsoleApp
    {
        public static async Task Run()
        {
            var dbSettings = ServiceProviderProvider.GetService<DbSettings>();
            var calculateLeaderboardService = ServiceProviderProvider.GetService<CalculateLeaderBoardsService>();
            var calculateChangeService = ServiceProviderProvider.GetService<CalculateChangeService>();

            var cl = await calculateLeaderboardService.CalculateLeaderboards();
            await calculateChangeService.CalculateChange();
            var cb = new CalculateBreakdowns(dbSettings);
        }
    }
}