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
        private Dictionary<string, string> _heroesList = new Dictionary<string, string>();
        private Dictionary<string, string> _heroesListShort = new Dictionary<string, string>();
        private Dictionary<string, string> _mmrTypeIds = new Dictionary<string, string>();
        private Dictionary<string, string> _mmrTypeNames = new Dictionary<string, string>();
        private Dictionary<string, string> _leagueTiers = new Dictionary<string, string>();

        private const double Bronze = .12;
        private const double Silver = .23;
        private const double Gold = .26;
        private const double Platinum = .22;
        private const double Diamond = .12;
        private const double Master = .05;
        private const double Grandmaster = 200;

        private const double MinGamesPlayed = 25;
        private const double MinGamesPlayedHero = 5;
        Dictionary<string, string> _leagueList = new Dictionary<string, string>();

        public CalculateBreakdowns(DbSettings dbSettings)
        {
            _dbSettings = dbSettings;
            _connectionString = ConnectionStringBuilder.BuildConnectionString(_dbSettings);
            
            GetHeroesList();

            var roleList = new Dictionary<string, string>
            {
                    {"Support", "Support"},
                    {"Melee Assassin", "Melee Assassin"},
                    {"Tank", "Tank"},
                    {"Bruiser", "Bruiser"},
                    {"Healer", "Healer"},
                    {"Ranged Assassin", "Ranged Assassin"}
            };


            _leagueList.Add("5", "sl");
            _leagueList.Add("3", "hl");
            _leagueList.Add("1", "qm");
            _leagueList.Add("4", "tl");
            _leagueList.Add("2", "ud");


            foreach (var leagueItem in _leagueList.Keys)
            {
                foreach (var item in _heroesList.Keys)
                {
                    CalculateLeagues(_mmrTypeNames[_heroesListShort[item]], leagueItem, "all");
                }
                CalculateLeagues("10000", leagueItem, "all");

            }

            foreach (var leagueItem in _leagueList.Keys)
            {
                foreach (var role in roleList.Keys)
                {
                    CalculateLeagues(_mmrTypeNames[role], leagueItem, "all");
                }
            }

        }
        private void GetHeroesList()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {

                cmd.CommandText = "SELECT name,short_name FROM heroes";
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var heroName = reader["short_name"].Equals(DBNull.Value) ? string.Empty : reader.GetString("short_name");
                    var name = reader["name"].Equals(DBNull.Value) ? string.Empty : reader.GetString("name");

                    _heroesListShort.Add(heroName, name);

                    _heroesList.Add(heroName, name);
                    Console.WriteLine(heroName);
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM heroesprofile.mmr_type_ids;";
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _mmrTypeIds.Add(reader.GetString("mmr_type_id"), reader.GetString("name"));
                    _mmrTypeNames.Add(reader.GetString("name"), reader.GetString("mmr_type_id"));
                }
            }
        }

        private void CalculateLeagues(string type, string gameType, string season)
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
                                  " AND game_type = " + gameType +
                                  " GROUP BY type_value, game_type, blizz_id, region";

                if (type == "10000")
                {
                    cmd.CommandText += " HAVING(win + loss) > " + MinGamesPlayed + ") as data";

                }
                else
                {
                    cmd.CommandText += " HAVING(win + loss) > " + MinGamesPlayedHero + ") as data";

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

                double[] bronzePlayers = { 0, bronzeTotal };
                double[] silverPlayers = { bronzePlayers[1], silverTotal };
                double[] goldPlayers = { silverPlayers[1] + bronzePlayers[1], goldTotal };
                double[] platinumPlayers = { goldPlayers[1] + silverPlayers[1] + bronzePlayers[1], platinumTotal };
                double[] diamondPlayers = { platinumPlayers[1] + goldPlayers[1] + silverPlayers[1] + bronzePlayers[1], diamondTotal };
                double[] masterPlayers = { diamondPlayers[1] + platinumPlayers[1] + goldPlayers[1] + silverPlayers[1] + bronzePlayers[1], masterTotal };

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
                    using var conn = new MySqlConnection(_connectionString);
                    conn.Open();

                    //Min to get into silver
                    double minMmr = 0;
                    using (var cmd = conn.CreateCommand())
                    {

                        cmd.CommandText = "SELECT MIN(conservative_rating) as min_mmr FROM (SELECT * FROM heroesprofile.master_mmr_data" +
                                          " WHERE" +
                                          " type_value = " + type +
                                          " AND game_type = " + gameType;
                        if (type == "10000")
                        {
                            cmd.CommandText += " HAVING(win + loss) > " + MinGamesPlayed;

                        }
                        else
                        {
                            cmd.CommandText += " HAVING(win + loss) > " + MinGamesPlayedHero;

                        }

                        cmd.CommandText += " ORDER BY conservative_rating ASC, (win + loss) ASC" +
                                           " LIMIT " + mmrList[item][1] + " OFFSET " + mmrList[item][0] + ") as mmr_data";

                        cmd.CommandTimeout = 0;
                        //Console.WriteLine(cmd.CommandText);
                        var reader = cmd.ExecuteReader();

                        while (reader.Read())
                        {
                            var value = reader["min_mmr"].Equals(DBNull.Value) ? string.Empty : reader.GetString("min_mmr");
                            if (value == "")
                            {
                                minMmr = 0;
                            }
                            else
                            {
                                minMmr = 1800 + 40 * Convert.ToDouble(reader["min_mmr"].Equals(DBNull.Value) ? string.Empty : reader.GetString("min_mmr"));
                            }
                        }
                    }
                    Console.WriteLine("For type " + _mmrTypeIds[type] + " in league " + _leagueList[gameType] + " |" + leagueTierNames[item] + " < " + minMmr);
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "INSERT into heroesprofile.league_breakdowns (type_role_hero, game_type, league_tier, min_mmr) VALUES (" +
                                          type + "," +
                                          gameType + "," +
                                          item + "," +
                                          minMmr + ")";
                        cmd.CommandText += " ON DUPLICATE KEY UPDATE " +
                                           "type_role_hero = VALUES(type_role_hero)," +
                                           "game_type = VALUES(game_type)," +
                                           "league_tier = VALUES(league_tier)," +
                                           "min_mmr = VALUES(min_mmr)";

                        //Console.WriteLine(cmd.CommandText);
                        var reader = cmd.ExecuteReader();
                    }
                }
            }
        }
    }
}
