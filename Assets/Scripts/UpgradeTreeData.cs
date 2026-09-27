using System.Collections.Generic;

public enum UpgradeBranch { None, Ball, Flair, Cue, Table }
public enum UpgradeNodeType { Root, Stat, Conditional, Twist, Milestone, Exclusive, Capstone }

public class UpgradeNodeDef
{
    public readonly string id;
    public readonly string label;
    public readonly UpgradeBranch branch;
    public readonly UpgradeNodeType type;
    public readonly string parent;        // primary unlock link, toward the centre - null for the centre/capstone
    public readonly string extraParent;   // a second node that also unlocks this one, if any
    public readonly string exclusiveWith; // buying this locks this other node forever, if any
    public readonly string description;   // what it actually does, shown in the hover tooltip

    public UpgradeNodeDef(string id, string label, UpgradeBranch branch, UpgradeNodeType type,
        string parent = null, string extraParent = null, string exclusiveWith = null, string description = "")
    {
        this.id = id;
        this.label = label;
        this.branch = branch;
        this.type = type;
        this.parent = parent;
        this.extraParent = extraParent;
        this.exclusiveWith = exclusiveWith;
        this.description = description;
    }
}

// The full upgrade tree, transcribed from the design doc: 1 centre + 29 ball + 23 flair + 18 cue +
// 17 table + 1 capstone = 89 nodes.
//
// Almost none of these have a real effect in the game yet - the doc assumes systems (chains, nights,
// special ball types, a second cue ball...) that don't exist in the code. Buying a node just marks it
// owned in UpgradeProgress; the only ones that do anything today are the aim guide, power, smooth felt
// and lively rails chains, since those map onto upgrades GameEngine/CueStick/Ball already have. See
// UpgradeTreeNodeUI.ApplyRealEffect for exactly which ids those are.
public static class UpgradeTreeData
{
    public const string CentreId = "centre";
    public const string CapstoneId = "capstone";

    // The capstone needs all four of these owned, not just one - a special case, unlike every other
    // node's parent/extraParent "either one" rule.
    public static readonly string[] CapstoneRequires =
    {
        "ball_reading_the_rack", "flair_run_the_table", "cue_seeing_the_table", "table_know_the_rails",
    };

    public static readonly UpgradeNodeDef[] All =
    {
        new UpgradeNodeDef(CentreId, "Rack 'Em", UpgradeBranch.None, UpgradeNodeType.Root,
            description: "Free. The tutorial node - unlocks the rest of the tree."),

        // ---- Ball (29): ball count, ball value, special balls ----
        new UpgradeNodeDef("ball_second", "Second Ball", UpgradeBranch.Ball, UpgradeNodeType.Stat, CentreId,
            description: "+1 ball (2 total)."),
        new UpgradeNodeDef("ball_warm_up", "Warm Up", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_second",
            description: "Ball Value I - +V to every ball."),
        new UpgradeNodeDef("ball_lucky_break", "Lucky Break", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_warm_up",
            description: "Each ball has a 5% chance to spawn as a special type when racked. Unlocks Gold (Vx3)."),
        new UpgradeNodeDef("ball_loaded_dice_1", "Loaded Dice I", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_lucky_break",
            description: "+Special ball spawn chance."),
        new UpgradeNodeDef("ball_vault_ball", "Vault Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_loaded_dice_1",
            description: "Unlocks Vault: worth 0 unless pocketed in a chain of 3+, then pays x10."),
        new UpgradeNodeDef("ball_glass_ball", "Glass Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_vault_ball",
            description: "Unlocks Glass: shatters after 3 hits, pays on the spot."),
        new UpgradeNodeDef("ball_high_rollers_1", "High Rollers I", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_glass_ball", "ball_money_ball",
            description: "Special balls pay x1.5."),
        new UpgradeNodeDef("ball_collector", "Collector", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_high_rollers_1",
            description: "Pocketing 2+ special balls of one type in a round pays a bonus."),
        new UpgradeNodeDef("ball_high_rollers_2", "High Rollers II", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_collector",
            description: "Special balls pay x1.5 more."),
        new UpgradeNodeDef("ball_loaded_dice_2", "Loaded Dice II", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_loaded_dice_1",
            description: "+Special ball spawn chance."),
        new UpgradeNodeDef("ball_loaded_dice_3", "Loaded Dice III", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_loaded_dice_2",
            description: "+Special ball spawn chance."),
        new UpgradeNodeDef("ball_reading_the_rack", "Reading the Rack", UpgradeBranch.Ball, UpgradeNodeType.Milestone, "ball_loaded_dice_2", "ball_big_rack",
            description: "Milestone. Every rack has at least one special ball."),
        new UpgradeNodeDef("ball_hot_hand", "Hot Hand", UpgradeBranch.Ball, UpgradeNodeType.Exclusive, "ball_reading_the_rack", null, "ball_slow_burn",
            description: "Exclusive with Slow Burn. First shot of every round gains triple M."),
        new UpgradeNodeDef("ball_slow_burn", "Slow Burn", UpgradeBranch.Ball, UpgradeNodeType.Exclusive, "ball_reading_the_rack", null, "ball_hot_hand",
            description: "Exclusive with Hot Hand. Every ball gains a little M for every shot it stays on the table."),
        new UpgradeNodeDef("ball_hot_ball", "Hot Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_lucky_break",
            description: "Unlocks Hot: gains double M when hit."),
        new UpgradeNodeDef("ball_wild_ball", "Wild Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_hot_ball",
            description: "Unlocks Wild: random x0-x5 on top of normal payout."),
        new UpgradeNodeDef("ball_money_ball", "Money Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_wild_ball",
            description: "Unlocks Money Ball: can't be pocketed until it's the last ball. V is 0 until then, but M still stacks."),
        new UpgradeNodeDef("ball_value_2", "Ball Value II", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_warm_up", "ball_third_ball",
            description: "+V to every ball."),
        new UpgradeNodeDef("ball_value_3", "Ball Value III", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_value_2",
            description: "+V to every ball."),
        new UpgradeNodeDef("ball_last_ball_standing", "Last Ball Standing", UpgradeBranch.Ball, UpgradeNodeType.Conditional, "ball_value_3",
            description: "The last ball left on the table each round gets +100% V."),
        new UpgradeNodeDef("ball_value_4", "Ball Value IV", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_value_3",
            description: "+V to every ball."),
        new UpgradeNodeDef("ball_third_ball", "Third Ball", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_second",
            description: "+1 ball (3 total)."),
        new UpgradeNodeDef("ball_small_rack", "Small Rack", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_third_ball",
            description: "+2 balls (5 total)."),
        new UpgradeNodeDef("ball_half_rack", "Half Rack", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_small_rack",
            description: "+2 balls (7 total)."),
        new UpgradeNodeDef("ball_big_rack", "Big Rack", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_half_rack",
            description: "+3 balls (10 total)."),
        new UpgradeNodeDef("ball_full_rack", "Full Rack", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_big_rack",
            description: "+5 balls (15 total - a real rack)."),
        new UpgradeNodeDef("ball_crowded_table", "Crowded Table", UpgradeBranch.Ball, UpgradeNodeType.Conditional, "ball_big_rack",
            description: "+25% V to every ball while 8+ balls are on the table."),
        new UpgradeNodeDef("ball_extra_cue_ball", "Extra Cue Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_half_rack",
            description: "A second cue ball you can shoot while balls are moving. Each shot costs 1 towards the shot cap."),
        new UpgradeNodeDef("ball_tandem", "Tandem", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_extra_cue_ball",
            description: "If both cue balls end up in the same chain, every ball in it gains double M."),

        // ---- Flair (23): chains, pocket bonuses, shots per night, round clears ----
        new UpgradeNodeDef("flair_crowd_pleaser_1", "Crowd Pleaser I", UpgradeBranch.Flair, UpgradeNodeType.Stat, CentreId,
            description: "Later balls in a chain gain more M."),
        new UpgradeNodeDef("flair_endurance_1", "Endurance I", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_1",
            description: "+1 shot per night."),
        new UpgradeNodeDef("flair_endurance_2", "Endurance II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_endurance_1",
            description: "+1 shot per night."),
        new UpgradeNodeDef("flair_clean_sweep_1", "Clean Sweep I", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_endurance_2",
            description: "Clearing a round pays a lump sum."),
        new UpgradeNodeDef("flair_clean_sweep_2", "Clean Sweep II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_clean_sweep_1",
            description: "Bigger round-clear bonus."),
        new UpgradeNodeDef("flair_run_the_table", "Run the Table", UpgradeBranch.Flair, UpgradeNodeType.Milestone, "flair_clean_sweep_2", "flair_head_start_2",
            description: "Milestone. Every round you clear in a night makes the next round's balls start with more M."),
        new UpgradeNodeDef("flair_second_wind", "Second Wind", UpgradeBranch.Flair, UpgradeNodeType.Exclusive, "flair_run_the_table", null, "flair_closer",
            description: "Exclusive with Closer. Every chain of 5+ refunds 1 shot."),
        new UpgradeNodeDef("flair_closer", "Closer", UpgradeBranch.Flair, UpgradeNodeType.Exclusive, "flair_run_the_table", null, "flair_second_wind",
            description: "Exclusive with Second Wind. The last shot of the night pays double."),
        new UpgradeNodeDef("flair_endurance_3", "Endurance III", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_endurance_2",
            description: "+2 shots per night."),
        new UpgradeNodeDef("flair_endurance_4", "Endurance IV", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_endurance_3",
            description: "+2 shots per night (+6 total)."),
        new UpgradeNodeDef("flair_leftovers", "Leftovers", UpgradeBranch.Flair, UpgradeNodeType.Twist, "flair_endurance_3",
            description: "When the night ends, balls still on the table pay 25% of V x M."),
        new UpgradeNodeDef("flair_nothing_to_lose", "Nothing to Lose", UpgradeBranch.Flair, UpgradeNodeType.Conditional, "flair_leftovers",
            description: "The last 3 shots of the night get +50% C."),
        new UpgradeNodeDef("flair_clean_pocket_1", "Clean Pocket I", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_1",
            description: "+C (chain pocket bonus)."),
        new UpgradeNodeDef("flair_clean_pocket_2", "Clean Pocket II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_clean_pocket_1",
            description: "+C."),
        new UpgradeNodeDef("flair_cash_out", "Cash Out", UpgradeBranch.Flair, UpgradeNodeType.Twist, "flair_clean_pocket_2",
            description: "Pocketing 3+ balls in one shot makes each pay x1.5."),
        new UpgradeNodeDef("flair_hot_streak", "Hot Streak", UpgradeBranch.Flair, UpgradeNodeType.Conditional, "flair_cash_out",
            description: "Every shot in a row that pockets a ball gives +10% C, stacking. Resets on a shot that pockets nothing."),
        new UpgradeNodeDef("flair_clean_pocket_3", "Clean Pocket III", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_clean_pocket_2",
            description: "+C."),
        new UpgradeNodeDef("flair_crowd_pleaser_2", "Crowd Pleaser II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_1",
            description: "Later balls in a chain gain more M."),
        new UpgradeNodeDef("flair_crowd_pleaser_3", "Crowd Pleaser III", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_2",
            description: "Later balls in a chain gain more M."),
        new UpgradeNodeDef("flair_long_chain", "Long Chain", UpgradeBranch.Flair, UpgradeNodeType.Twist, "flair_crowd_pleaser_3",
            description: "Chains that hit a big share of the balls on the table give every ball in the chain bonus M."),
        new UpgradeNodeDef("flair_crowd_pleaser_4", "Crowd Pleaser IV", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_3",
            description: "Later balls in a chain gain more M."),
        new UpgradeNodeDef("flair_head_start_1", "Head Start I", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_2",
            description: "Every ball starts each round with some M."),
        new UpgradeNodeDef("flair_head_start_2", "Head Start II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_head_start_1",
            description: "More starting M."),

        // ---- Cue (18): power, aiming, shot control ----
        new UpgradeNodeDef("cue_chalk_up", "Chalk Up", UpgradeBranch.Cue, UpgradeNodeType.Stat, CentreId,
            description: "Power I - +max shot power."),
        new UpgradeNodeDef("cue_aim_guide_1", "Aim Guide I", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_chalk_up",
            description: "Line up to the first ball the cue would hit."),
        new UpgradeNodeDef("cue_aim_guide_2", "Aim Guide II", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_aim_guide_1",
            description: "Also shows where that ball goes after contact."),
        new UpgradeNodeDef("cue_aim_guide_3", "Aim Guide III", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_aim_guide_2",
            description: "Shows one more rail bounce."),
        new UpgradeNodeDef("cue_aim_guide_4", "Aim Guide IV", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_aim_guide_3",
            description: "Shows one more rail bounce."),
        new UpgradeNodeDef("cue_seeing_the_table", "Seeing the Table", UpgradeBranch.Cue, UpgradeNodeType.Milestone, "cue_aim_guide_4", "cue_clean_contact",
            description: "Milestone. The aim guide shows the whole predicted chain."),
        new UpgradeNodeDef("cue_power_game", "Power Game", UpgradeBranch.Cue, UpgradeNodeType.Exclusive, "cue_seeing_the_table", null, "cue_touch_game",
            description: "Exclusive with Touch Game. Big max power boost - balls hit at high speed gain extra M."),
        new UpgradeNodeDef("cue_touch_game", "Touch Game", UpgradeBranch.Cue, UpgradeNodeType.Exclusive, "cue_seeing_the_table", null, "cue_power_game",
            description: "Exclusive with Power Game. Balls hit at low speed gain extra M - rewards precise setup shots."),
        new UpgradeNodeDef("cue_clean_contact", "Clean Contact", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_aim_guide_2",
            description: "The first ball the cue hits each shot gains extra M."),
        new UpgradeNodeDef("cue_power_2", "Power II", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_chalk_up",
            description: "+max shot power."),
        new UpgradeNodeDef("cue_power_3", "Power III", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_power_2",
            description: "+max shot power."),
        new UpgradeNodeDef("cue_power_4", "Power IV", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_power_3",
            description: "+max shot power."),
        new UpgradeNodeDef("cue_power_5", "Power V", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_power_4",
            description: "+max shot power."),
        new UpgradeNodeDef("cue_break_shot", "Break Shot", UpgradeBranch.Cue, UpgradeNodeType.Conditional, "cue_power_3",
            description: "The first shot of each round gets +50% power."),
        new UpgradeNodeDef("cue_steady_hand_1", "Steady Hand I", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_chalk_up",
            description: "A finer power meter / slower aiming for precision."),
        new UpgradeNodeDef("cue_steady_hand_2", "Steady Hand II", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_steady_hand_1",
            description: "More precision."),
        new UpgradeNodeDef("cue_stop_shot", "Stop Shot", UpgradeBranch.Cue, UpgradeNodeType.Twist, "cue_steady_hand_2",
            description: "The cue ball slows down fast after its first hit - easier to control where it ends up."),
        new UpgradeNodeDef("cue_scratch_insurance", "Scratch Insurance", UpgradeBranch.Cue, UpgradeNodeType.Twist, "cue_steady_hand_2",
            description: "Scratching doesn't cost anything extra."),

        // ---- Table (17): physics constants, then rail/pocket detection ----
        new UpgradeNodeDef("table_smooth_felt_1", "Smooth Felt I", UpgradeBranch.Table, UpgradeNodeType.Stat, CentreId,
            description: "Less friction - balls roll longer."),
        new UpgradeNodeDef("table_lively_rails_1", "Lively Rails I", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_1",
            description: "Bouncier rails - balls keep more speed."),
        new UpgradeNodeDef("table_lively_rails_2", "Lively Rails II", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_lively_rails_1",
            description: "Bouncier rails."),
        new UpgradeNodeDef("table_lively_rails_3", "Lively Rails III", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_lively_rails_2",
            description: "Bouncier rails."),
        new UpgradeNodeDef("table_know_the_rails", "Know the Rails", UpgradeBranch.Table, UpgradeNodeType.Milestone, "table_lively_rails_3", "table_cushion_cash",
            description: "Milestone. Every rail bounce during a chain counts as a chain link."),
        new UpgradeNodeDef("table_call_your_pocket", "Call Your Pocket", UpgradeBranch.Table, UpgradeNodeType.Exclusive, "table_know_the_rails", null, "table_wide_open",
            description: "Exclusive with Wide Open. Pick a pocket each round - balls sunk there get x2 value."),
        new UpgradeNodeDef("table_wide_open", "Wide Open", UpgradeBranch.Table, UpgradeNodeType.Exclusive, "table_know_the_rails", null, "table_call_your_pocket",
            description: "Exclusive with Call Your Pocket. Every pocket pays a little extra."),
        new UpgradeNodeDef("table_bank_shot", "Bank Shot", UpgradeBranch.Table, UpgradeNodeType.Conditional, "table_lively_rails_2",
            description: "Balls that hit a rail before being pocketed pay +50% C."),
        new UpgradeNodeDef("table_cushion_cash", "Cushion Cash", UpgradeBranch.Table, UpgradeNodeType.Twist, "table_bank_shot",
            description: "Balls gain a little M when they hit a rail."),
        new UpgradeNodeDef("table_wide_pockets_1", "Wide Pockets I", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_1",
            description: "Bigger pocket radius."),
        new UpgradeNodeDef("table_wide_pockets_2", "Wide Pockets II", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_wide_pockets_1",
            description: "Bigger pockets."),
        new UpgradeNodeDef("table_wide_pockets_3", "Wide Pockets III", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_wide_pockets_2",
            description: "Bigger pockets."),
        new UpgradeNodeDef("table_tight_table", "Tight Table", UpgradeBranch.Table, UpgradeNodeType.Conditional, "table_wide_pockets_3",
            description: "When 3 or fewer balls are left on the table, pockets get wider."),
        new UpgradeNodeDef("table_side_action", "Side Action", UpgradeBranch.Table, UpgradeNodeType.Conditional, "table_wide_pockets_2",
            description: "Side pockets pay +50% - they're harder to hit."),
        new UpgradeNodeDef("table_smooth_felt_2", "Smooth Felt II", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_1",
            description: "Less friction."),
        new UpgradeNodeDef("table_smooth_felt_3", "Smooth Felt III", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_2",
            description: "Less friction."),
        new UpgradeNodeDef("table_smooth_felt_4", "Smooth Felt IV", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_3",
            description: "Less friction."),

        new UpgradeNodeDef(CapstoneId, "Back on Top", UpgradeBranch.None, UpgradeNodeType.Capstone,
            description: "Needs every milestone. Triggers the ending (you're the pro again), then endless play."),
    };

    static Dictionary<string, UpgradeNodeDef> byId;

    public static UpgradeNodeDef Get(string id)
    {
        if (byId == null)
        {
            byId = new Dictionary<string, UpgradeNodeDef>();
            foreach (UpgradeNodeDef def in All) byId[def.id] = def;
        }
        return byId.TryGetValue(id, out UpgradeNodeDef found) ? found : null;
    }
}
