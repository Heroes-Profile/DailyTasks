using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace DailyTasks
{
    class CalculateChange
    {
        private string _dbConnectString = new DB_Connect().heroesprofile_config;

        public CalculateChange()
        {

            //Likely need to change this to a dynamic list, and then pull major patches from 2.43 up to the latest from season_game_versions table
            var majorPatches = new string[8];

            majorPatches[0] = "2.43";
            majorPatches[1] = "2.44";
            majorPatches[2] = "2.45";
            majorPatches[3] = "2.46";
            majorPatches[4] = "2.47";
            majorPatches[5] = "2.48";
            majorPatches[6] = "2.49";
            majorPatches[7] = "2.50";


            var minorPatches = new List<string>();
            using (var conn = new MySqlConnection(_dbConnectString))
            {
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT game_version FROM heroesprofile.season_game_versions where game_version >= 2.43";

                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    minorPatches.Add(reader.GetString("game_version"));
                }
            }

            var gameTypes = new string[3];

            gameTypes[0] = "5";
            gameTypes[1] = "1";
            gameTypes[2] = "2";

            foreach (var majorPatch in majorPatches)
            {
                foreach (var gameType in gameTypes)
                {
                    var globalResults = new Dictionary<string, ChangeData>();
                    var totalGames = 0;
                    using var conn = new MySqlConnection(_dbConnectString);
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT hero, win_loss, SUM(games_played) as games_played FROM heroesprofile.global_hero_stats" +
                                          " WHERE game_version like " + "\"" + majorPatch + "%" + "\"" +
                                          " AND game_type in (" + gameType + ")";
                        cmd.CommandText += " GROUP by hero, win_loss";
                        cmd.CommandText += " ORDER BY hero ASC, win_loss ASC";



                        Console.WriteLine(cmd.CommandText);
                        cmd.CommandTimeout = 0;
                        var reader = cmd.ExecuteReader();

                        while (reader.Read())
                        {
                            if (globalResults.ContainsKey(reader.GetString("hero")))
                            {
                                var d = globalResults[reader.GetString("hero")];

                                if (reader.GetInt32("win_loss") == 1)
                                {
                                    d.wins = reader.GetInt32("games_played");
                                }
                                else
                                {
                                    d.losses = reader.GetInt32("games_played");
                                }
                                totalGames += reader.GetInt32("games_played");
                                globalResults[reader.GetString("hero")] = d;
                            }
                            else
                            {
                                var d = new ChangeData
                                {
                                        wins = 0,
                                        losses = 0,
                                        win_rate = 0,
                                        games_played = 0,
                                        bans = 0,
                                        ban_rate = 0,
                                        popularity = 0
                                };

                                if (reader.GetInt32("win_loss") == 1)
                                {
                                    d.wins = reader.GetInt32("games_played");
                                }
                                else
                                {
                                    d.losses = reader.GetInt32("games_played");
                                }
                                totalGames += reader.GetInt32("games_played");
                                globalResults[reader.GetString("hero")] = d;
                            }
                        }
                    }


                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT hero, SUM(bans) as bans FROM heroesprofile.global_hero_stats_bans" +
                                          " WHERE game_version like " + "\"" + majorPatch + "%" + "\"" +
                                          " AND game_type in (" + gameType + ")";
                        cmd.CommandText += " GROUP by hero";
                        cmd.CommandText += " ORDER BY hero ASC";



                        Console.WriteLine(cmd.CommandText);
                        cmd.CommandTimeout = 0;
                        var reader = cmd.ExecuteReader();

                        while (reader.Read())
                        {
                            if (globalResults.ContainsKey(reader.GetString("hero")))
                            {
                                var d = globalResults[reader.GetString("hero")];
                                d.bans = reader.GetInt32("bans");
                                globalResults[reader.GetString("hero")] = d;
                            }
                            else
                            {
                                var d = new ChangeData {bans = reader.GetInt32("bans")};
                                globalResults[reader.GetString("hero")] = d;
                            }
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
                            d.win_rate = (d.wins / d.games_played) * 100;
                        }


                        if (d.bans == 0)
                        {
                            d.popularity = (d.games_played / totalGames) * 100;
                            d.ban_rate = 0;
                        }
                        else
                        {
                            d.popularity = ((d.games_played + d.bans) / totalGames) * 100;
                            d.ban_rate = (d.bans / totalGames) * 100;
                        }


                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "INSERT INTO heroesprofile_cache.global_hero_change (game_version, game_type, hero, win_rate, popularity, ban_rate, games_played, wins, losses, bans) VALUES (" +
                                          "\"" + majorPatch + "\"" + ", " +
                                          "\"" + gameType + "\"" + ", " +
                                          "\"" + hero + "\"" + ", " +
                                          "\"" + d.win_rate + "\"" + ", " +
                                          "\"" + d.popularity + "\"" + ", " +
                                          "\"" + d.ban_rate + "\"" + ", " +
                                          "\"" + d.games_played + "\"" + ", " +
                                          "\"" + d.wins + "\"" + ", " +
                                          "\"" + d.losses + "\"" + ", " +
                                          "\"" + d.bans + "\"" +
                                          ")";
                        cmd.CommandText += " ON DUPLICATE KEY UPDATE " +
                                           "win_rate = VALUES(win_rate), " +
                                           "popularity = VALUES(popularity), " +
                                           "ban_rate = VALUES(ban_rate), " +
                                           "games_played = VALUES(games_played), " +
                                           "wins = VALUES(wins), " +
                                           "losses = VALUES(losses), " +
                                           "bans = VALUES(bans)";

                        Console.WriteLine(cmd.CommandText);
                        cmd.CommandTimeout = 0;
                        var reader = cmd.ExecuteReader();
                    }
                }
            }

            foreach (var patch in minorPatches)
            {
                {
                    foreach (var gameType in gameTypes)
                    {
                        var globalResults = new Dictionary<string, ChangeData>();
                        var totalGames = 0;
                        using var conn = new MySqlConnection(_dbConnectString);
                        conn.Open();

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT hero, win_loss, SUM(games_played) as games_played FROM heroesprofile.global_hero_stats" +
                                              " WHERE game_version = " + "\"" + patch + "\"" +
                                              " AND game_type in (" + gameType + ")";
                            cmd.CommandText += " GROUP by hero, win_loss";
                            cmd.CommandText += " ORDER BY hero ASC, win_loss ASC";



                            Console.WriteLine(cmd.CommandText);
                            cmd.CommandTimeout = 0;
                            var reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                if (globalResults.ContainsKey(reader.GetString("hero")))
                                {
                                    var d = globalResults[reader.GetString("hero")];

                                    if (reader.GetInt32("win_loss") == 1)
                                    {
                                        d.wins = reader.GetInt32("games_played");
                                    }
                                    else
                                    {
                                        d.losses = reader.GetInt32("games_played");
                                    }
                                    totalGames += reader.GetInt32("games_played");
                                    globalResults[reader.GetString("hero")] = d;
                                }
                                else
                                {
                                    var d = new ChangeData
                                    {
                                            wins = 0,
                                            losses = 0,
                                            win_rate = 0,
                                            games_played = 0,
                                            bans = 0,
                                            ban_rate = 0,
                                            popularity = 0
                                    };

                                    if (reader.GetInt32("win_loss") == 1)
                                    {
                                        d.wins = reader.GetInt32("games_played");
                                    }
                                    else
                                    {
                                        d.losses = reader.GetInt32("games_played");
                                    }
                                    totalGames += reader.GetInt32("games_played");
                                    globalResults[reader.GetString("hero")] = d;
                                }
                            }
                        }


                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT hero, SUM(bans) as bans FROM heroesprofile.global_hero_stats_bans" +
                                              " WHERE game_version like " + "\"" + patch + "%" + "\"" +
                                              " AND game_type in (" + gameType + ")";
                            cmd.CommandText += " GROUP by hero";
                            cmd.CommandText += " ORDER BY hero ASC";



                            Console.WriteLine(cmd.CommandText);
                            cmd.CommandTimeout = 0;
                            var reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                if (globalResults.ContainsKey(reader.GetString("hero")))
                                {
                                    var d = globalResults[reader.GetString("hero")];
                                    d.bans = reader.GetInt32("bans");
                                    globalResults[reader.GetString("hero")] = d;
                                }
                                else
                                {
                                    var d = new ChangeData {bans = reader.GetInt32("bans")};
                                    globalResults[reader.GetString("hero")] = d;
                                }
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
                                d.win_rate = (d.wins / d.games_played) * 100;
                            }


                            if (d.bans == 0)
                            {
                                d.popularity = (d.games_played / totalGames) * 100;
                                d.ban_rate = 0;
                            }
                            else
                            {
                                d.popularity = ((d.games_played + d.bans) / totalGames) * 100;
                                d.ban_rate = (d.bans / totalGames) * 100;
                            }


                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = "INSERT INTO heroesprofile_cache.global_hero_change (game_version, game_type, hero, win_rate, popularity, ban_rate, games_played, wins, losses, bans) VALUES (" +
                                              "\"" + patch + "\"" + ", " +
                                              "\"" + gameType + "\"" + ", " +
                                              "\"" + hero + "\"" + ", " +
                                              "\"" + d.win_rate + "\"" + ", " +
                                              "\"" + d.popularity + "\"" + ", " +
                                              "\"" + d.ban_rate + "\"" + ", " +
                                              "\"" + d.games_played + "\"" + ", " +
                                              "\"" + d.wins + "\"" + ", " +
                                              "\"" + d.losses + "\"" + ", " +
                                              "\"" + d.bans + "\"" +
                                              ")";
                            cmd.CommandText += " ON DUPLICATE KEY UPDATE " +
                                               "win_rate = VALUES(win_rate), " +
                                               "popularity = VALUES(popularity), " +
                                               "ban_rate = VALUES(ban_rate), " +
                                               "games_played = VALUES(games_played), " +
                                               "wins = VALUES(wins), " +
                                               "losses = VALUES(losses), " +
                                               "bans = VALUES(bans)";

                            Console.WriteLine(cmd.CommandText);
                            cmd.CommandTimeout = 0;
                            var reader = cmd.ExecuteReader();
                        }
                    }
                }
            }
        }
    }

}
