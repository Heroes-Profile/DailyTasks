using System;
using System.Collections.Generic;
using DailyTasks.Models;
using MySql.Data.MySqlClient;

namespace DailyTasks
{
    class CalculateLeaderBoards
    {
        private readonly DbSettings _dbSettings;
        private readonly string _connectionString;
        private Dictionary<string, int> _heroes = new Dictionary<string, int>();
        private Dictionary<string, int> _roles = new Dictionary<string, int>();
        private Dictionary<string, int> _gameTypes = new Dictionary<string, int>();
        private int _season;
        public CalculateLeaderBoards(DbSettings dbSettings)
        {
            _dbSettings = dbSettings;
            _connectionString = ConnectionStringBuilder.BuildConnectionString(_dbSettings);
            var cacheNumber = 0;
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT name,id, new_role FROM heroes";
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        _heroes.Add(reader.GetString("name"), reader.GetInt32("id"));
                    }
                }


                using (var cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT mmr_type_id, mmr_type_ids.name FROM heroesprofile.mmr_type_ids inner join heroes on heroes.new_role = mmr_type_ids.name group by mmr_type_id, mmr_type_ids.name";
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        _roles.Add(reader.GetString("name"), reader.GetInt32("mmr_type_id"));
                    }
                }
                using (var cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT MAX(cache_number) as max_cache_number FROM heroesprofile_cache.table_cache_value where table_to_cache = 'leaderboard'";
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        cacheNumber = (reader.GetInt32("max_cache_number") + 1);
                    }
                }

            }
            _gameTypes.Add("qm", 1);
            _gameTypes.Add("ud", 2);
            _gameTypes.Add("sl", 5);


            var startDate = DateTime.Now;
            var weeks = 1;

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT MAX(id) as max_id FROM heroesprofile.season_dates";
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        _season = reader.GetInt32("max_id");
                    }
                }


                using (var cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT start_date FROM heroesprofile.season_dates where id = " + _season;
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        startDate = reader.GetDateTime("start_date");
                    }
                }


                using (var cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT ROUND(DATEDIFF(NOW(), '" + startDate.ToString("yyyy-MM-dd HH:mm:ss") + "')/7, 0) AS weeksout";
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        weeks = reader.GetInt32("weeksout");
                    }
                }
            }
            if (weeks == 0)
            {
                weeks = 1;
            }

            foreach (var shortGameMode in _gameTypes.Keys)
            {
                Console.WriteLine("Running Player data for " + shortGameMode); ;

                GetPlayers("player", _season, startDate, weeks, _gameTypes[shortGameMode], 10000, cacheNumber);


                foreach (var hero in _heroes.Keys)
                {
                    Console.WriteLine("Running " + hero + " data for " + shortGameMode); ;

                    GetPlayers("hero", _season, startDate, weeks, _gameTypes[shortGameMode], _heroes[hero], cacheNumber);
                }

                foreach (var role in _roles.Keys)
                {
                    Console.WriteLine("Running " + role + " data for " + shortGameMode); ;

                    GetPlayers("role", _season, startDate, weeks, _gameTypes[shortGameMode], _roles[role], cacheNumber);
                }
            }


            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT into heroesprofile_cache.table_cache_value (table_to_cache, date_cached) VALUES ('leaderboard', " + "\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"" + ")";
                var reader = cmd.ExecuteReader();
            }
        }

        private void GetPlayers(string type, int season, DateTime startDate, int weeks, int gameType, int playerHeroRole, int cacheNumber)
        {
            var checkBattletags = new Dictionary<string, LeaderboardPlayerData>();

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var maxGamesPlayed = 0;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT if(AVG(games_played) is null, 1,AVG(games_played))  as max_games_played FROM (SELECT games_played FROM heroesprofile.master_games_played_data where type_value = " + playerHeroRole + " and season = " + season + " and game_type = " + gameType + " ORDER BY games_played DESC LIMIT " + weeks + ") as data";

                //Console.WriteLine(cmd.CommandText);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    maxGamesPlayed = Convert.ToInt32(Math.Floor(reader.GetDouble("max_games_played")));
                }
            }

            var insertString = "";
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT " +
                                  "SUBSTRING_INDEX(battletags.battletag, '#', 1) as split_battletag, " +
                                  "battletags.battletag, " +
                                  "battletags.latest_game, " +
                                  "master_games_played_data.blizz_id, " +
                                  "master_games_played_data.region, " +
                                  "(master_games_played_data.win / (master_games_played_data.win + master_games_played_data.loss)) * 100 AS win_rate, " +
                                  "master_games_played_data.win, " +
                                  "master_games_played_data.loss, " +
                                  "master_games_played_data.games_played, " +
                                  "master_mmr_data.conservative_rating, " +
                                  "50 + (((master_games_played_data.win / (master_games_played_data.win + master_games_played_data.loss)) * 100) - 50) * (master_games_played_data.games_played / " + maxGamesPlayed + ") + (master_mmr_data.conservative_rating / 10) as rating " +
                                  "FROM " +
                                  "master_games_played_data " +
                                  "INNER JOIN " +
                                  "master_mmr_data ON (master_mmr_data.type_value = " + playerHeroRole +
                                  " AND master_mmr_data.game_type = " + gameType +
                                  " AND master_mmr_data.blizz_id = master_games_played_data.blizz_id " +
                                  "AND master_mmr_data.region = master_games_played_data.region) " +
                                  "INNER JOIN " +
                                  "battletags ON (battletags.blizz_id = master_games_played_data.blizz_id " +
                                  "AND battletags.region = master_games_played_data.region) " +
                                  "WHERE " +
                                  "master_games_played_data.type_value = " + playerHeroRole +
                                  " AND master_games_played_data.season = " + season +
                                  " AND master_games_played_data.game_type = " + gameType;

                cmd.CommandText += " AND master_games_played_data.games_played >=  ";

                switch (type)
                {
                    case "player":
                        cmd.CommandText += (5 * weeks);
                        break;
                    case "hero":
                    case "role":
                        cmd.CommandText += (2 * weeks);
                        break;
                }
                cmd.CommandText += " HAVING win_rate >= 50 " +
                                   "ORDER BY rating DESC ";//LIMIT 500";

                var rankCounter = 1;
                //Console.WriteLine(cmd.CommandText);
                cmd.CommandTimeout = 0;
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (checkBattletags.ContainsKey(reader.GetInt32("blizz_id") + "|" + reader.GetInt32("region")))
                    {
                        var inDict = checkBattletags[reader.GetInt32("blizz_id") + "|" + reader.GetInt32("region")].latest_game_played;
                        var compare = reader.GetDateTime("latest_game");
                        if (checkBattletags[reader.GetInt32("blizz_id") + "|" + reader.GetInt32("region")].latest_game_played < reader.GetDateTime("latest_game"))
                        {
                            var data = new LeaderboardPlayerData
                            {
                                    game_type = gameType,
                                    season = season,
                                    player_hero_role = playerHeroRole,
                                    rank_counter = checkBattletags[reader.GetInt32("blizz_id") + "|" + reader.GetInt32("region")].rank_counter,
                                    split_battletag = reader.GetString("split_battletag"),
                                    battletag = reader.GetString("battletag"),
                                    blizz_id = reader.GetInt32("blizz_id"),
                                    region = reader.GetInt32("region"),
                                    win_rate = reader.GetDouble("win_rate"),
                                    win = reader.GetInt32("win"),
                                    loss = reader.GetInt32("loss"),
                                    latest_game_played = reader.GetDateTime("latest_game"),
                                    games_played = reader.GetInt32("games_played"),
                                    conservative_rating = reader.GetDouble("conservative_rating"),
                                    rating = reader.GetDouble("rating"),
                                    cache_number = cacheNumber
                            };

                            checkBattletags[reader.GetInt32("blizz_id") + "|" + reader.GetInt32("region")] = data;
                        }
                    }
                    else
                    {
                        var data = new LeaderboardPlayerData
                        {
                                game_type = gameType,
                                season = season,
                                player_hero_role = playerHeroRole,
                                rank_counter = rankCounter,
                                split_battletag = reader.GetString("split_battletag"),
                                battletag = reader.GetString("battletag"),
                                blizz_id = reader.GetInt32("blizz_id"),
                                region = reader.GetInt32("region"),
                                win_rate = reader.GetDouble("win_rate"),
                                win = reader.GetInt32("win"),
                                loss = reader.GetInt32("loss"),
                                games_played = reader.GetInt32("games_played"),
                                latest_game_played = reader.GetDateTime("latest_game"),
                                conservative_rating = reader.GetDouble("conservative_rating"),
                                rating = reader.GetDouble("rating"),
                                cache_number = cacheNumber
                        };

                        checkBattletags[reader.GetInt32("blizz_id") + "|" + reader.GetInt32("region")] = data;
                        rankCounter++;

                    }
                }
            }

            foreach (var item in checkBattletags.Keys)
            {

                if (insertString == "")
                {
                    insertString = "(" +
                                   checkBattletags[item].game_type + "," +
                                   checkBattletags[item].season + "," +
                                   checkBattletags[item].player_hero_role + "," +
                                   checkBattletags[item].rank_counter + "," +
                                   "\"" + checkBattletags[item].split_battletag + "\"" + "," +
                                   "\"" + checkBattletags[item].battletag + "\"" + "," +
                                   checkBattletags[item].blizz_id + "," +
                                   checkBattletags[item].region + "," +
                                   checkBattletags[item].win_rate + "," +
                                   checkBattletags[item].win + "," +
                                   checkBattletags[item].loss + "," +
                                   checkBattletags[item].games_played + "," +
                                   checkBattletags[item].conservative_rating + "," +
                                   checkBattletags[item].rating + "," +
                                   checkBattletags[item].cache_number +
                                   ")";
                }
                else
                {
                    insertString += ",(" +
                                    checkBattletags[item].game_type + "," +
                                    checkBattletags[item].season + "," +
                                    checkBattletags[item].player_hero_role + "," +
                                    checkBattletags[item].rank_counter + "," +
                                    "\"" + checkBattletags[item].split_battletag + "\"" + "," +
                                    "\"" + checkBattletags[item].battletag + "\"" + "," +
                                    checkBattletags[item].blizz_id + "," +
                                    checkBattletags[item].region + "," +
                                    checkBattletags[item].win_rate + "," +
                                    checkBattletags[item].win + "," +
                                    checkBattletags[item].loss + "," +
                                    checkBattletags[item].games_played + "," +
                                    checkBattletags[item].conservative_rating + "," +
                                    checkBattletags[item].rating + "," +
                                    checkBattletags[item].cache_number +
                                    ")";
                }
            }

            if (insertString != "")
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT into heroesprofile_cache.leaderboard (game_type, season, type, rank, split_battletag, battletag, blizz_id, region, win_rate, win, loss, games_played, conservative_rating, rating, cache_number) VALUES " + insertString;
                var reader = cmd.ExecuteReader();
            }
        }
    }

}
