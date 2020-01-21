using System;
using System.Collections.Generic;
using DailyTasks.Models;
using MySql.Data.MySqlClient;

namespace DailyTasks
{
    internal class CalculateBreakdowns
    {
        private readonly DbSettings _dbSettings;
        private readonly string _connectionString;
        private Dictionary<string, string> heroesList = new Dictionary<string, string>();
        private Dictionary<string, string> heroesList_short = new Dictionary<string, string>();
        private Dictionary<string, string> mmr_type_ids = new Dictionary<string, string>();
        private Dictionary<string, string> mmr_type_names = new Dictionary<string, string>();
        private Dictionary<string, string> league_tiers = new Dictionary<string, string>();

        private const double bronze = .12;
        private const double silver = .23;
        private const double gold = .26;
        private const double platinum = .22;
        private const double diamond = .12;
        private const double master = .05;
        private const double grandmaster = 200;

        private const double MIN_GAMES_PLAYED = 25;
        private const double MIN_GAMES_PLAYED_HERO = 5;
        Dictionary<string, string> leagueList = new Dictionary<string, string>();

        public CalculateBreakdowns(DbSettings dbSettings)
        {
            _dbSettings = dbSettings;
            _connectionString = ConnectionStringBuilder.BuildConnectionString(_dbSettings);
            
            getHeroesList();

            var roleList = new Dictionary<string, string>
            {
                    {"Support", "Support"},
                    {"Melee Assassin", "Melee Assassin"},
                    {"Tank", "Tank"},
                    {"Bruiser", "Bruiser"},
                    {"Healer", "Healer"},
                    {"Ranged Assassin", "Ranged Assassin"}
            };


            leagueList.Add("5", "sl");

            leagueList.Add("3", "hl");
            leagueList.Add("1", "qm");
            leagueList.Add("4", "tl");
            leagueList.Add("2", "ud");


            foreach (var leagueItem in leagueList.Keys)
            {
                foreach (var item in heroesList.Keys)
                {
                    CalculateLeagues(mmr_type_names[heroesList_short[item]], leagueItem, "all");
                }
                CalculateLeagues("10000", leagueItem, "all");

            }

            foreach (var leagueItem in leagueList.Keys)
            {
                foreach (var role in roleList.Keys)
                {
                    CalculateLeagues(mmr_type_names[role], leagueItem, "all");
                }
            }

        }
        private void getHeroesList()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {

                cmd.CommandText = "SELECT name,short_name FROM heroes";
                var Reader = cmd.ExecuteReader();
                while (Reader.Read())
                {

                    var heroName = Reader["short_name"].Equals(DBNull.Value) ? string.Empty : Reader.GetString("short_name");
                    var name = Reader["name"].Equals(DBNull.Value) ? string.Empty : Reader.GetString("name");


                    heroesList_short.Add(heroName, name);

                    heroesList.Add(heroName, name);
                    Console.WriteLine(heroName);
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM heroesprofile.mmr_type_ids;";
                var Reader = cmd.ExecuteReader();
                while (Reader.Read())
                {
                    mmr_type_ids.Add(Reader.GetString("mmr_type_id"), Reader.GetString("name"));
                    mmr_type_names.Add(Reader.GetString("name"), Reader.GetString("mmr_type_id"));
                }
            }
        }

        private void CalculateLeagues(string type, string game_type, string season)
        {
            var totalPlayers = 0;
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) as total_players FROM (SELECT " +
                                  " type_value," +
                                  " game_type," +
                                  " blizz_id," +
                                  " region," +
                                  " SUM(win) AS win," +
                                  " SUM(loss) AS loss" +
                                  " FROM" +
                                  " heroesprofile.master_mmr_data" +
                                  " WHERE" +
                                  " type_value = " + type +
                                  " AND game_type = " + game_type +
                                  " GROUP BY type_value, game_type, blizz_id, region";

                if (type == "10000")
                {
                    cmd.CommandText += " HAVING(win + loss) > " + MIN_GAMES_PLAYED + ") as data";

                }
                else
                {
                    cmd.CommandText += " HAVING(win + loss) > " + MIN_GAMES_PLAYED_HERO + ") as data";

                }

                cmd.CommandTimeout = 0;
                //Console.WriteLine(cmd.CommandText);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    totalPlayers = Convert.ToInt32(reader["total_players"].Equals(DBNull.Value) ? string.Empty : reader.GetString("total_players"));
                }
            }

            if (totalPlayers <= 15) return;
            {
                var bronzeTotal = Math.Floor(totalPlayers * bronze);
                if (bronzeTotal == 0)
                {
                    bronzeTotal = 1;
                }

                var silverTotal = Math.Floor(totalPlayers * silver);
                if (bronzeTotal == 0)
                {
                    silverTotal = 1;
                }

                var goldTotal = Math.Floor(totalPlayers * gold);
                if (goldTotal == 0)
                {
                    goldTotal = 1;
                }

                var platinumTotal = Math.Floor(totalPlayers * platinum);
                if (platinumTotal == 0)
                {
                    platinumTotal = 1;
                }

                var diamondTotal = Math.Floor(totalPlayers * diamond);
                if (diamondTotal == 0)
                {
                    diamondTotal = 1;
                }

                var masterTotal = Math.Floor(totalPlayers * master);
                if (masterTotal == 0)
                {
                    masterTotal = 1;
                }

                double[] bronzePlayers = { 0, bronzeTotal };
                double[] silverPlayers = { bronzePlayers[1], silverTotal };
                double[] goldPlayers = { silverPlayers[1] + bronzePlayers[1], goldTotal };
                double[] platinumPlayers = { goldPlayers[1] + silverPlayers[1] + bronzePlayers[1], platinumTotal };
                double[] diamondPlayers = { platinumPlayers[1] + goldPlayers[1] + silverPlayers[1] + bronzePlayers[1], diamondTotal };
                double[] masterPlayers = { diamondPlayers[1] + platinumPlayers[1] + goldPlayers[1] + silverPlayers[1] + bronzePlayers[1], masterTotal };

                var mmr_list = new Dictionary<string, double[]>
                {
                        {"2", silverPlayers},
                        {"3", goldPlayers},
                        {"4", platinumPlayers},
                        {"5", diamondPlayers},
                        {"6", masterPlayers}
                };

                // mmr_list.Add("1", bronzePlayers);


                var league_tier_names = new Dictionary<string, string>
                {
                        {"1", "bronze"},
                        {"2", "silver"},
                        {"3", "gold"},
                        {"4", "platinum"},
                        {"5", "diamond"},
                        {"6", "master"}
                };
                foreach (var item in mmr_list.Keys)
                {
                    using var conn = new MySqlConnection(_connectionString);
                    conn.Open();

                    //Min to get into silver
                    double minMmr = 0;
                    using (var cmd = conn.CreateCommand())
                    {

                        cmd.CommandText = "SELECT MIN(conservative_rating) as min_mmr FROM (SELECT * FROM heroesprofile.master_mmr_data" +
                                          " WHERE" +
                                          " type_value = " + type +
                                          " AND game_type = " + game_type;
                        if (type == "10000")
                        {
                            cmd.CommandText += " HAVING(win + loss) > " + MIN_GAMES_PLAYED;

                        }
                        else
                        {
                            cmd.CommandText += " HAVING(win + loss) > " + MIN_GAMES_PLAYED_HERO;

                        }

                        cmd.CommandText += " ORDER BY conservative_rating ASC, (win + loss) ASC" +
                                           " LIMIT " + mmr_list[item][1] + " OFFSET " + mmr_list[item][0] + ") as mmr_data";

                        cmd.CommandTimeout = 0;
                        //Console.WriteLine(cmd.CommandText);
                        var Reader = cmd.ExecuteReader();

                        while (Reader.Read())
                        {
                            var value = Reader["min_mmr"].Equals(DBNull.Value) ? string.Empty : Reader.GetString("min_mmr");
                            if (value == "")
                            {
                                minMmr = 0;
                            }
                            else
                            {
                                minMmr = 1800 + 40 * Convert.ToDouble(Reader["min_mmr"].Equals(DBNull.Value) ? string.Empty : Reader.GetString("min_mmr"));
                            }
                        }
                    }
                    Console.WriteLine("For type " + mmr_type_ids[type] + " in league " + leagueList[game_type] + " |" + league_tier_names[item] + " < " + minMmr);
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "INSERT into heroesprofile.league_breakdowns (type_role_hero, game_type, league_tier, min_mmr) VALUES (" +
                                          type + "," +
                                          game_type + "," +
                                          item + "," +
                                          minMmr + ")";
                        cmd.CommandText += " ON DUPLICATE KEY UPDATE " +
                                           "type_role_hero = VALUES(type_role_hero)," +
                                           "game_type = VALUES(game_type)," +
                                           "league_tier = VALUES(league_tier)," +
                                           "min_mmr = VALUES(min_mmr)";

                        //Console.WriteLine(cmd.CommandText);
                        var Reader = cmd.ExecuteReader();
                    }
                }
            }
        }
    }
}
