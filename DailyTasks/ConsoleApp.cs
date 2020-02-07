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
            var calculateLeaderboardService = ServiceProviderProvider.GetService<CalculateLeaderBoardsService>();
            var calculateChangeService = ServiceProviderProvider.GetService<CalculateChangeService>();
            var calculateBreakdownsService = ServiceProviderProvider.GetService<CalculateBreakdownsService>();

            var cl = await calculateLeaderboardService.CalculateLeaderboards();
            await calculateChangeService.CalculateChange();
            await calculateBreakdownsService.CalculateBreakdowns();
        }
    }
}