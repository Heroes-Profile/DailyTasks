using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace DailyTasks
{
    class CalculateLeaderBoards
    {
        private string strConnect = new DB_Connect().heroesprofile_config;
        private Dictionary<string, int> heroes = new Dictionary<string, int>();
        private Dictionary<string, int> roles = new Dictionary<string, int>();
        private Dictionary<string, int> game_types = new Dictionary<string, int>();
        private int season;
        public CalculateLeaderBoards()
        {
            int cache_number = 0;
            using (MySqlConnection conn = new MySqlConnection(strConnect))
            {
                conn.Open();

                using (MySqlCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT name,id, new_role FROM heroes";
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {
                        heroes.Add(Reader.GetString("name"), Reader.GetInt32("id"));
                    }
                }


                using (MySqlCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT mmr_type_id, mmr_type_ids.name FROM heroesprofile.mmr_type_ids inner join heroes on heroes.new_role = mmr_type_ids.name group by mmr_type_id, mmr_type_ids.name";
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {
                        roles.Add(Reader.GetString("name"), Reader.GetInt32("mmr_type_id"));
                    }
                }
                using (MySqlCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT MAX(cache_number) as max_cache_number FROM heroesprofile_cache.table_cache_value where table_to_cache = 'leaderboard'";
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {
                        cache_number = (Reader.GetInt32("max_cache_number") + 1);
                    }
                }

            }
            game_types.Add("qm", 1);
            game_types.Add("ud", 2);
            game_types.Add("sl", 5);


            DateTime start_date = DateTime.Now;
            int weeks = 1;

            using (MySqlConnection conn = new MySqlConnection(strConnect))
            {
                conn.Open();

                using (MySqlCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT MAX(id) as max_id FROM heroesprofile.season_dates";
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {
                        season = Reader.GetInt32("max_id");
                    }
                }


                using (MySqlCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT start_date FROM heroesprofile.season_dates where id = " + season;
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {
                        start_date = Reader.GetDateTime("start_date");
                    }
                }


                using (MySqlCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT ROUND(DATEDIFF(NOW(), '" + start_date.ToString("yyyy-MM-dd HH:mm:ss") + "')/7, 0) AS weeksout";
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {
                        weeks = Reader.GetInt32("weeksout");
                    }
                }
            }
            if (weeks == 0)
            {
                weeks = 1;
            }

            foreach (var short_game_mode in game_types.Keys)
            {
                Console.WriteLine("Running Player data for " + short_game_mode); ;

                getPlayers("player", season, start_date, weeks, game_types[short_game_mode], 10000, cache_number);


                foreach (var hero in heroes.Keys)
                {
                    Console.WriteLine("Running " + hero + " data for " + short_game_mode); ;

                    getPlayers("hero", season, start_date, weeks, game_types[short_game_mode], heroes[hero], cache_number);
                }

                foreach (var role in roles.Keys)
                {
                    Console.WriteLine("Running " + role + " data for " + short_game_mode); ;

                    getPlayers("role", season, start_date, weeks, game_types[short_game_mode], roles[role], cache_number);
                }
            }


            using (MySqlConnection conn = new MySqlConnection(strConnect))
            {
                conn.Open();
                using (MySqlCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "INSERT into heroesprofile_cache.table_cache_value (table_to_cache, date_cached) VALUES ('leaderboard', " + "\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"" + ")";
                    MySqlDataReader Reader = cmd.ExecuteReader();
                }
            }



        }

        private void getPlayers(string type, int season, DateTime start_date, int weeks, int game_type, int player_hero_role, int cache_number)
        {
            Dictionary<string, LeaderboardPlayerData> check_battletags = new Dictionary<string, LeaderboardPlayerData>();

            using (MySqlConnection conn = new MySqlConnection(strConnect))
            {
                conn.Open();
                int max_games_played = 0;
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT if(AVG(games_played) is null, 1,AVG(games_played))  as max_games_played FROM (SELECT games_played FROM heroesprofile.master_games_played_data where type_value = " + player_hero_role + " and season = " + season + " and game_type = " + game_type + " ORDER BY games_played DESC LIMIT " + weeks + ") as data";

                    //Console.WriteLine(cmd.CommandText);
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {
                        max_games_played = Convert.ToInt32(Math.Floor(Reader.GetDouble("max_games_played")));
                    }
                }

                string insertString = "";
                using (MySqlCommand cmd = conn.CreateCommand())
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
                    "50 + (((master_games_played_data.win / (master_games_played_data.win + master_games_played_data.loss)) * 100) - 50) * (master_games_played_data.games_played / " + max_games_played + ") + (master_mmr_data.conservative_rating / 10) as rating " +
                    "FROM " +
                    "master_games_played_data " +
                    "INNER JOIN " +
                    "master_mmr_data ON (master_mmr_data.type_value = " + player_hero_role +
                    " AND master_mmr_data.game_type = " + game_type +
                    " AND master_mmr_data.blizz_id = master_games_played_data.blizz_id " +
                    "AND master_mmr_data.region = master_games_played_data.region) " +
                    "INNER JOIN " +
                    "battletags ON (battletags.blizz_id = master_games_played_data.blizz_id " +
                    "AND battletags.region = master_games_played_data.region) " +
                    "WHERE " +
                    "master_games_played_data.type_value = " + player_hero_role +
                    " AND master_games_played_data.season = " + season +
                    " AND master_games_played_data.game_type = " + game_type;

                    cmd.CommandText += " AND master_games_played_data.games_played >=  ";

                    if (type == "player")
                    {
                        cmd.CommandText += (5 * weeks);
                    }
                    else if (type == "hero")
                    {
                        cmd.CommandText += (2 * weeks);
                    }
                    else if (type == "role")
                    {
                        cmd.CommandText += (2 * weeks);
                    }
                    cmd.CommandText += " HAVING win_rate >= 50 " +
                    "ORDER BY rating DESC ";//LIMIT 500";

                    int rank_counter = 1;
                    //Console.WriteLine(cmd.CommandText);
                    cmd.CommandTimeout = 0;
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {
                        if (check_battletags.ContainsKey(Reader.GetInt32("blizz_id") + "|" + Reader.GetInt32("region")))
                        {
                            DateTime inDict = check_battletags[Reader.GetInt32("blizz_id") + "|" + Reader.GetInt32("region")].latest_game_played;
                            DateTime compare = Reader.GetDateTime("latest_game");
                            if (check_battletags[Reader.GetInt32("blizz_id") + "|" + Reader.GetInt32("region")].latest_game_played < Reader.GetDateTime("latest_game"))
                            {
                                LeaderboardPlayerData data = new LeaderboardPlayerData();
                                data.game_type = game_type;
                                data.season = season;
                                data.player_hero_role = player_hero_role;
                                data.rank_counter = check_battletags[Reader.GetInt32("blizz_id") + "|" + Reader.GetInt32("region")].rank_counter;
                                data.split_battletag = Reader.GetString("split_battletag");
                                data.battletag = Reader.GetString("battletag");
                                data.blizz_id = Reader.GetInt32("blizz_id");
                                data.region = Reader.GetInt32("region");
                                data.win_rate = Reader.GetDouble("win_rate");
                                data.win = Reader.GetInt32("win");
                                data.loss = Reader.GetInt32("loss");
                                data.latest_game_played = Reader.GetDateTime("latest_game");
                                data.games_played = Reader.GetInt32("games_played");
                                data.conservative_rating = Reader.GetDouble("conservative_rating");
                                data.rating = Reader.GetDouble("rating");
                                data.cache_number = cache_number;

                                check_battletags[Reader.GetInt32("blizz_id") + "|" + Reader.GetInt32("region")] = data;
                            }
                        }
                        else
                        {
                            LeaderboardPlayerData data = new LeaderboardPlayerData();
                            data.game_type = game_type;
                            data.season = season;
                            data.player_hero_role = player_hero_role;
                            data.rank_counter = rank_counter;
                            data.split_battletag = Reader.GetString("split_battletag");
                            data.battletag = Reader.GetString("battletag");
                            data.blizz_id = Reader.GetInt32("blizz_id");
                            data.region = Reader.GetInt32("region");
                            data.win_rate = Reader.GetDouble("win_rate");
                            data.win = Reader.GetInt32("win");
                            data.loss = Reader.GetInt32("loss");
                            data.games_played = Reader.GetInt32("games_played");
                            data.latest_game_played = Reader.GetDateTime("latest_game");
                            data.conservative_rating = Reader.GetDouble("conservative_rating");
                            data.rating = Reader.GetDouble("rating");
                            data.cache_number = cache_number;

                            check_battletags[Reader.GetInt32("blizz_id") + "|" + Reader.GetInt32("region")] = data;
                            rank_counter++;

                        }


                    }
                }

                foreach (var item in check_battletags.Keys)
                {

                    if (insertString == "")
                    {
                        insertString = "(" +
                            check_battletags[item].game_type + "," +
                            check_battletags[item].season + "," +
                            check_battletags[item].player_hero_role + "," +
                            check_battletags[item].rank_counter + "," +
                            "\"" + check_battletags[item].split_battletag + "\"" + "," +
                            "\"" + check_battletags[item].battletag + "\"" + "," +
                            check_battletags[item].blizz_id + "," +
                            check_battletags[item].region + "," +
                            check_battletags[item].win_rate + "," +
                            check_battletags[item].win + "," +
                            check_battletags[item].loss + "," +
                            check_battletags[item].games_played + "," +
                            check_battletags[item].conservative_rating + "," +
                            check_battletags[item].rating + "," +
                            check_battletags[item].cache_number +
                            ")";
                    }
                    else
                    {
                        insertString += ",(" +
                            check_battletags[item].game_type + "," +
                            check_battletags[item].season + "," +
                            check_battletags[item].player_hero_role + "," +
                            check_battletags[item].rank_counter + "," +
                            "\"" + check_battletags[item].split_battletag + "\"" + "," +
                            "\"" + check_battletags[item].battletag + "\"" + "," +
                            check_battletags[item].blizz_id + "," +
                            check_battletags[item].region + "," +
                            check_battletags[item].win_rate + "," +
                            check_battletags[item].win + "," +
                            check_battletags[item].loss + "," +
                            check_battletags[item].games_played + "," +
                            check_battletags[item].conservative_rating + "," +
                            check_battletags[item].rating + "," +
                            check_battletags[item].cache_number +
                            ")";
                    }
                }

                if (insertString != "")
                {
                    using (MySqlCommand cmd = conn.CreateCommand())
                    {

                        cmd.CommandText = "INSERT into heroesprofile_cache.leaderboard (game_type, season, type, rank, split_battletag, battletag, blizz_id, region, win_rate, win, loss, games_played, conservative_rating, rating, cache_number) VALUES " + insertString;
                        MySqlDataReader Reader = cmd.ExecuteReader();
                    }
                }



            }

        }
    }

}
