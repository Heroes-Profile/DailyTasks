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
    public class CalculateLeaderboardsResult
    {
        public Dictionary<string, int> Heroes = new Dictionary<string, int>();
        public Dictionary<string, int> Roles = new Dictionary<string, int>();
        public Dictionary<string, int> GameTypes = new Dictionary<string, int>();
        public int Season;
    }

    class CalculateLeaderBoardsService
    {
        private readonly HeroesProfileContext _context;
        private readonly HeroesProfileCacheContext _cacheContext;


        public CalculateLeaderBoardsService(DbSettings dbSettings, HeroesProfileContext context,
                                            HeroesProfileCacheContext cacheContext)
        {
            _context = context;
            _cacheContext = cacheContext;
        }

        public async Task<CalculateLeaderboardsResult> CalculateLeaderboards()
        {
            var result = new CalculateLeaderboardsResult();

            var heroes = await _context.Heroes.Select(x => new {x.Name, x.Id, x.NewRole}).ToListAsync();

            foreach (var hero in heroes)
            {
                result.Heroes.Add(hero.Name, (int) hero.Id);
            }

            var mmrTypeIds = await (from m in _context.MmrTypeIds
                                    join h in _context.Heroes on m.Name equals h.NewRole
                                    group h by new {m.MmrTypeId, m.Name}
                                    into g
                                    select new
                                    {
                                            g.Key.MmrTypeId,
                                            g.Key.Name
                                    }).ToListAsync();

            foreach (var mmrTypeId in mmrTypeIds)
            {
                result.Roles.Add(mmrTypeId.Name, (int) mmrTypeId.MmrTypeId);
            }

            var maxCacheNumber = await _cacheContext.TableCacheValue.Where(x => x.TableToCache == "leaderboard")
                                                    .MaxAsync(x => x.CacheNumber);

            result.GameTypes.Add("qm", 1);
            result.GameTypes.Add("ud", 2);
            result.GameTypes.Add("sl", 5);

            var weeks = 1;

            var maxSeasonDatesId = await _context.SeasonDates.MaxAsync(x => x.Id);

            result.Season = (int) maxSeasonDatesId;

            var startDate = (await _context.SeasonDates.FirstOrDefaultAsync(x => x.Id == result.Season))?.StartDate ??
                            DateTime.Now;

            weeks = (int) Math.Round((DateTime.Now - startDate).TotalDays / 7, 0);

            if (weeks == 0)
            {
                weeks = 1;
            }

            foreach (var shortGameMode in result.GameTypes.Keys)
            {
                Console.WriteLine("Running Player data for " + shortGameMode);

                await GetPlayers("player", result.Season, startDate, weeks, result.GameTypes[shortGameMode], 10000,
                        maxCacheNumber);


                foreach (var hero in result.Heroes.Keys)
                {
                    Console.WriteLine("Running " + hero + " data for " + shortGameMode);

                    await GetPlayers("hero", result.Season, startDate, weeks, result.GameTypes[shortGameMode],
                            result.Heroes[hero], maxCacheNumber);
                }

                foreach (var role in result.Roles.Keys)
                {
                    Console.WriteLine("Running " + role + " data for " + shortGameMode);

                    await GetPlayers("role", result.Season, startDate, weeks, result.GameTypes[shortGameMode],
                            result.Roles[role], maxCacheNumber);
                }
            }

            await _cacheContext.TableCacheValue.AddAsync(new TableCacheValue
            {
                    TableToCache = "leaderboard",
                    DateCached = DateTime.Now
            });

            await _cacheContext.SaveChangesAsync();

            return result;
        }

        private async Task GetPlayers(string type, int season, DateTime startDate, int weeks, int gameType,
                                      int playerHeroRole, int cacheNumber)
        {
            var checkBattletags = new Dictionary<string, LeaderboardPlayerData>();

            var maxGamesPlayed = await _context.MasterGamesPlayedData
                                               .Where(x => x.TypeValue == playerHeroRole &&
                                                           x.Season == season &&
                                                           x.GameType == gameType)
                                               .OrderByDescending(x => x.GamesPlayed)
                                               .Take(weeks)
                                               .Select(x => x.GamesPlayed).AverageAsync() ?? 1;

            var masterGames = from mgpd in _context.MasterGamesPlayedData
                                                   .Where(x => x.TypeValue == playerHeroRole
                                                            && x.Season == season
                                                            && x.GameType == gameType)
                              join mmd in _context.MasterMmrData.Where(x =>
                                              x.GameType == gameType && x.TypeValue == playerHeroRole)
                                      on new {mgpd.BlizzId, mgpd.Region} equals
                                      new {mmd.BlizzId, mmd.Region}
                              join b in _context.Battletags
                                      on new {mgpd.BlizzId, mgpd.Region} equals
                                      new {BlizzId = (uint) b.BlizzId, Region = (byte) b.Region}
                              select new
                              {
                                      SplitBattleTag = "",
                                      b.Battletag,
                                      b.LatestGame,
                                      mgpd.BlizzId,
                                      mgpd.Region,
                                      WinRate = mgpd.Win / (mgpd.Win + mgpd.Loss) * 100,
                                      mgpd.Win,
                                      mgpd.Loss,
                                      mgpd.GamesPlayed,
                                      mmd.ConservativeRating,
                                      Rating = (mgpd.Win / (mgpd.Win + mgpd.Loss) * 100 - 50) *
                                               (mgpd.GamesPlayed / maxGamesPlayed) + mmd.ConservativeRating / 10
                              };
            masterGames = masterGames.Where(x => x.WinRate >= 50).OrderByDescending(x => x.Rating);

            switch (type)
            {
                case "player":
                    masterGames = masterGames.Where(x => x.GamesPlayed >= 5 * weeks);
                    break;
                case "hero":
                case "role":
                    masterGames = masterGames.Where(x => x.GamesPlayed >= 2 * weeks);
                    break;
            }

            var rankCounter = 1;

            foreach (var game in masterGames)
            {
                var key = game.BlizzId + "|" + game.Region;
                if (checkBattletags.ContainsKey(key))
                {
                    var inDict = checkBattletags[key].latest_game_played;

                    var data = new LeaderboardPlayerData
                    {
                            game_type = gameType,
                            season = season,
                            player_hero_role = playerHeroRole,
                            rank_counter = checkBattletags[key].rank_counter,
                            split_battletag = game.Battletag.Split('#')[0],
                            battletag = game.Battletag,
                            blizz_id = (int) game.BlizzId,
                            region = game.Region,
                            win_rate = (double) game.WinRate,
                            win = (int) game.Win,
                            loss = (int) game.Loss,
                            latest_game_played = game.LatestGame,
                            games_played = (int) game.GamesPlayed,
                            conservative_rating = game.ConservativeRating,
                            rating = (double) game.Rating,
                            cache_number = cacheNumber
                    };
                    checkBattletags[key] = data;

                    if (inDict >= game.LatestGame)
                    {
                        data.rank_counter = rankCounter;
                        rankCounter++;
                    }
                }
            }

            foreach (var (key, value) in checkBattletags)
            {
                _cacheContext.Leaderboard.Add(new Leaderboard
                {
                        GameType = value.game_type,
                        Season = value.season,
                        Type = value.player_hero_role,
                        Rank = value.rank_counter,
                        SplitBattletag = value.split_battletag,
                        Battletag = value.battletag,
                        BlizzId = value.blizz_id,
                        Region = (sbyte) value.region,
                        WinRate = value.win_rate,
                        Win = value.win,
                        Loss = value.loss,
                        GamesPlayed = value.games_played,
                        ConservativeRating = value.conservative_rating,
                        Rating = value.rating,
                        CacheNumber = value.cache_number,

                });
            }

            await _cacheContext.SaveChangesAsync();
        }
    }
}
