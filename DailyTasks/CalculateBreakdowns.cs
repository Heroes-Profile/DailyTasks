using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace DailyTasks
{
    class CalculateBreakdowns
    {
        private string strConnect = new DB_Connect().heroesprofile_config;
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

        public CalculateBreakdowns()
        {
            getHeroesList();

            Dictionary<string, string> roleList = new Dictionary<string, string>();
            roleList.Add("Support", "Support");
            roleList.Add("Melee Assassin", "Melee Assassin");
            roleList.Add("Tank", "Tank");
            roleList.Add("Bruiser", "Bruiser");
            roleList.Add("Healer", "Healer");
            roleList.Add("Ranged Assassin", "Ranged Assassin");


            leagueList.Add("5", "sl");

            leagueList.Add("3", "hl");
            leagueList.Add("1", "qm");
            leagueList.Add("4", "tl");
            leagueList.Add("2", "ud");


            foreach (var leagueItem in leagueList.Keys)
            {
                foreach (var item in heroesList.Keys)
                {
                    calculateLeagues(mmr_type_names[heroesList_short[item]], leagueItem, "all");
                }
                calculateLeagues("10000", leagueItem, "all");

            }

            foreach (var leagueItem in leagueList.Keys)
            {
                foreach (var role in roleList.Keys)
                {
                    calculateLeagues(mmr_type_names[role], leagueItem, "all");
                }
            }

        }
        private void getHeroesList()
        {

            using (MySqlConnection conn = new MySqlConnection(strConnect))
            {
                conn.Open();

                using (MySqlCommand cmd = conn.CreateCommand())
                {

                    cmd.CommandText = "SELECT name,short_name FROM heroes";
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {

                        string heroName = Reader["short_name"].Equals(DBNull.Value) ? String.Empty : Reader.GetString("short_name");
                        string name = Reader["name"].Equals(DBNull.Value) ? String.Empty : Reader.GetString("name");


                        heroesList_short.Add(heroName, name);

                        heroesList.Add(heroName, name);
                        Console.WriteLine(heroName);


                    }
                }


                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM heroesprofile.mmr_type_ids;";
                    MySqlDataReader Reader = cmd.ExecuteReader();
                    while (Reader.Read())
                    {


                        mmr_type_ids.Add(Reader.GetString("mmr_type_id"), Reader.GetString("name"));
                        mmr_type_names.Add(Reader.GetString("name"), Reader.GetString("mmr_type_id"));


                    }
                }
            }

        }

        private void calculateLeagues(string type, string game_type, string season)
        {

            int totalPlayers = 0;
            using (MySqlConnection conn = new MySqlConnection(strConnect))
            {
                conn.Open();
                using (MySqlCommand cmd = conn.CreateCommand())
                {

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
                    MySqlDataReader Reader = cmd.ExecuteReader();

                    int counter = 0;
                    while (Reader.Read())
                    {
                        counter++;
                        totalPlayers = Convert.ToInt32(Reader["total_players"].Equals(DBNull.Value) ? String.Empty : Reader.GetString("total_players"));

                    }
                }
            }
            if (totalPlayers > 15)
            {
                double bronzeTotal = Math.Floor(totalPlayers * bronze);
                if (bronzeTotal == 0)
                {
                    bronzeTotal = 1;
                }

                double silverTotal = Math.Floor(totalPlayers * silver);
                if (bronzeTotal == 0)
                {
                    silverTotal = 1;
                }

                double goldTotal = Math.Floor(totalPlayers * gold);
                if (goldTotal == 0)
                {
                    goldTotal = 1;
                }

                double platinumTotal = Math.Floor(totalPlayers * platinum);
                if (platinumTotal == 0)
                {
                    platinumTotal = 1;
                }

                double diamondTotal = Math.Floor(totalPlayers * diamond);
                if (diamondTotal == 0)
                {
                    diamondTotal = 1;
                }

                double masterTotal = Math.Floor(totalPlayers * master);
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

                Dictionary<string, double[]> mmr_list = new Dictionary<string, double[]>();

                // mmr_list.Add("1", bronzePlayers);
                mmr_list.Add("2", silverPlayers);
                mmr_list.Add("3", goldPlayers);
                mmr_list.Add("4", platinumPlayers);
                mmr_list.Add("5", diamondPlayers);
                mmr_list.Add("6", masterPlayers);


                Dictionary<string, string> league_tier_names = new Dictionary<string, string>();
                league_tier_names.Add("1", "bronze");
                league_tier_names.Add("2", "silver");
                league_tier_names.Add("3", "gold");
                league_tier_names.Add("4", "platinum");
                league_tier_names.Add("5", "diamond");
                league_tier_names.Add("6", "master");
                foreach (var item in mmr_list.Keys)
                {
                    using (MySqlConnection conn = new MySqlConnection(strConnect))
                    {
                        conn.Open();

                        //Min to get into silver
                        double min_mmr = 0;
                        using (MySqlCommand cmd = conn.CreateCommand())
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
                            MySqlDataReader Reader = cmd.ExecuteReader();

                            int counter = 0;
                            while (Reader.Read())
                            {
                                counter++;
                                string value = Reader["min_mmr"].Equals(DBNull.Value) ? String.Empty : Reader.GetString("min_mmr");
                                if (value == "")
                                {
                                    min_mmr = 0;
                                }
                                else
                                {
                                    min_mmr = 1800 + 40 * Convert.ToDouble(Reader["min_mmr"].Equals(DBNull.Value) ? String.Empty : Reader.GetString("min_mmr"));
                                }
                            }
                        }
                        Console.WriteLine("For type " + mmr_type_ids[type] + " in league " + leagueList[game_type] + " |" + league_tier_names[item] + " < " + min_mmr);
                        using (MySqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "INSERT into heroesprofile.league_breakdowns (type_role_hero, game_type, league_tier, min_mmr) VALUES (" +
                                type + "," +
                                game_type + "," +
                                item + "," +
                                min_mmr + ")";
                            cmd.CommandText += " ON DUPLICATE KEY UPDATE " +
                                    "type_role_hero = VALUES(type_role_hero)," +
                                    "game_type = VALUES(game_type)," +
                                    "league_tier = VALUES(league_tier)," +
                                    "min_mmr = VALUES(min_mmr)";

                            //Console.WriteLine(cmd.CommandText);
                            MySqlDataReader Reader = cmd.ExecuteReader();
                        }
                    }
                }
            }
        }
    }
}
