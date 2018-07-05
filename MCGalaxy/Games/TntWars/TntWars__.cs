/*
    Copyright 2011 MCForge
        
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    http://www.opensource.org/licenses/ecl2.php
    http://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
///////--|----------------------------------|--\\\\\\\
//////---|  TNT WARS - Coded by edh649      |---\\\\\\
/////----|                                  |----\\\\\
////-----|  Note: Double click on // to see |-----\\\\
///------|        them in the sidebar!!     |------\\\
//-------|__________________________________|-------\\
using System.Collections.Generic;
using System.Threading;

namespace MCGalaxy.Games
{
    public sealed class TntWarsGame1
    {
        //Vars
        public static List<TntWarsGame1> GameList = new List<TntWarsGame1>();
        public Level lvl;
        public TntWarsStatus GameStatus = TntWarsStatus.WaitingForPlayers;
        public int BackupNumber;
        public TntWarsGameMode GameMode = TntWarsGameMode.TDM;
        public TntWarsDifficulty Difficulty = TntWarsDifficulty.Normal;
        public int GameNumber;
        public ushort[] RedSpawn = null;
        public ushort[] BlueSpawn = null;
        
        public TWMapConfig Config = new TWMapConfig();
        public Thread Starter;

        public TntWarsGame1(Level level) {
            Starter = new Thread(Start);
            lvl = level;
        }

        //Player/Team stuff
        public List<player> Players = new List<player>();
        public class player {
            public Player p;
            public bool Red, Blue, spec;
            public int Score = 0;
            public string OrigCol;
            
            public player(Player pl) {
                p = pl; OrigCol = pl.color;
            }
        }
        public int RedScore = 0;
        public int BlueScore = 0;

        //Zones
        public List<Zone> NoTNTplacableZones = new List<Zone>();
        public List<Zone> NoBlockDeathZones = new List<Zone>();
        public class Zone {
            public ushort MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
        }

        //During Game Main Methods
        public void Start()
        {
            GameStatus = TntWarsStatus.AboutToStart;
            //Checking Backups & physics etc.
            {
                BackupNumber = lvl.Backup(true);
                if (BackupNumber <= 0)
                {
                    MessageAll(Colors.red + "Backing up Level for TNT Wars failed, Stopping game");
                    Chat.MessageOps(Colors.red + "Backing up Level for TNT Wars failed, Stopping game");
                    GameStatus = TntWarsStatus.Finished;
                    return;
                }
                Logger.Log(LogType.SystemActivity, "Backed up {0} ({1}) for TNT Wars", lvl.name, BackupNumber);
            }
            //Map stuff
            lvl.SetPhysics(3);
            //Seting Up Some Player stuff
            {
                foreach (player p in Players)
                {
                    p.p.PlayingTntWars = true;
                    p.p.CurrentAmountOfTnt = 0;
                    p.p.CurrentTntGameNumber = GameNumber;
                    if (Difficulty == TntWarsDifficulty.Easy || Difficulty == TntWarsDifficulty.Normal) p.p.TntWarsHealth = 2;
                    else p.p.TntWarsHealth = 1;
                    p.p.HarmedBy = null;
                    if (Config.InitialGracePeriod)
                    {
                        p.p.canBuild = false;
                    }
                    if (p.spec)
                    {
                        p.p.canBuild = false;
                        Player.Message(p.p, "TNT Wars: Disabled building because you are a spectator!");
                    }
                    p.p.TntWarsKillStreak = 0;
                    p.p.TntWarsScoreMultiplier = 1f;
                    p.p.TNTWarsLastKillStreakAnnounced = 0;
                    SetTitlesAndColor(p);
                }
                if (Config.InitialGracePeriod)
                {
                    MessageAll("TNT Wars: Disabled building during Grace Period!");
                }
            }
            //Spawn them (And if needed, move them to the correct level!)
            {
                foreach (player p in Players)
                {
                    if (p.p.level == lvl) continue;
                    PlayerActions.ChangeMap(p.p, lvl);
                    p.p.inTNTwarsMap = true;
                }
                if (GameMode == TntWarsGameMode.TDM) { Command.Find("Reload").Use(null, "all " + lvl.name); }//So peoples names apear above their heads in the right color!
                foreach (player p in Players)
                {
                    Command.Find("Spawn").Use(p.p, ""); //This has to be after reveal so that they spawn in the correct place!!
                    Thread.Sleep(250);
                }
            }
            //Announcing Etc.
            string Gamemode = "Free For All";
            if (GameMode == TntWarsGameMode.TDM) Gamemode = "Team Deathmatch";
            string difficulty = "Normal";
            string HitsToDie = "2";
            string explosiontime = "medium";
            string explosionsize = "normal";
            switch (Difficulty)
            {
                case TntWarsDifficulty.Easy:
                    difficulty = "Easy";
                    explosiontime = "long";
                    break;

                case TntWarsDifficulty.Normal:
                    difficulty = "Normal";
                    break;

                case TntWarsDifficulty.Hard:
                    HitsToDie = "1";
                    difficulty = "Hard";
                    break;

                case TntWarsDifficulty.Extreme:
                    HitsToDie = "1";
                    explosiontime = "short";
                    explosionsize = "big";
                    difficulty = "Extreme";
                    break;
            }
            string teamkillling = "Disabled";
            if (Config.TeamKills) teamkillling = "Enabled";
            Chat.MessageGlobal("&cTNT Wars %Son " + lvl.ColoredName + " %Shas started &3" + Gamemode + " %Swith a difficulty of &3" +
                               difficulty + " %S(&3" + HitsToDie + " %Shits to die, a &3" + explosiontime +
                               " %Sexplosion delay and with a &3" + explosionsize + " %Sexplosion size)" +
                               ", team killing is &3" + teamkillling + " %Sand you can place &3" + Config.MaxPlayerActiveTnt
                               + " %STNT at a time and there is a score limit of &3" + Config.ScoreRequired + "%S!!");
            if (GameMode == TntWarsGameMode.TDM) MessageAll("TNT Wars: Start your message with ':' to send it as a team chat!");
            //GracePeriod
            if (Config.InitialGracePeriod) //Check This Grace Stuff
            {
                GameStatus = TntWarsStatus.GracePeriod;
                int GracePeriodSecsRemaining = Config.GracePeriodSeconds;
                MessageAll("TNT Wars: Grace Period of &a" + GracePeriodSecsRemaining + " %Sseconds");
                while (GracePeriodSecsRemaining > 0)
                {
                    switch (GracePeriodSecsRemaining)
                    {
                        case 300:
                            MessageAll("TNT Wars: &35 %Sminutes remaining!"); break;
                        case 240:
                            MessageAll("TNT Wars: &34 %Sminutes remaining!"); break;
                        case 180:
                            MessageAll("TNT Wars: &33 %Sminutes remaining!"); break;
                        case 120:
                            MessageAll("TNT Wars: &32 %Sminutes remaining!"); break;
                        case 90:
                            MessageAll("TNT Wars: &31 %Sminute and &330 %Sseconds remaining!"); break;
                        case 60:
                            MessageAll("TNT Wars: &31 %Sminute remaining!"); break;
                        case 45:
                            MessageAll("TNT Wars: &345 %Sseconds remaining!"); break;
                        case 30:
                            MessageAll("TNT Wars: &330 %Sseconds remaining!"); break;
                        case 15:
                            MessageAll("TNT Wars: &315 %Sseconds remaining!"); break;
                        case 10:
                            MessageAll("TNT Wars: &310 %Sseconds remaining!"); break;
                        case 9:
                            MessageAll("TNT Wars: &39 %Sseconds remaining!"); break;
                        case 8:
                            MessageAll("TNT Wars: &38 %Sseconds remaining!"); break;
                        case 7:
                            MessageAll("TNT Wars: &37 %Sseconds remaining!"); break;
                        case 6:
                            MessageAll("TNT Wars: &36 %Sseconds remaining!"); break;
                        case 5:
                            MessageAll("TNT Wars: &35 %Sseconds remaining!"); break;
                        case 4:
                            MessageAll("TNT Wars: &34 %Sseconds remaining!"); break;
                        case 3:
                            MessageAll("TNT Wars: &33 %Sseconds remaining!"); break;
                        case 2:
                            MessageAll("TNT Wars: &32 %Sseconds remaining!"); break;
                        case 1:
                            MessageAll("TNT Wars: &31 %Ssecond remaining!"); break;
                    }
                    
                    Thread.Sleep(1000);
                    GracePeriodSecsRemaining--;
                }
                MessageAll("TNT Wars: Grace Period is over!!!!!");
                MessageAll("TNT Wars: You may now place " + Colors.red + "TNT");
            }
            MessageAll("TNT Wars: &fThe Game Has Started!!!!!");
            GameStatus = TntWarsStatus.InProgress;
            foreach (player p in Players)
            {
                if (!p.spec)
                {
                    p.p.canBuild = true;
                }
            }
            if (Config.InitialGracePeriod)
            {
                MessageAll("TNT Wars: You can now build!!");
            }
            //MainLoop
            while (!Finished())
            {
                int i = 1; //For making a top 5 (or whatever) players announcement every 3 loops (if TDM)
                Thread.Sleep(3 * 1000); if (Finished()) break;  //--\\
                Thread.Sleep(3 * 1000); if (Finished()) break;  //----\
                Thread.Sleep(3 * 1000); if (Finished()) break;  //-----> So that if it finsihes, we don't have to wait like 10 secs for the announcement!!
                Thread.Sleep(3 * 1000); if (Finished()) break;  //----/
                Thread.Sleep(3 * 1000); if (Finished()) break;  //--//
                if (GameMode == TntWarsGameMode.TDM)
                {
                    if (i < 3)
                    {
                        AnnounceScores(true, true);
                    }
                    if (i >= 3)
                    {
                        AnnounceScores(true, true, true);
                        i = 0;
                    }
                    i++;
                }
                else if (GameMode == TntWarsGameMode.FFA)
                {
                    AnnounceScores(false, true, true);
                }
            }
            END();
        }
        public void END()
        {
            GameStatus = TntWarsStatus.Finished;
            //let them build and spawn them and change playingtntwars to false
            foreach (player p in Players)
            {
                p.p.canBuild = true;
                Command.Find("Spawn").Use(p.p, "");
                p.p.PlayingTntWars = false;
            }
            //Message about winners etc.
            if (Players.Count <= 1)
            {
                Chat.MessageGlobal("&cTNT Wars %Shas ended because there are no longer enough players!");
            }
            else
            {
                Chat.MessageGlobal("&cTNT Wars %Shas ended!!");
            }
            if (GameMode == TntWarsGameMode.TDM)
            {
                if (RedScore >= BlueScore)
                {
                    Chat.MessageGlobal("TNT Wars: Team &cRed %Swon &cTNT Wars %Sby {0} points!", RedScore - BlueScore);
                }
                if (BlueScore >= RedScore)
                {
                    Chat.MessageGlobal("TNT Wars: Team &9Blue %Swon &cTNT Wars %Sby {0} points!", BlueScore - RedScore);
                }
                try
                {
                    foreach (player p in Players)
                    {
                        if (!p.spec)
                        {
                            Player.Message(p.p, "TNT Wars: You Scored " + p.Score + " points");
                        }
                    }
                }
                catch { }
                MessageAll("TNT Wars: Top Scores:");
                AnnounceScores(false, false, true);
            }
            if (GameMode == TntWarsGameMode.FFA)
            {
                int count = PlayingPlayers();
                List<player> pls = SortedByScore();
                for (int i = 0; i < count; i++) {
                    player pl = pls[i];
                    if (i == 0)
                    {
                        Chat.MessageGlobal("&cTNT Wars %S1st Place: " + pl.p.ColoredName + " %Swith a score of " + pl.p.color + pl.Score);
                    }
                    else if (i == 1)
                    {
                        MessageAll("&cTNT Wars %S2nd Place: " + pl.p.ColoredName + " %Swith a score of " + pl.p.color + pl.Score);
                    }
                    else if (i == 2)
                    {
                        MessageAll("&cTNT Wars %S3rd Place: " + pl.p.ColoredName + " %Swith a score of " + pl.p.color + pl.Score);
                    }
                    else
                    {
                        MessageAll("&cTNT Wars %S" + count + "th Place: " + pl.p.ColoredName+ " %Swith a score of " + pl.p.color + pl.Score);
                    }
                    Thread.Sleep(750); //Maybe, not sure (was 500)
                }
            }
            //Reset map
            Command.Find("Restore").Use(null, BackupNumber + " " + lvl.name);
        }

        public void MessageAll(string Message)
        {
            try {
                foreach (player p in Players) {
                    Player.Message(p.p, Message);
                }
            } catch { }
        }

        public void HandleKill(Player Killer, List<Player> Killed)
        {
            List<Player> Dead = new List<Player>();
            int damage = 1;
            int kills = 0;
            int penalty = 0;
            if (Difficulty == TntWarsDifficulty.Hard || Difficulty == TntWarsDifficulty.Extreme) {
                damage = 2;
            }
            
            foreach (Player Kld in Killed) {
                if (FindPlayer(Kld).spec) continue;
                if (!Config.TeamKills && TeamKill(Killer, Kld)) continue;
                
                if (Kld.TntWarsHealth - damage <= 0)
                {
                    Kld.TntWarsHealth = 0;
                    Dead.Add(Kld);
                    if (Config.TeamKills && TeamKill(Killer, Kld))
                    {
                        penalty += Config.ScorePerKill;
                    }
                }
                else
                {
                    Kld.TntWarsHealth -= damage;
                    Kld.HarmedBy = Killer;
                    Player.Message(Killer, "TNT Wars: You harmed " + Kld.ColoredName);
                    Player.Message(Kld, "TNT Wars: You were harmed by " + Killer.ColoredName);
                }
            }
            
            foreach (Player Died in Dead) {
                Died.TntWarsKillStreak = 0;
                Died.TntWarsScoreMultiplier = 1f;
                Died.TNTWarsLastKillStreakAnnounced = 0;
                if (Died.HarmedBy == null || Died.HarmedBy == Killer)
                {
                    if (TeamKill(Killer, Died))
                    {
                        MessageAll("TNT Wars: " + Killer.ColoredName + " %Steam killed " + Died.ColoredName);
                    }
                    else
                    {
                        MessageAll("TNT Wars: " + Killer.ColoredName + " %Skilled " + Died.ColoredName);
                        kills += 1;
                    }
                }
                else
                {
                    {
                        if (TeamKill(Killer, Died))
                        {
                            MessageAll("TNT Wars: " + Killer.ColoredName + " %Steam killed " + Died.ColoredName + " %S(with help from " + Died.HarmedBy.ColoredName + ")");
                        }
                        else
                        {
                            MessageAll("TNT Wars: " + Killer.ColoredName + " %Skilled " + Died.ColoredName + " %S(with help from " + Died.HarmedBy.ColoredName + ")");
                            kills += 1;
                        }
                    }
                    {
                        if (TeamKill(Died.HarmedBy, Died))
                        {
                            Player.Message(Died.HarmedBy, "TNT Wars: - " + Config.AssistScore + " point(s) for team kill assist!");
                            ChangeScore(Died.HarmedBy, -Config.AssistScore);
                        }
                        else
                        {
                            Player.Message(Died.HarmedBy, "TNT Wars: + " + Config.AssistScore + " point(s) for assist!");
                            ChangeScore(Died.HarmedBy, Config.AssistScore);
                        }
                    }
                    Died.HarmedBy = null;
                }
                Command.Find("Spawn").Use(Died, "");
                Died.TntWarsHealth = 2;
            }
            //Scoring
            int points = 0;
            //streaks
            Killer.TntWarsKillStreak += kills;
            if (kills >= 1 && Config.Streaks)
            {
                if (Killer.TntWarsKillStreak >= Config.StreakOneAmount && Killer.TntWarsKillStreak < Config.StreakTwoAmount && Killer.TNTWarsLastKillStreakAnnounced != Config.StreakOneAmount)
                {
                    Player.Message(Killer, "TNT Wars: Kill streak of " + Killer.TntWarsKillStreak + " (Multiplier of " + Config.StreakOneMultiplier + ")");
                    MessageAll("TNT Wars: " + Killer.ColoredName + " %Shas a kill streak of " + Killer.TntWarsKillStreak);
                    Killer.TntWarsScoreMultiplier = Config.StreakOneMultiplier;
                    Killer.TNTWarsLastKillStreakAnnounced = Config.StreakOneAmount;
                }
                else if (Killer.TntWarsKillStreak >= Config.StreakTwoAmount && Killer.TntWarsKillStreak < Config.StreakThreeAmount && Killer.TNTWarsLastKillStreakAnnounced != Config.StreakTwoAmount)
                {
                    Player.Message(Killer, "TNT Wars: Kill streak of " + Killer.TntWarsKillStreak + " (Multiplier of " + Config.StreakTwoMultiplier + " and a bigger explosion!)");
                    MessageAll("TNT Wars: " + Killer.ColoredName + " %Shas a kill streak of " + Killer.TntWarsKillStreak + " and now has a bigger explosion for their TNT!");
                    Killer.TntWarsScoreMultiplier = Config.StreakTwoMultiplier;
                    Killer.TNTWarsLastKillStreakAnnounced = Config.StreakTwoAmount;
                }
                else if (Killer.TntWarsKillStreak >= Config.StreakThreeAmount && Killer.TNTWarsLastKillStreakAnnounced != Config.StreakThreeAmount)
                {
                    Player.Message(Killer, "TNT Wars: Kill streak of " + Killer.TntWarsKillStreak + " (Multiplier of " + Config.StreakThreeMultiplier + " and you now have 1 extra health!)");
                    MessageAll("TNT Wars: " + Killer.ColoredName + " %Shas a kill streak of " + Killer.TntWarsKillStreak + " and now has 1 extra health!");
                    Killer.TntWarsScoreMultiplier = Config.StreakThreeMultiplier;
                    Killer.TNTWarsLastKillStreakAnnounced = Config.StreakThreeAmount;
                    if (Difficulty == TntWarsDifficulty.Hard || Difficulty == TntWarsDifficulty.Extreme)
                    {
                        Killer.TntWarsHealth += 2;
                    }
                    else
                    {
                        Killer.TntWarsHealth += 1;
                    }
                }
                else
                {
                    Player.Message(Killer, "TNT Wars: Kill streak of " + Killer.TntWarsKillStreak);
                }
            }
            
            points += kills * Config.ScorePerKill;
            if (kills > 1) points += kills * Config.MultiKillBonus;

            if (points > 0) {
                points = (int)(points * Killer.TntWarsScoreMultiplier);
                ChangeScore(Killer, points);
                Player.Message(Killer, "TNT Wars: + " + points + " point(s) for " + kills + " kills");
            }
            if (penalty > 0) {
                ChangeScore(Killer, -penalty);
                Player.Message(Killer, "TNT Wars: - " + penalty + " point(s) for team kill(s)!");
            }
        }

        public void ChangeScore(Player p, int Amount) {
            ChangeScore(FindPlayer(p), Amount);
        }

        public void ChangeScore(player p, int Amount) {
            p.Score += Amount;
            if (GameMode != TntWarsGameMode.TDM) return;
            if (p.Red) RedScore += Amount;
            if (p.Blue) BlueScore += Amount;
        }

        public bool InZone(ushort x, ushort y, ushort z, bool CheckForPlacingTnt) {
            List<Zone> zones = CheckForPlacingTnt ? NoTNTplacableZones : NoBlockDeathZones;
            return InZone(x, y, z, zones);
        }
        
        public bool InZone(ushort x, ushort y, ushort z, List<Zone> zones) {
            foreach (Zone Zn in zones) {
                if (x >= Zn.MinX && y >= Zn.MinY && z >= Zn.MinZ
                    && x <= Zn.MaxX && y <= Zn.MaxY && z <= Zn.MaxZ) return true;
            }
            return false;
        }
        
        public bool TeamKill(Player p1, Player p2) {
            return TeamKill(FindPlayer(p1), FindPlayer(p2));
        }

        public bool TeamKill(player p1, player p2) {
            if (GameMode == TntWarsGameMode.TDM)
            {
                if (p1.Red && p2.Red) return true;
                if (p1.Blue && p2.Blue) return true;
            }
            return false;
        }
        
        public List<player> SortedByScore() {
            List<TntWarsGame1.player> sorted = new List<TntWarsGame1.player>(Players);
            sorted.Sort((a, b) => b.Score.CompareTo(a.Score));
            return sorted;
        }

        public void AnnounceScores(bool TotalTeamScores = false, bool TheirTotalScore = false, bool TopScores = false) {
            try {
                if (TotalTeamScores)
                {
                    MessageAll("TNT Wars Scores:");
                    MessageAll(Colors.red + "RED: &f" + RedScore + " " + Colors.red + "(" + (Config.ScoreRequired - RedScore) + " needed)");
                    MessageAll(Colors.blue + "BLUE: &f" + BlueScore + " " + Colors.red + "(" + (Config.ScoreRequired - BlueScore) + " needed)");
                    Thread.Sleep(1000);
                }
                if (TopScores)
                {
                    List<player> sorted = SortedByScore();
                    int count = System.Math.Min(PlayingPlayers(), 5);
                    
                    for (int i = 0; i < count; i++) {
                        MessageAll((i + 1) + ": " + sorted[i].p.name + " - " + sorted[i].Score);
                        Thread.Sleep(500); //Maybe, not sure (250??)
                    }
                    Thread.Sleep(1000);
                }
                if (TheirTotalScore)
                {
                    foreach (player p in Players)
                    {
                        if (p.spec) continue;
                        Player.Message(p.p, "TNT Wars: Your Score is &a" + p.Score);
                    }
                    Thread.Sleep(1000);
                }
            }
            catch { }
        }

        public bool Finished() {
            if (GameMode == TntWarsGameMode.TDM && (RedScore >= Config.ScoreRequired || BlueScore >= Config.ScoreRequired))
                return true;
            
            if (GameMode == TntWarsGameMode.FFA) {
                try
                {
                    foreach (player p in Players) {
                        if (p.Score >= Config.ScoreRequired) return true;
                    }
                }
                catch { }
            }
            if (PlayingPlayers() <= 1) return true;
            return GameStatus == TntWarsStatus.Finished;
        }

        //enums
        public enum TntWarsStatus {
            WaitingForPlayers = 0,
            AboutToStart = 1,
            GracePeriod = 2,
            InProgress = 3,
            Finished = 4
        }

        //Other stuff
        public int RedTeam() {
            int count = 0;
            foreach (player p in Players) {
                if (p.Red) count++;
            }
            return count;
        }

        public int BlueTeam() {
            int count = 0;
            foreach (player p in Players) {
                if (p.Blue) count++;
            }
            return count;
        }

        public int PlayingPlayers() {
            int count = 0;
            foreach (player p in Players) {
                if (!p.spec) count++;
            }
            return count;
        }

        public static void SetTitlesAndColor(player p, bool reset = false) {
            try {
                if (reset) {
                    p.p.color = p.OrigCol;
                    p.p.SetPrefix();
                } else {
                    if (p.Red) p.p.color = Colors.red;
                    if (p.Blue) p.p.color = Colors.blue;
                    p.p.SetPrefix();
                }
            } catch { }
        }

        public static TntWarsGame1 Find(Level level) {
            foreach (TntWarsGame1 g in GameList) {
                if (g.lvl == level) return g;
            }
            return null;
        }

        public static TntWarsGame1 FindFromGameNumber(int num) {
            foreach (TntWarsGame1 g in GameList) {
                if (g.GameNumber == num) return g;
            }
            return null;
        }

        public player FindPlayer(Player pla) {
            foreach (player p in Players) {
                if (p.p == pla) return p;
            }
            return null;
        }

        public static TntWarsGame1 GameIn(Player p) {
            TntWarsGame1 it = TntWarsGame1.Find(p.level);
            if (it != null) return it;
            it = FindFromGameNumber(p.CurrentTntGameNumber);
            return it;
        }
        
        public void AutoAssignTeam(player pl) {
            pl.Red = false; pl.Blue = false;
            int red = RedTeam(), blue = BlueTeam();
            
            if (blue > red) {
                pl.Red = true;
            } else if (red > blue) {
                pl.Blue = true;
            } else if (RedScore > BlueScore) {
                pl.Blue = true;
            } else if (BlueScore > RedScore) {
                pl.Red = true;
            } else {
                pl.Red = true;
            }
        }
    }
}