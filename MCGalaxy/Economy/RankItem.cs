﻿/*
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
using System;
using System.Collections.Generic;
using System.IO;
using MCGalaxy.Commands;

namespace MCGalaxy.Eco {
    
    public sealed class RankItem : Item {
        
        public RankItem() {
            Aliases = new string[] { "rank", "ranks", "rankup" };
        }
        
        public override string Name { get { return "Rank"; } }
        
        public override string ShopName { get { return "Rankup"; } }
        
        public List<RankEntry> Ranks = new List<RankEntry>();        
        public class RankEntry {
            public LevelPermission Perm;
            public int Price = 1000;
        }
        
        public override void Parse(string line, string[] args) {
            if (!args[1].CaselessEq("price")) return;
            LevelPermission perm = Group.ParsePermOrName(args[2], LevelPermission.Null);
            if (perm == LevelPermission.Null) return;
            
            RankEntry rank = GetOrAdd(perm);
            rank.Price = int.Parse(args[3]);
        }
        
        public override void Serialise(StreamWriter writer) {
            foreach (RankEntry rank in Ranks) {
                writer.WriteLine("rank:price:" + (int)rank.Perm + ":" + rank.Price);
            }
        }
        
        public RankEntry GetOrAdd(LevelPermission perm) {
            RankEntry rank = Find(perm);
            if (rank != null) return rank;
            
            rank = new RankItem.RankEntry(); rank.Perm = perm;
            Ranks.Add(rank);
            Ranks.Sort((a, b) => a.Perm.CompareTo(b.Perm));
            return rank;
        }
        
        public RankEntry Find(LevelPermission perm) {
            foreach (RankEntry rank in Ranks) {
                if (rank.Perm == perm) return rank;
            }
            return null;
        }        
        public bool Remove(LevelPermission perm) { return Ranks.Remove(Find(perm)); }

        RankEntry NextRank(Player p) {
            if (Player.IsSuper(p)) return null;
            foreach (RankEntry rank in Ranks) {
                if (rank.Perm > p.Rank) return rank;
            }
            return null;
        }
        
        protected internal override void OnBuyCommand(Player p, string message, string[] args) {
            if (args.Length >= 2) {
                Player.Message(p, "%WYou cannot provide a rank name, use %T/Buy rank %Wto buy the NEXT rank."); return;
            }
            
            RankEntry nextRank = NextRank(p);
            if (nextRank == null) {
                Player.Message(p, "%WYou are already at or past the max buyable rank"); return;
            }           
            if (p.money < nextRank.Price) {
                Player.Message(p, "%WYou don't have enough &3" + ServerConfig.Currency + " %Wto buy the next rank"); return;
            }
            
            string rankName = Group.Find(nextRank.Perm).Name; // TODO: What if null reference happens here
            Command.Find("SetRank").Use(null, p.name + " " + rankName);
            Player.Message(p, "You bought the rank " + p.group.ColoredName);
            Economy.MakePurchase(p, nextRank.Price, "&3Rank: " + p.group.ColoredName);
        }
        
        protected internal override void OnSetupCommand(Player p, string[] args) {
            if (args[1].CaselessEq("price")) {
                Group grp = Matcher.FindRanks(p, args[2]);
                if (grp == null) return;
                if (p != null && p.Rank < grp.Permission) { Player.Message(p, "%WCannot set price of a rank higher than yours."); return; }
                
                int cost = 0;
                if (!CommandParser.GetInt(p, args[3], "Price", ref cost, 0)) return;
                Player.Message(p, "&aSet price of rank {0} &ato &f{1} &3{2}", grp.ColoredName, cost, ServerConfig.Currency);
                GetOrAdd(grp.Permission).Price = cost;
            } else if (Command.IsDeleteCommand(args[1])) {
                Group grp = Matcher.FindRanks(p, args[2]);
                if (grp == null) return;
                if (p != null && p.Rank < grp.Permission) { Player.Message(p, "%WCannot remove a rank higher than yours."); return; }
                
                if (Remove(grp.Permission)) {
                    Player.Message(p, "&aMade rank {0} &ano longer buyable", grp.ColoredName);
                } else {
                    Player.Message(p, "%WThat rank was not buyable to begin with.");
                }
            } else {
                OnSetupCommandHelp(p);
            }
        }
        
        protected internal override void OnSetupCommandHelp(Player p) {
            base.OnSetupCommandHelp(p);
            Player.Message(p, "%T/Eco rank price [rank] [amount]");
            Player.Message(p, "%HSets how many &3{0} %Hthat rank costs.", ServerConfig.Currency);
            Player.Message(p, "%T/Eco rank remove [rank]");
            Player.Message(p, "%HMakes that rank no longer buyable");
        }

        protected internal override void OnStoreOverview(Player p) {
            RankEntry next = NextRank(p);
            if (next == null) {
                Player.Message(p, "&6Rankup %S- %Wno further ranks to buy.");
            } else {
                Player.Message(p, "&6Rankup to {0} %S- &a{1} %S{2}",
                               Group.GetColoredName(next.Perm), next.Price, ServerConfig.Currency);
            }
        }
        
        protected internal override void OnStoreCommand(Player p) {
            Player.Message(p, "%T/Buy rankup");
            if (Ranks.Count == 0) {
                Player.Message(p, "%WNo ranks have been setup be buyable. See %T/eco help rank"); return;
            }
            
            LevelPermission maxRank = Ranks[Ranks.Count - 1].Perm;
            Player.Message(p, "&fThe highest buyable rank is: {0}", Group.GetColoredName(maxRank));
            Player.Message(p, "%WYou can only buy ranks one at a time, in sequential order.");
            
            foreach (RankEntry rank in Ranks) {
                Player.Message(p, "&6{0} %S- &a{1} %S{2}",
                               Group.GetColoredName(rank.Perm), rank.Price, ServerConfig.Currency);
            }
        }
    }
}
