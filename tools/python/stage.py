"""Stage encounter scenarios in the running game, through the dev server's /eval and the game's own
hidden debug panel (StarVaders.DebugController: in the encounter scene, inactive, never set up).

    uv run python tools/python/stage.py <command> [args]

  state                     phase, target, act and round, pilot order, tokens, hearts, Mult
  skip                      the debug panel's Skip Round (one target; run again for more)
  pilot NAME [--index I]    put a pilot in the run's pilot order (default: the next undefeated slot)
  pilots                    every pilot name
  offer NAME [--slot S]     make shop choice S (default 0) offer a unit, an artifact or a spell by its
                            game name (EntityName / ArtifactName / DraftType: Oculus, FourDPrinter,
                            Swap...); in the shop only
  find TEXT                 unit and artifact names containing TEXT
  gain money|budge|chrono|heart [N]   the debug panel's Gain buttons, N times (default 1)
  set VALUE N               set an EncounterValue (Hearts, GlobalMult, TotalScore...) to N

Uses only the game's own methods and data. Stage on the Unlock All test profile; ask before
anything that may award a Steam achievement (a run won, a one-turn KO): they are account-wide and
re-sync from the save at the next launch.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from dev import call  # noqa: E402

PRELUDE = """
var c = UnityEngine.Object.FindObjectOfType<StarVaders.EncounterController>();
if (c == null) return "not in an encounter";
var m = c.EncounterModel;
StarVaders.DebugController Debug()
{
    var all = UnityEngine.Object.FindObjectsOfType<StarVaders.DebugController>(true);
    if (all.Length == 0) return null;
    var d = all[0];
    if (d.EncounterController == null || d.EncounterController.Pointer != c.Pointer)
    {
        // Fills the panel's state (controller, every item, a pilot list) and opens its canvas;
        // closed again at once.
        d.gameObject.SetActive(true);
        d.SetupDebug(c);
        d.CloseDebug();
    }
    return d;
}
"""

STATE = """
var order = new System.Collections.Generic.List<string>();
foreach (var p in m.PilotOrder) order.Add(p.ToString());
string I(EncounterValue v) => m.GetIntValue(v).ToString();
string B(EncounterValue v) => m.GetBigDoubleValue(v).ToString();
return "phase " + c.CurrentPhase + ", act " + I(EncounterValue.ActNumber) + " round " + I(EncounterValue.RoundNumber)
    + ", pilots " + string.Join(" ", order) + " (defeated " + m.PilotsDefeatedCount + ", current " + m.GetCurrentPilot() + ")"
    + ", attack " + B(EncounterValue.TotalScore) + "/" + B(EncounterValue.TargetTributeScore)
    + ", turns " + I(EncounterValue.TurnsRemaining) + ", hearts " + I(EncounterValue.Hearts)
    + ", cash " + I(EncounterValue.ShopToken) + ", budge " + I(EncounterValue.BudgeToken) + ", chrono " + I(EncounterValue.ChronoToken)
    + ", mult " + B(EncounterValue.GlobalMult);
"""


def run(body: str) -> int:
    print(call("/eval", body=PRELUDE + body, timeout=90).rstrip())
    return 0


def cs(s: str) -> str:
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("state", "skip", "pilots"):
        sub.add_parser(name)
    p = sub.add_parser("pilot"); p.add_argument("name"); p.add_argument("--index", type=int)
    p = sub.add_parser("offer"); p.add_argument("name"); p.add_argument("--slot", type=int, default=0)
    p = sub.add_parser("find"); p.add_argument("text")
    p = sub.add_parser("gain"); p.add_argument("what", choices=("money", "budge", "chrono", "heart")); p.add_argument("n", type=int, nargs="?", default=1)
    p = sub.add_parser("set"); p.add_argument("value"); p.add_argument("n", type=float)
    args = ap.parse_args()

    if args.cmd == "state":
        return run(STATE)
    if args.cmd == "skip":
        return run("var d = Debug(); if (d == null) return \"no debug panel\"; d.SkipRound(); return \"skip round queued\";")
    if args.cmd == "pilots":
        return run("return string.Join(\" \", System.Enum.GetNames(typeof(StarVaders.PilotName)));")
    if args.cmd == "pilot":
        index = "m.PilotsDefeatedCount" if args.index is None else str(args.index)
        return run(f"""
StarVaders.PilotName pilot;
if (!System.Enum.TryParse({cs(args.name)}, true, out pilot)) return "no pilot named " + {cs(args.name)};
int i = {index};
if (i < 0 || i >= m.PilotOrder.Count) return "index " + i + " outside the order (" + m.PilotOrder.Count + ")";
m.PilotOrder[i] = pilot;
c.EncounterView.MapPanel.UpdateMap(c);
""" + STATE)
    if args.cmd == "find":
        return run(f"""
var hits = new System.Collections.Generic.List<string>();
foreach (var n in System.Enum.GetNames(typeof(StarVaders.EntityName))) if (n.IndexOf({cs(args.text)}, System.StringComparison.OrdinalIgnoreCase) >= 0) hits.Add("unit " + n);
foreach (var n in System.Enum.GetNames(typeof(StarVaders.ArtifactName))) if (n.IndexOf({cs(args.text)}, System.StringComparison.OrdinalIgnoreCase) >= 0) hits.Add("artifact " + n);
foreach (var n in System.Enum.GetNames(typeof(StarVaders.DraftType))) if (n.IndexOf({cs(args.text)}, System.StringComparison.OrdinalIgnoreCase) >= 0) hits.Add("spell " + n);
return hits.Count == 0 ? "none" : string.Join(", ", hits);
""")
    if args.cmd == "offer":
        return run(f"""
if (c.CurrentPhase != StarVaders.Phase.Draft) return "not in the shop (" + c.CurrentPhase + ")";
var choices = c.DraftPanel.DraftChoices;
int s = {args.slot};
if (s < 0 || s >= choices.Count) return "slot " + s + " outside the shop (" + choices.Count + ")";
StarVaders.EntityName unit; StarVaders.ArtifactName artifact; StarVaders.DraftType spell;
if (System.Enum.TryParse({cs(args.name)}, true, out unit)) choices[s].DebugInitializeUnit(c, s, unit);
else if (System.Enum.TryParse({cs(args.name)}, true, out artifact)) choices[s].Initialize(c, StarVaders.DraftType.Artifact, artifact, 0f, s, false, 0f, true);
else if (System.Enum.TryParse({cs(args.name)}, true, out spell) && spell != StarVaders.DraftType.None) choices[s].Initialize(c, spell, StarVaders.ArtifactName.None, 0f, s, false, 0f, true);
else return "no unit or artifact named " + {cs(args.name)} + " (try: stage.py find)";
return "slot " + s + " now offers " + {cs(args.name)};
""")
    if args.cmd == "gain":
        method = {"money": "GainMoney", "budge": "GainBudge", "chrono": "GainChrono", "heart": "GainHeart"}[args.what]
        return run(f"var d = Debug(); if (d == null) return \"no debug panel\"; for (int k = 0; k < {args.n}; k++) d.{method}(); return \"queued {args.n} x {method}\";")
    if args.cmd == "set":
        return run(f"""
EncounterValue v;
if (!System.Enum.TryParse({cs(args.value)}, true, out v)) return "no EncounterValue named " + {cs(args.value)};
if (m.IntValues.ContainsKey(v)) {{ Il2CppSystem.Object boxed = (int){args.n}; m.SetValue(v, boxed); }}
else m.SetValue(v, new BreakInfinity.BigDouble({args.n}).BoxIl2CppObject());
return v + " = " + (m.IntValues.ContainsKey(v) ? m.GetIntValue(v).ToString() : m.GetBigDoubleValue(v).ToString());
""")
    return 2


if __name__ == "__main__":
    sys.exit(main())
