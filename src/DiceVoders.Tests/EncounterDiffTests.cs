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
            Assert.Equal(new[] { "column 3: 20, column 5: 10, 30/100 attack", "shop" },
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
    }
}
