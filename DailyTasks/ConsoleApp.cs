using DailyTasks.Models;
using Microsoft.Extensions.DependencyInjection;
using static DailyTasks.Program;

namespace DailyTasks
{
    public class ConsoleApp
    {
        public static void Run()
        {
            var dbSettings = ServiceProviderProvider.GetService<DbSettings>();
            
            var cl = new CalculateLeaderBoards(dbSettings);
            var cc = new CalculateChange(dbSettings);
            var cb = new CalculateBreakdowns(dbSettings);
        }
    }
}