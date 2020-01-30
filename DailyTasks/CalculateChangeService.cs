using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DailyTasks.Models;
using HeroesProfileDb.HeroesProfile;
using HeroesProfileDb.HeroesProfileCache;
using Microsoft.EntityFrameworkCore;

namespace DailyTasks
{
    class CalculateChangeService
    {
        private readonly HeroesProfileContext _context;
        private readonly HeroesProfileCacheContext _cacheContext;

        public CalculateChangeService(HeroesProfileContext context, HeroesProfileCacheContext cacheContext)
        {
            _context = context;
            _cacheContext = cacheContext;
        }

        public async Task CalculateChange()
        {
            //Get major versions ('2.43') from SeasonGameVersions table
            var patches = (await _context.SeasonGameVersions
                                        .Select(x => x.GameVersion)
                                        .ToListAsync())
                                        .Where(x => string.Compare(x, "2.43", StringComparison.Ordinal) > 0)
                                        .ToList();

            //Split the full version to just the major version
            var majorPatches = patches
                               .Select(x => x.Split('.')[0] + "." + x.Split('.')[1])
                               .Distinct()
                               .OrderBy(x => x);

            // Combine the list of short versions and full versions
            patches.AddRange(majorPatches);
            patches = patches.OrderBy(x => x).Distinct().ToList();

            var gameTypes = new List<string>
            {
                    "5", "1", "2"
            };

            foreach (var patch in patches)
            {
                foreach (var gameType in gameTypes)
                {
                    var globalResults = new Dictionary<string, ChangeData>();
                    var totalGames = 0;

                    var heroStats = await _context.GlobalHeroStats
                                                  .Where(x => x.GameVersion.StartsWith(patch)
                                                  && x.GameType == Convert.ToSByte(gameType))
                                                  .GroupBy(x => new {x.Hero, x.WinLoss})
                                                  .Select(x => new
                                                  {
                                                          x.Key.Hero, x.Key.WinLoss,
                                                          GamesPlayed = x.Sum(y => y.GamesPlayed)
                                                  })
                                                  .OrderBy(x => x.Hero)
                                                  .ThenBy(x => x.WinLoss).ToListAsync();

                    foreach (var heroStat in heroStats)
                    {
                        var hero = heroStat.Hero.ToString();
                        if (globalResults.ContainsKey(hero))
                        {
                            if (heroStat.WinLoss == 1)
                            {
                                globalResults[hero].wins = heroStat.GamesPlayed;
                            }
                            else
                            {
                                globalResults[hero].losses = heroStat.GamesPlayed;
                            }

                            totalGames += (int) heroStat.GamesPlayed;
                        }
                        else
                        {
                            var cd = new ChangeData
                            {
                                    wins = 0,
                                    losses = 0,
                                    win_rate = 0,
                                    games_played = 0,
                                    bans = 0,
                                    ban_rate = 0,
                                    popularity = 0
                            };
                            if (heroStat.WinLoss == 1)
                            {
                                cd.wins = heroStat.GamesPlayed;
                            }
                            else
                            {
                                cd.losses = heroStat.GamesPlayed;
                            }

                            totalGames += (int) heroStat.GamesPlayed;
                            globalResults[hero] = cd;
                        }
                    }

                    var heroBans = await _context.GlobalHeroStatsBans.Where(x => x.GameVersion.StartsWith(patch)
                                                                              && x.GameType == Convert.ToSByte(gameType))
                                                 .GroupBy(x => x.Hero)
                                                 .Select(x => new {Hero = x.Key, Bans = x.Sum(x => x.Bans)})
                                                 .OrderBy(x => x.Hero)
                                                 .ToListAsync();

                    foreach (var heroBan in heroBans)
                    {
                        if (globalResults.ContainsKey(heroBan.Hero.ToString()))
                        {
                            globalResults[heroBan.Hero.ToString()].bans = (double) heroBan.Bans;
                        }
                        else
                        {
                            globalResults.Add(heroBan.Hero.ToString(), new ChangeData {bans = (double) heroBan.Bans});
                        }
                    }

                    totalGames /= 10;


                    foreach (var hero in globalResults.Keys)
                    {
                        var d = globalResults[hero];
                        d.games_played = d.wins + d.losses;

                        if (d.wins == 0 && d.losses == 0)
                        {
                            d.win_rate = 0;

                        }
                        else if (d.wins != 0 && d.losses == 0)
                        {
                            d.win_rate = 100;

                        }
                        else if (d.wins == 0 && d.losses != 0)
                        {
                            d.win_rate = 0;
                        }
                        else
                        {
                            d.win_rate = d.wins / d.games_played * 100;
                        }


                        if (d.bans == 0)
                        {
                            d.popularity = d.games_played / totalGames * 100;
                            d.ban_rate = 0;
                        }
                        else
                        {
                            d.popularity = (d.games_played + d.bans) / totalGames * 100;
                            d.ban_rate = d.bans / totalGames * 100;
                        }

                        var globalHeroChange = new GlobalHeroChange
                        {
                                GameVersion = patch,
                                GameType = Convert.ToSByte(gameType),
                                Hero = Convert.ToSByte(hero),
                                WinRate = d.win_rate,
                                Popularity = d.popularity,
                                BanRate = d.ban_rate,
                                GamesPlayed = (int) d.games_played,
                                Wins = (int) d.wins,
                                Losses = (int) d.losses,
                                Bans = (int) d.bans
                        };

                        await _cacheContext.GlobalHeroChange.Upsert(globalHeroChange)
                                           .WhenMatched(x => new GlobalHeroChange
                                           {
                                                   WinRate = globalHeroChange.WinRate,
                                                   Popularity = globalHeroChange.Popularity,
                                                   BanRate = globalHeroChange.BanRate,
                                                   GamesPlayed = globalHeroChange.GamesPlayed,
                                                   Wins = globalHeroChange.Wins,
                                                   Losses = globalHeroChange.Losses,
                                                   Bans = globalHeroChange.Bans
                                           }).RunAsync();
                    }
                }
            }
        }
    }
}