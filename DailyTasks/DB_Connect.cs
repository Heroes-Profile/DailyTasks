﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DailyTasks
{
    class DB_Connect
    {
        //Need to pull from ENV
        public string heroesprofile_config =
          "SERVER=localhost;" +
          "DATABASE=heroesprofile;" +
          "UID=root;" +
          "PASSWORD=;" +
          "Charset=utf8;";
    }
}
