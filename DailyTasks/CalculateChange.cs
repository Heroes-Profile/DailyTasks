using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace DailyTasks
{
    class CalculateChange
    {
        private string db_connect_string = new DB_Connect().heroesprofile_config;

        public CalculateChange()
        {

            //Likely need to change this to a dynamic list, and then pull major patches from 2.43 up to the latest from season_game_versions table
            string[] major_patches = new string[8];

            major_patches[0] = "2.43";
            major_patches[1] = "2.44";
            major_patches[2] = "2.45";
            major_patches[3] = "2.46";
            major_patches[4] = "2.47";
            major_patches[5] = "2.48";
            major_patches[6] = "2.49";
            major_patches[7] = "2.50";


            List<string> minor_patches = new List<string>();
            using (MySqlConnection conn = new MySqlConnection(db_connect_string))
            {
                conn.Open();

                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT game_version FROM heroesprofile.season_game_versions where game_version >= 2.43";

                    MySqlDataReader Reader = cmd.ExecuteReader();

                    while (Reader.Read())
                    {
                        minor_patches.Add(Reader.GetString("game_version"));
                    }
                }
            }

            string[] game_types = new string[3];

            game_types[0] = "5";
            game_types[1] = "1";
            game_types[2] = "2";

            for (int i = 0; i < major_patches.Length; i++)
            {
                for (int j = 0; j < game_types.Length; j++)
                {
                    Dictionary<string, ChangeData> global_results = new Dictionary<string, ChangeData>();
                    int total_games = 0;
                    using (MySqlConnection conn = new MySqlConnection(db_connect_string))
                    {
                        conn.Open();

                        using (MySqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT hero, win_loss, SUM(games_played) as games_played FROM heroesprofile.global_hero_stats" +
                                " WHERE game_version like " + "\"" + major_patches[i] + "%" + "\"" +
                                " AND game_type in (" + game_types[j] + ")";
                            cmd.CommandText += " GROUP by hero, win_loss";
                            cmd.CommandText += " ORDER BY hero ASC, win_loss ASC";



                            Console.WriteLine(cmd.CommandText);
                            cmd.CommandTimeout = 0;
                            MySqlDataReader Reader = cmd.ExecuteReader();

                            while (Reader.Read())
                            {
                                if (global_results.ContainsKey(Reader.GetString("hero")))
                                {
                                    ChangeData d = global_results[Reader.GetString("hero")];

                                    if (Reader.GetInt32("win_loss") == 1)
                                    {
                                        d.wins = Reader.GetInt32("games_played");
                                    }
                                    else
                                    {
                                        d.losses = Reader.GetInt32("games_played");
                                    }
                                    total_games += Reader.GetInt32("games_played");
                                    global_results[Reader.GetString("hero")] = d;
                                }
                                else
                                {
                                    ChangeData d = new ChangeData();
                                    d.wins = 0;
                                    d.losses = 0;
                                    d.win_rate = 0;
                                    d.games_played = 0;
                                    d.bans = 0;
                                    d.ban_rate = 0;
                                    d.popularity = 0;

                                    if (Reader.GetInt32("win_loss") == 1)
                                    {
                                        d.wins = Reader.GetInt32("games_played");
                                    }
                                    else
                                    {
                                        d.losses = Reader.GetInt32("games_played");
                                    }
                                    total_games += Reader.GetInt32("games_played");
                                    global_results[Reader.GetString("hero")] = d;
                                }
                            }
                        }


                        using (MySqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT hero, SUM(bans) as bans FROM heroesprofile.global_hero_stats_bans" +
                                " WHERE game_version like " + "\"" + major_patches[i] + "%" + "\"" +
                                " AND game_type in (" + game_types[j] + ")";
                            cmd.CommandText += " GROUP by hero";
                            cmd.CommandText += " ORDER BY hero ASC";



                            Console.WriteLine(cmd.CommandText);
                            cmd.CommandTimeout = 0;
                            MySqlDataReader Reader = cmd.ExecuteReader();

                            while (Reader.Read())
                            {
                                if (global_results.ContainsKey(Reader.GetString("hero")))
                                {
                                    ChangeData d = global_results[Reader.GetString("hero")];
                                    d.bans = Reader.GetInt32("bans");
                                    global_results[Reader.GetString("hero")] = d;
                                }
                                else
                                {
                                    ChangeData d = new ChangeData();
                                    d.bans = Reader.GetInt32("bans");
                                    global_results[Reader.GetString("hero")] = d;
                                }
                            }
                        }

                        total_games /= 10;


                        foreach (var hero in global_results.Keys)
                        {
                            ChangeData d = global_results[hero];
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
                                d.popularity = (d.games_played / total_games) * 100;
                                d.ban_rate = 0;
                            }
                            else
                            {
                                d.popularity = ((d.games_played + d.bans) / total_games) * 100;
                                d.ban_rate = (d.bans / total_games) * 100;
                            }


                            using (MySqlCommand cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "INSERT INTO heroesprofile_cache.global_hero_change (game_version, game_type, hero, win_rate, popularity, ban_rate, games_played, wins, losses, bans) VALUES (" +
                                    "\"" + major_patches[i] + "\"" + ", " +
                                    "\"" + game_types[j] + "\"" + ", " +
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
                                MySqlDataReader Reader = cmd.ExecuteReader();
                            }
                        }



                    }
                }

            }

            foreach (var patch in minor_patches)
            {
                {
                    for (int j = 0; j < game_types.Length; j++)
                    {
                        Dictionary<string, ChangeData> global_results = new Dictionary<string, ChangeData>();
                        int total_games = 0;
                        using (MySqlConnection conn = new MySqlConnection(db_connect_string))
                        {
                            conn.Open();

                            using (MySqlCommand cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT hero, win_loss, SUM(games_played) as games_played FROM heroesprofile.global_hero_stats" +
                                    " WHERE game_version = " + "\"" + patch + "\"" +
                                    " AND game_type in (" + game_types[j] + ")";
                                cmd.CommandText += " GROUP by hero, win_loss";
                                cmd.CommandText += " ORDER BY hero ASC, win_loss ASC";



                                Console.WriteLine(cmd.CommandText);
                                cmd.CommandTimeout = 0;
                                MySqlDataReader Reader = cmd.ExecuteReader();

                                while (Reader.Read())
                                {
                                    if (global_results.ContainsKey(Reader.GetString("hero")))
                                    {
                                        ChangeData d = global_results[Reader.GetString("hero")];

                                        if (Reader.GetInt32("win_loss") == 1)
                                        {
                                            d.wins = Reader.GetInt32("games_played");
                                        }
                                        else
                                        {
                                            d.losses = Reader.GetInt32("games_played");
                                        }
                                        total_games += Reader.GetInt32("games_played");
                                        global_results[Reader.GetString("hero")] = d;
                                    }
                                    else
                                    {
                                        ChangeData d = new ChangeData();
                                        d.wins = 0;
                                        d.losses = 0;
                                        d.win_rate = 0;
                                        d.games_played = 0;
                                        d.bans = 0;
                                        d.ban_rate = 0;
                                        d.popularity = 0;

                                        if (Reader.GetInt32("win_loss") == 1)
                                        {
                                            d.wins = Reader.GetInt32("games_played");
                                        }
                                        else
                                        {
                                            d.losses = Reader.GetInt32("games_played");
                                        }
                                        total_games += Reader.GetInt32("games_played");
                                        global_results[Reader.GetString("hero")] = d;
                                    }
                                }
                            }


                            using (MySqlCommand cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT hero, SUM(bans) as bans FROM heroesprofile.global_hero_stats_bans" +
                                    " WHERE game_version like " + "\"" + patch + "%" + "\"" +
                                    " AND game_type in (" + game_types[j] + ")";
                                cmd.CommandText += " GROUP by hero";
                                cmd.CommandText += " ORDER BY hero ASC";



                                Console.WriteLine(cmd.CommandText);
                                cmd.CommandTimeout = 0;
                                MySqlDataReader Reader = cmd.ExecuteReader();

                                while (Reader.Read())
                                {
                                    if (global_results.ContainsKey(Reader.GetString("hero")))
                                    {
                                        ChangeData d = global_results[Reader.GetString("hero")];
                                        d.bans = Reader.GetInt32("bans");
                                        global_results[Reader.GetString("hero")] = d;
                                    }
                                    else
                                    {
                                        ChangeData d = new ChangeData();
                                        d.bans = Reader.GetInt32("bans");
                                        global_results[Reader.GetString("hero")] = d;
                                    }
                                }
                            }

                            total_games /= 10;


                            foreach (var hero in global_results.Keys)
                            {
                                ChangeData d = global_results[hero];
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
                                    d.popularity = (d.games_played / total_games) * 100;
                                    d.ban_rate = 0;
                                }
                                else
                                {
                                    d.popularity = ((d.games_played + d.bans) / total_games) * 100;
                                    d.ban_rate = (d.bans / total_games) * 100;
                                }


                                using (MySqlCommand cmd = conn.CreateCommand())
                                {
                                    cmd.CommandText = "INSERT INTO heroesprofile_cache.global_hero_change (game_version, game_type, hero, win_rate, popularity, ban_rate, games_played, wins, losses, bans) VALUES (" +
                                        "\"" + patch + "\"" + ", " +
                                        "\"" + game_types[j] + "\"" + ", " +
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
                                    MySqlDataReader Reader = cmd.ExecuteReader();
                                }
                            }
                        }
                    }

                }
            }
        }
    }

}
