using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DailyTasks.Models;
using HeroesProfileDb.HeroesProfile;
using Microsoft.EntityFrameworkCore;

namespace DailyTasks
{
    public class GetHeroesListResult
    {
        public Dictionary<string, string> HeroesList = new Dictionary<string, string>();
        public Dictionary<string, string> HeroesListShort = new Dictionary<string, string>();
        public Dictionary<string, string> MmrTypeIds = new Dictionary<string, string>();
        public Dictionary<string, string> MmrTypeNames = new Dictionary<string, string>();
    }

    internal class CalculateBreakdownsService
    {
        private readonly HeroesProfileContext _context;

        private const double Bronze = .12;
        private const double Silver = .23;
        private const double Gold = .26;
        private const double Platinum = .22;
        private const double Diamond = .12;
        private const double Master = .05;
        private const double Grandmaster = 200;

        private const double MinGamesPlayed = 25;
        private const double MinGamesPlayedHero = 5;

        readonly Dictionary<string, string> _leagueList = new Dictionary<string, string>()
        {
                {"5", "sl"},
                {"3", "hl"},
                {"1", "qm"},
                {"4", "tl"},
                {"2", "ud"},
        };

        public CalculateBreakdownsService(HeroesProfileContext context)
        {
            _context = context;
        }

        public async Task CalculateBreakdowns()
        {
            var heroesList = await GetHeroesList();

            var roleList = new Dictionary<string, string>
            {
                    {"Support", "Support"},
                    {"Melee Assassin", "Melee Assassin"},
                    {"Tank", "Tank"},
                    {"Bruiser", "Bruiser"},
                    {"Healer", "Healer"},
                    {"Ranged Assassin", "Ranged Assassin"}
            };

            foreach (var leagueItem in _leagueList.Keys)
            {
                foreach (var item in heroesList.HeroesList.Keys)
                {
                    await CalculateLeagues(heroesList.MmrTypeNames[heroesList.HeroesListShort[item]], leagueItem, "all", heroesList.MmrTypeIds);
                }

                await CalculateLeagues("10000", leagueItem, "all", heroesList.MmrTypeIds);

            }

            foreach (var leagueItem in _leagueList.Keys)
            {
                foreach (var role in roleList.Keys)
                {
                    await CalculateLeagues(heroesList.MmrTypeNames[role], leagueItem, "all", heroesList.MmrTypeIds);
                }
            }

        }

        private async Task<GetHeroesListResult> GetHeroesList()
        {
            var result = new GetHeroesListResult();

            var names = await _context.Heroes.Select(x => new {x.Name, x.ShortName}).ToListAsync();

            foreach (var name in names)
            {
                result.HeroesListShort.Add(name.ShortName ?? "", name.Name ?? "");
                result.HeroesList.Add(name.ShortName ?? "", name.Name ?? "");

                Console.WriteLine(name.ShortName ?? "");
            }

            var mmrTypeIds = await _context.MmrTypeIds.ToListAsync();

            foreach (var mmrTypeId in mmrTypeIds)
            {
                result.MmrTypeIds.Add(mmrTypeId.MmrTypeId.ToString(), mmrTypeId.Name ?? "");
                result.MmrTypeNames.Add(mmrTypeId.Name ?? "", mmrTypeId.MmrTypeId.ToString());
            }

            return result;
        }

        private async Task CalculateLeagues(string type, string gameType, string season,
                                            Dictionary<string, string> mmrTypeIds)
        {
            var gamesPlayedInt = type == "10000" ? MinGamesPlayed : MinGamesPlayedHero;

            var totalPlayers = await _context.MasterMmrData.Where(x =>
                                                     x.TypeValue == Convert.ToInt32(type) &&
                                                     x.GameType == Convert.ToByte(gameType))
                                             .GroupBy(x => new {x.TypeValue, x.GameType, x.BlizzId, x.Region})
                                             .Select(x => new
                                             {
                                                     x.Key.TypeValue,
                                                     x.Key.GameType,
                                                     x.Key.BlizzId,
                                                     x.Key.Region,
                                                     Win = x.Sum(x => x.Win),
                                                     Loss = x.Sum(x => x.Loss)
                                             })
                                             .Where(x => x.Win + x.Loss > gamesPlayedInt)
                                             .CountAsync();

            if (totalPlayers <= 15) return;
            {
                var bronzeTotal = Math.Floor(totalPlayers * Bronze);
                if (bronzeTotal == 0)
                {
                    bronzeTotal = 1;
                }

                var silverTotal = Math.Floor(totalPlayers * Silver);
                if (bronzeTotal == 0)
                {
                    silverTotal = 1;
                }

                var goldTotal = Math.Floor(totalPlayers * Gold);
                if (goldTotal == 0)
                {
                    goldTotal = 1;
                }

                var platinumTotal = Math.Floor(totalPlayers * Platinum);
                if (platinumTotal == 0)
                {
                    platinumTotal = 1;
                }

                var diamondTotal = Math.Floor(totalPlayers * Diamond);
                if (diamondTotal == 0)
                {
                    diamondTotal = 1;
                }

                var masterTotal = Math.Floor(totalPlayers * Master);
                if (masterTotal == 0)
                {
                    masterTotal = 1;
                }

                double[] bronzePlayers = {0, bronzeTotal};
                double[] silverPlayers = {bronzePlayers[1], silverTotal};
                double[] goldPlayers = {silverPlayers[1] + bronzePlayers[1], goldTotal};
                double[] platinumPlayers = {goldPlayers[1] + silverPlayers[1] + bronzePlayers[1], platinumTotal};
                double[] diamondPlayers =
                        {platinumPlayers[1] + goldPlayers[1] + silverPlayers[1] + bronzePlayers[1], diamondTotal};
                double[] masterPlayers =
                {
                        diamondPlayers[1] + platinumPlayers[1] + goldPlayers[1] + silverPlayers[1] + bronzePlayers[1],
                        masterTotal
                };

                var mmrList = new Dictionary<string, double[]>
                {
                        {"2", silverPlayers},
                        {"3", goldPlayers},
                        {"4", platinumPlayers},
                        {"5", diamondPlayers},
                        {"6", masterPlayers}
                };

                // mmr_list.Add("1", bronzePlayers);


                var leagueTierNames = new Dictionary<string, string>
                {
                        {"1", "bronze"},
                        {"2", "silver"},
                        {"3", "gold"},
                        {"4", "platinum"},
                        {"5", "diamond"},
                        {"6", "master"}
                };
                foreach (var item in mmrList.Keys)
                {
                    var gamesPlayed = type == "10000" ? MinGamesPlayed : MinGamesPlayedHero;

                    //Min to get into silver
                    double minMmr = 0;

                    var minConservativeRating = await _context.MasterMmrData.Where(x =>
                                                                      x.TypeValue == Convert.ToInt32(type) &&
                                                                      x.GameType == Convert.ToByte(gameType)
                                                                   && x.Win + x.Loss > gamesPlayed)
                                                              .OrderBy(x => x.ConservativeRating)
                                                              .ThenBy(x => x.Win + x.Loss)
                                                              .Skip((int) mmrList[item][0])
                                                              .Take((int) mmrList[item][1])
                                                              .MinAsync(x => x.ConservativeRating);

                    minMmr = 1800 + 40 * minConservativeRating;

                    Console.WriteLine("For type " + mmrTypeIds[type] + " in league " + _leagueList[gameType] + " |" +
                                      leagueTierNames[item] + " < " + minMmr);

                    var leagueBreakdown = new LeagueBreakdowns
                    {
                            TypeRoleHero = Convert.ToInt32(type),
                            GameType = Convert.ToSByte(gameType),
                            LeagueTier = Convert.ToSByte(item),
                            MinMmr = minMmr
                    };

                    await _context.LeagueBreakdowns.Upsert(leagueBreakdown).WhenMatched(x => new LeagueBreakdowns
                    {
                            TypeRoleHero = leagueBreakdown.TypeRoleHero,
                            GameType = leagueBreakdown.GameType,
                            LeagueTier = leagueBreakdown.LeagueTier,
                            MinMmr = leagueBreakdown.MinMmr
                    }).RunAsync();
                }
            }
        }
    }
}
