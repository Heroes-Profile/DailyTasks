using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DailyTasks
{
    class LeaderboardPlayerData
    {
        public int game_type { get; set; }
        public int season { get; set; }
        public int player_hero_role { get; set; }
        public int rank_counter { get; set; }
        public string split_battletag { get; set; }
        public string battletag { get; set; }
        public DateTime latest_game_played { get; set; }

        public int blizz_id { get; set; }
        public int region { get; set; }
        public double win_rate { get; set; }
        public int win { get; set; }
        public int loss { get; set; }
        public int games_played { get; set; }
        public double conservative_rating { get; set; }
        public double rating { get; set; }
        public int cache_number { get; set; }
    }
}
