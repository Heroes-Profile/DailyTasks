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

            var cl = await calculateLeaderboardService.CalculateLeaderboards();
            var cc = new CalculateChange(dbSettings);
            var cb = new CalculateBreakdowns(dbSettings);
        }
    }
}