using System.Collections.Generic;
using DiceVoders.Core.Encounter;
using Xunit;

namespace DiceVoders.Tests
{
    public class EncounterDiffTests
    {
        private static EncounterSnapshot Snap(string phase, double total = 0, int hearts = 5, int turns = 4, params int[] dice) => new EncounterSnapshot
        {
            Phase = phase,
            TurnsLeft = turns,
            TurnsText = turns + " Turns",
            Hearts = hearts,
            Total = total,
            TotalText = total.ToString(),
            Target = 100,
            TargetText = "100",
            TargetReachedText = "TARGET REACHED!",
            Dice = new List<int>(dice),
            ColumnAttack = new List<string> { "0", "0", "0", "0", "0", "0" },
        };

        [Fact]
        public void Nothing_changed_says_nothing()
        {
            Assert.Empty(EncounterDiff.Lines(Snap("Player", dice: new[] { 3, 3, 5 }), Snap("Player", dice: new[] { 3, 3, 5 })));
        }

        [Fact]
        public void Leaving_the_player_phase_with_more_attack_says_the_columns_then_the_total()
        {
            var now = Snap("Draft", total: 30, turns: 3);
            now.ColumnAttack[2] = "20";
            now.ColumnAttack[4] = "10";
            Assert.Equal(new[] { "column 3: 20, column 5: 10, 30/100 attack", "Shop" },
                EncounterDiff.Lines(Snap("Player", dice: new[] { 3, 3, 5 }), now));
        }

        [Fact]
        public void Passing_the_target_says_it_was_reached()
        {
            var lines = EncounterDiff.Lines(Snap("Player", total: 90), Snap("Draft", total: 120));
            Assert.Contains("TARGET REACHED!", lines);
            Assert.Empty(EncounterDiff.Lines(Snap("Draft", total: 120), Snap("Draft", total: 150)).FindAll(l => l == "TARGET REACHED!"));
        }

        [Fact]
        public void A_new_turn_says_the_turns_and_the_roll()
        {
            Assert.Equal(new[] { "3 Turns, rolled 2, 4, 6" },
                EncounterDiff.Lines(Snap("Draft", turns: 3), Snap("Player", turns: 3, dice: new[] { 2, 4, 6 })));
        }

        [Fact]
        public void A_reroll_in_the_turn_says_the_new_faces()
        {
            Assert.Equal(new[] { "rolled 1, 5, 6" },
                EncounterDiff.Lines(Snap("Player", dice: new[] { 3, 3, 5 }), Snap("Player", dice: new[] { 1, 5, 6 })));
        }

        [Fact]
        public void The_tray_resorting_its_dice_is_no_roll()
        {
            Assert.Empty(EncounterDiff.Lines(Snap("Player", dice: new[] { 4, 3, 5 }), Snap("Player", dice: new[] { 3, 4, 5 })));
        }

        [Fact]
        public void A_die_still_rolling_waits()
        {
            Assert.Empty(EncounterDiff.Lines(Snap("Draft"), Snap("Player", dice: new[] { 2, 0, 6 })));
        }

        [Fact]
        public void A_lost_heart_is_said()
        {
            Assert.Contains("Hearts 4", EncounterDiff.Lines(Snap("Draft", hearts: 5), Snap("Draft", hearts: 4)));
        }

        private static UnitSnap Unit(string id, string name, int x, int y, double strength, params string[] states) => new UnitSnap
        {
            Id = id, Name = name, X = x, Y = y, Strength = strength, StrengthText = strength.ToString(), States = new List<string>(states),
        };

        private static List<string> Gains(params (int Column, string Gain)[] gains)
        {
            var list = new List<string> { null, null, null, null, null, null };
            foreach (var (column, gain) in gains) list[column - 1] = gain;
            return list;
        }

        [Fact]
        public void A_column_step_says_its_gain_then_what_it_set_off()
        {
            var prev = Snap("Scoring");
            prev.Units.Add(Unit("Migo#1", "Migo", 2, 0, 10));
            var now = Snap("Scoring");
            now.Units.Add(Unit("Migo#1", "Migo", 2, 0, 15));
            Assert.Equal(new[] { "column 3: 40, column 5: 10", "Migo at 3, 1: 15 strength" },
                EncounterDiff.Step(prev, now, 3, 1, Gains((3, "40"), (5, "10")), null));
        }

        [Fact]
        public void A_base_die_column_that_changed_nothing_is_not_said()
        {
            Assert.Empty(EncounterDiff.Step(Snap("Scoring"), Snap("Scoring"), 4, 2, Gains(), null));
        }

        [Fact]
        public void A_mini_die_is_always_said_and_its_spending_is_not()
        {
            var prev = WithMiniDice(Snap("Scoring"), 1);
            var now = WithMiniDice(Snap("Scoring"), 0);
            Assert.Equal(new[] { "Mini-Dice, column 1: 0" }, EncounterDiff.Step(prev, now, 1, 1, Gains(), "Mini-Dice"));
            Assert.Equal(new[] { "3 Mini-Dice, column 1: 0" }, EncounterDiff.Step(prev, now, 1, 3, Gains(), "Mini-Dice"));
        }

        [Fact]
        public void Steps_then_the_shop_say_the_total_once()
        {
            Assert.Equal(new[] { "60/100 attack", "Shop" },
                EncounterDiff.Lines(Snap("Scoring", total: 0), Snap("Draft", total: 60, turns: 3)));
        }

        private static EncounterSnapshot WithMiniDice(EncounterSnapshot s, int count)
        {
            s.Tokens.Add(new Figure { Key = "minidice", Label = "Mini-Dice", Value = count });
            return s;
        }

        [Fact]
        public void A_token_change_says_the_token()
        {
            var prev = Snap("Draft");
            prev.Tokens.Add(new Figure { Key = "chrono", Label = "Chrono", Value = 2 });
            var now = Snap("Draft");
            now.Tokens.Add(new Figure { Key = "chrono", Label = "Chrono", Value = 4 });
            Assert.Equal(new[] { "Chrono 4" }, EncounterDiff.Lines(prev, now));
        }

        [Fact]
        public void A_unit_gone_is_destroyed_where_it_stood()
        {
            var prev = Snap("Draft");
            prev.Units.Add(Unit("TimeAngel#1", "Time Angel", 5, 0, 10));
            Assert.Equal(new[] { "Time Angel at 6, 1 destroyed" }, EncounterDiff.Lines(prev, Snap("Draft")));
        }

        [Fact]
        public void A_unit_placed_is_added_where_it_stands()
        {
            var now = Snap("Draft");
            now.Units.Add(Unit("Migo#4", "Migo", 3, 0, 10));
            Assert.Equal(new[] { "Migo added at 4, 1" }, EncounterDiff.Lines(Snap("Draft"), now));
        }

        [Fact]
        public void Every_unit_gaining_alike_is_one_line()
        {
            var prev = Snap("Draft");
            var now = Snap("Draft");
            prev.Units.Add(Unit("Migo#1", "Migo", 1, 0, 10)); prev.Units.Add(Unit("Migo#2", "Migo", 2, 0, 10));
            now.Units.Add(Unit("Migo#1", "Migo", 1, 0, 15)); now.Units.Add(Unit("Migo#2", "Migo", 2, 0, 15));
            Assert.Equal(new[] { "all units +5 strength" }, EncounterDiff.Lines(prev, now));
        }

        [Fact]
        public void One_unit_gaining_says_its_new_strength()
        {
            var prev = Snap("Draft");
            var now = Snap("Draft");
            prev.Units.Add(Unit("Migo#1", "Migo", 1, 0, 10)); prev.Units.Add(Unit("Migo#2", "Migo", 2, 0, 10));
            now.Units.Add(Unit("Migo#1", "Migo", 1, 0, 25)); now.Units.Add(Unit("Migo#2", "Migo", 2, 0, 10));
            Assert.Equal(new[] { "Migo at 2, 1: 25 strength" }, EncounterDiff.Lines(prev, now));
        }

        [Fact]
        public void States_gained_and_lost_are_said()
        {
            var prev = Snap("Draft");
            var now = Snap("Draft");
            prev.Units.Add(Unit("Migo#1", "Migo", 1, 0, 10, "Shock"));
            now.Units.Add(Unit("Migo#1", "Migo", 1, 0, 10, "Shielded"));
            Assert.Equal(new[] { "Migo at 2, 1: Shielded", "Migo at 2, 1: Shock lost" }, EncounterDiff.Lines(prev, now));
        }

        [Fact]
        public void A_unit_moved_says_where_to()
        {
            var prev = Snap("Draft");
            var now = Snap("Draft");
            prev.Units.Add(Unit("Migo#1", "Migo", 1, 0, 10));
            now.Units.Add(Unit("Migo#1", "Migo", 2, 1, 10));
            Assert.Equal(new[] { "Migo moved to 3, 2" }, EncounterDiff.Lines(prev, now));
        }

        [Fact]
        public void Artifacts_added_moved_and_levelled_are_said()
        {
            var prev = Snap("Draft");
            var now = Snap("Draft");
            prev.Artifacts.Add(new ArtifactSnap { Id = 7, Name = "Meteor", Slot = 4, Level = 1, Xp = 9, XpNeeded = 10 });
            now.Artifacts.Add(new ArtifactSnap { Id = 7, Name = "Meteor", Slot = 2, Level = 2, Xp = 4, XpNeeded = 20 });
            now.Artifacts.Add(new ArtifactSnap { Id = 9, Name = "Pyramid", Slot = 0, Level = 1 });
            Assert.Equal(new[] { "Meteor moved to column 3", "Meteor level 2", "Meteor: 4/20 XP", "Pyramid added, column 1" }, EncounterDiff.Lines(prev, now));
        }

        [Fact]
        public void Xp_gained_is_said_and_none_for_an_artifact_without_it()
        {
            var prev = Snap("Draft");
            var now = Snap("Draft");
            prev.Artifacts.Add(new ArtifactSnap { Id = 7, Name = "Meteor", Slot = 4, Level = 1, Xp = 4, XpNeeded = 10 });
            now.Artifacts.Add(new ArtifactSnap { Id = 7, Name = "Meteor", Slot = 4, Level = 1, Xp = 9, XpNeeded = 10 });
            prev.Artifacts.Add(new ArtifactSnap { Id = 1, Name = "The Tutorial", Slot = 9, Level = 1, Xp = -1 });
            now.Artifacts.Add(new ArtifactSnap { Id = 1, Name = "The Tutorial", Slot = 9, Level = 1, Xp = -1 });
            Assert.Equal(new[] { "Meteor: 9/10 XP" }, EncounterDiff.Lines(prev, now));
        }

        [Fact]
        public void A_new_tile_is_said()
        {
            var now = Snap("Draft");
            now.Built.Add((3, 0));
            Assert.Equal(new[] { "new tile at 4, 1" }, EncounterDiff.Lines(Snap("Draft"), now));
        }

        [Fact]
        public void A_purchase_is_no_shop_change_but_a_reroll_is()
        {
            var prev = Snap("Draft");
            prev.Shop.AddRange(new[] { "Migo", "Comet", "Build Tile" });
            var bought = Snap("Draft");
            bought.Shop.AddRange(new[] { "Migo", "Build Tile" });
            Assert.Empty(EncounterDiff.Lines(prev, bought));
            var rerolled = Snap("Draft");
            rerolled.Shop.AddRange(new[] { "Pyramid", "Swap or Move" });
            Assert.Equal(new[] { "Shop: Pyramid, Swap or Move" }, EncounterDiff.Lines(prev, rerolled));
        }

        [Fact]
        public void The_offer_arriving_after_the_shop_opens_is_no_change()
        {
            var opened = Snap("Draft");
            var filled = Snap("Draft");
            filled.Shop.AddRange(new[] { "Swap or Move", "Get +1 Chrono", "Shooter" });
            Assert.Empty(EncounterDiff.Lines(opened, filled));
        }

        [Fact]
        public void A_new_target_is_said_and_its_resets_are_not()
        {
            var prev = Snap("Simulation", total: 110, turns: 1);
            prev.TargetName = "Defeat the P.O.G.";
            prev.Tokens.Add(new Figure { Key = "cash", Label = "Cash", Value = 0 });
            var now = Snap("Draft", total: 0, turns: 4);
            now.TargetName = "Defeat the N.B.S.";
            now.Act = "Act 1-2";
            now.Tokens.Add(new Figure { Key = "cash", Label = "Cash", Value = 3 });
            Assert.Equal(new[] { "Defeat the N.B.S., Act 1-2", "Shop" }, EncounterDiff.Lines(prev, now));
        }
    }
}
