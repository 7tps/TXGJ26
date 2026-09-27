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
// Nodes with a binding in Upgrades.BuildNodeBindings buy through the matching Upgrades.Buy method: that
// method owns the real cost, prerequisites and effect. Nodes without one (18 of them, plus the centre and capstone - the doc assumes systems
// like rail hits, pockets and shot caps that do not exist in the code yet) just become owned at the
// placeholder price below, with no gameplay effect.
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
            description: "Rack size 2: one more ball on the table each night (you start with 1)."),
        new UpgradeNodeDef("ball_warm_up", "Warm Up", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_second",
            description: "Ball Value I - every ball is worth +$10."),
        new UpgradeNodeDef("ball_lucky_break", "Lucky Break", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_warm_up",
            description: "Each ball has a 5% chance to spawn as a special ball when racked. Unlocks Gold: worth x3."),
        new UpgradeNodeDef("ball_loaded_dice_1", "Loaded Dice I", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_lucky_break",
            description: "+5% chance for a ball to spawn as a special ball."),
        new UpgradeNodeDef("ball_vault_ball", "Vault Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_loaded_dice_1",
            description: "Unlocks Vault: pays nothing unless it is pocketed in a chain of 3+ balls."),
        new UpgradeNodeDef("ball_glass_ball", "Glass Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_vault_ball",
            description: "Unlocks Glass: shatters after 3 hits and pays out on the spot."),
        new UpgradeNodeDef("ball_high_rollers_1", "High Rollers I", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_glass_ball", "ball_money_ball",
            description: "Special balls pay x1.5. (Not in the game yet.)"),
        new UpgradeNodeDef("ball_collector", "Collector", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_high_rollers_1",
            description: "Pocketing 2 special balls of the same type in one turn pays a $50 bonus."),
        new UpgradeNodeDef("ball_high_rollers_2", "High Rollers II", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_collector",
            description: "Special balls pay x1.5 more. (Not in the game yet.)"),
        new UpgradeNodeDef("ball_loaded_dice_2", "Loaded Dice II", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_loaded_dice_1",
            description: "+5% chance for a ball to spawn as a special ball."),
        new UpgradeNodeDef("ball_loaded_dice_3", "Loaded Dice III", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_loaded_dice_2",
            description: "+5% chance for a ball to spawn as a special ball."),
        new UpgradeNodeDef("ball_reading_the_rack", "Reading the Rack", UpgradeBranch.Ball, UpgradeNodeType.Milestone, "ball_tandem", "ball_collector",
            description: "Milestone. Every rack has at least one special ball (from the types you have unlocked). Needs both Tandem and Collector."),
        new UpgradeNodeDef("ball_hot_hand", "Hot Hand", UpgradeBranch.Ball, UpgradeNodeType.Exclusive, "ball_reading_the_rack", null, "ball_slow_burn",
            description: "Exclusive with Slow Burn. Balls hit on the first shot of every turn gain triple M."),
        new UpgradeNodeDef("ball_slow_burn", "Slow Burn", UpgradeBranch.Ball, UpgradeNodeType.Exclusive, "ball_reading_the_rack", null, "ball_hot_hand",
            description: "Exclusive with Hot Hand. Every ball still on the table gains +0.02 M at the start of each turn."),
        new UpgradeNodeDef("ball_hot_ball", "Hot Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_lucky_break",
            description: "Unlocks Hot: gains double M every time it is hit."),
        new UpgradeNodeDef("ball_wild_ball", "Wild Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_hot_ball",
            description: "Unlocks Wild: its payout is multiplied by a random x0 to x5."),
        new UpgradeNodeDef("ball_money_ball", "Money Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_wild_ball",
            description: "Unlocks Money: pays nothing until every other ball is pocketed, but its M still stacks."),
        new UpgradeNodeDef("ball_value_2", "Ball Value II", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_warm_up", "ball_third_ball",
            description: "Ball Value II - every ball is worth another +$10."),
        new UpgradeNodeDef("ball_value_3", "Ball Value III", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_value_2",
            description: "Ball Value III - every ball is worth another +$10."),
        new UpgradeNodeDef("ball_last_ball_standing", "Last Ball Standing", UpgradeBranch.Ball, UpgradeNodeType.Conditional, "ball_value_3",
            description: "The last ball left on the table each round gets +100% V. (Not in the game yet.)"),
        new UpgradeNodeDef("ball_value_4", "Ball Value IV", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_value_3",
            description: "Ball Value IV - every ball is worth another +$10."),
        new UpgradeNodeDef("ball_third_ball", "Third Ball", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_second",
            description: "Rack size 3: one more ball on the table."),
        new UpgradeNodeDef("ball_small_rack", "Small Rack", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_third_ball",
            description: "Rack size 5: two more balls on the table."),
        new UpgradeNodeDef("ball_half_rack", "Half Rack", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_small_rack",
            description: "Rack size 7: two more balls on the table."),
        new UpgradeNodeDef("ball_big_rack", "Big Rack", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_half_rack",
            description: "Rack size 10: three more balls on the table."),
        new UpgradeNodeDef("ball_full_rack", "Full Rack", UpgradeBranch.Ball, UpgradeNodeType.Stat, "ball_big_rack",
            description: "Rack size 15: five more balls, a full rack."),
        new UpgradeNodeDef("ball_crowded_table", "Crowded Table", UpgradeBranch.Ball, UpgradeNodeType.Conditional, "ball_big_rack",
            description: "+25% V to every ball while 8+ balls are on the table. (Not in the game yet.)"),
        new UpgradeNodeDef("ball_extra_cue_ball", "Extra Cue Ball", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_half_rack",
            description: "Adds a second cue ball and cue stick you can shoot each turn."),
        new UpgradeNodeDef("ball_tandem", "Tandem", UpgradeBranch.Ball, UpgradeNodeType.Twist, "ball_extra_cue_ball",
            description: "Once both cue balls have hit something in a turn, every ball collision gives double M. Needs Extra Cue Ball."),

        // ---- Flair (23): chains, pocket bonuses, shots per night, round clears ----
        new UpgradeNodeDef("flair_crowd_pleaser_1", "Crowd Pleaser I", UpgradeBranch.Flair, UpgradeNodeType.Stat, CentreId,
            description: "Later balls in a chain gain more M: +25% M gain per chain position."),
        new UpgradeNodeDef("flair_endurance_1", "Endurance I", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_1",
            description: "The night lasts 15 seconds longer (next night onwards)."),
        new UpgradeNodeDef("flair_endurance_2", "Endurance II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_endurance_1",
            description: "The night lasts 30 seconds longer in total."),
        new UpgradeNodeDef("flair_clean_sweep_1", "Clean Sweep I", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_endurance_2",
            description: "Clearing the rack pays a $100 bonus."),
        new UpgradeNodeDef("flair_clean_sweep_2", "Clean Sweep II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_clean_sweep_1",
            description: "Clearing the rack pays a $250 bonus instead."),
        new UpgradeNodeDef("flair_run_the_table", "Run the Table", UpgradeBranch.Flair, UpgradeNodeType.Milestone, "flair_clean_sweep_2", "flair_head_start_2",
            description: "Milestone. Every rack you clear in a night gives the next racks' balls +0.25 starting M."),
        new UpgradeNodeDef("flair_second_wind", "Second Wind", UpgradeBranch.Flair, UpgradeNodeType.Exclusive, "flair_run_the_table", null, "flair_closer",
            description: "Exclusive with Closer. A chain of 5+ balls adds 5 seconds to the night."),
        new UpgradeNodeDef("flair_closer", "Closer", UpgradeBranch.Flair, UpgradeNodeType.Exclusive, "flair_run_the_table", null, "flair_second_wind",
            description: "Exclusive with Second Wind. Everything paid out on the shot still rolling when time runs out is doubled."),
        new UpgradeNodeDef("flair_endurance_3", "Endurance III", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_endurance_2",
            description: "The night lasts 45 seconds longer in total."),
        new UpgradeNodeDef("flair_endurance_4", "Endurance IV", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_endurance_3",
            description: "The night lasts 60 seconds longer in total."),
        new UpgradeNodeDef("flair_leftovers", "Leftovers", UpgradeBranch.Flair, UpgradeNodeType.Twist, "flair_endurance_3",
            description: "When the night ends with balls still on the table, each pays 25% of its payout."),
        new UpgradeNodeDef("flair_nothing_to_lose", "Nothing to Lose", UpgradeBranch.Flair, UpgradeNodeType.Conditional, "flair_leftovers",
            description: "In the last 15 seconds of the night, chain pocket bonuses are x1.5."),
        new UpgradeNodeDef("flair_clean_pocket_1", "Clean Pocket I", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_1",
            description: "Balls pocketed as part of a chain (2+ balls hit that turn) pay x1.25."),
        new UpgradeNodeDef("flair_clean_pocket_2", "Clean Pocket II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_clean_pocket_1",
            description: "Chain pockets pay x1.5."),
        new UpgradeNodeDef("flair_cash_out", "Cash Out", UpgradeBranch.Flair, UpgradeNodeType.Twist, "flair_clean_pocket_2",
            description: "Pocketing 3+ balls in one turn pays a x1.5 bonus on that turn's total."),
        new UpgradeNodeDef("flair_hot_streak", "Hot Streak", UpgradeBranch.Flair, UpgradeNodeType.Conditional, "flair_cash_out",
            description: "Each turn in a row that pockets a ball adds +10% to chain pocket bonuses, stacking. Resets on a turn that pockets nothing."),
        new UpgradeNodeDef("flair_clean_pocket_3", "Clean Pocket III", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_clean_pocket_2",
            description: "Chain pockets pay x1.75."),
        new UpgradeNodeDef("flair_crowd_pleaser_2", "Crowd Pleaser II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_1",
            description: "+25% more M gain per chain position (+50% total)."),
        new UpgradeNodeDef("flair_crowd_pleaser_3", "Crowd Pleaser III", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_2",
            description: "+25% more M gain per chain position (+75% total)."),
        new UpgradeNodeDef("flair_long_chain", "Long Chain", UpgradeBranch.Flair, UpgradeNodeType.Twist, "flair_crowd_pleaser_3",
            description: "When a chain reaches 40/60/80/100% of the balls on the table (at least 3), every ball in it gains +0.5 M per tier. Needs a rack of 5+."),
        new UpgradeNodeDef("flair_crowd_pleaser_4", "Crowd Pleaser IV", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_3",
            description: "+25% more M gain per chain position (+100% total)."),
        new UpgradeNodeDef("flair_head_start_1", "Head Start I", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_crowd_pleaser_2",
            description: "Every ball starts each round with +0.25 M."),
        new UpgradeNodeDef("flair_head_start_2", "Head Start II", UpgradeBranch.Flair, UpgradeNodeType.Stat, "flair_head_start_1",
            description: "Every ball starts each round with +0.5 M in total."),

        // ---- Cue (18): power, aiming, shot control ----
        new UpgradeNodeDef("cue_chalk_up", "Chalk Up", UpgradeBranch.Cue, UpgradeNodeType.Stat, CentreId,
            description: "Power I - the starting power (free): max pull 4, max shot speed 25."),
        new UpgradeNodeDef("cue_aim_guide_1", "Aim Guide I", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_chalk_up",
            description: "Aim guide: a line to the first ball the cue would hit."),
        new UpgradeNodeDef("cue_aim_guide_2", "Aim Guide II", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_aim_guide_1",
            description: "The guide also follows one rail bounce."),
        new UpgradeNodeDef("cue_aim_guide_3", "Aim Guide III", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_aim_guide_2",
            description: "The guide follows two rail bounces."),
        new UpgradeNodeDef("cue_aim_guide_4", "Aim Guide IV", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_aim_guide_3",
            description: "The guide follows three rail bounces."),
        new UpgradeNodeDef("cue_seeing_the_table", "Seeing the Table", UpgradeBranch.Cue, UpgradeNodeType.Milestone, "cue_aim_guide_4", "cue_clean_contact",
            description: "Milestone. The guide follows four rail bounces."),
        new UpgradeNodeDef("cue_power_game", "Power Game", UpgradeBranch.Cue, UpgradeNodeType.Exclusive, "cue_seeing_the_table", null, "cue_touch_game",
            description: "Exclusive with Touch Game. Big max power boost - balls hit at high speed gain extra M. (Not in the game yet.)"),
        new UpgradeNodeDef("cue_touch_game", "Touch Game", UpgradeBranch.Cue, UpgradeNodeType.Exclusive, "cue_seeing_the_table", null, "cue_power_game",
            description: "Exclusive with Power Game. Balls hit at low speed gain extra M - rewards precise setup shots. (Not in the game yet.)"),
        new UpgradeNodeDef("cue_clean_contact", "Clean Contact", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_aim_guide_2",
            description: "The first ball the cue hits each shot gains extra M. (Not in the game yet.)"),
        new UpgradeNodeDef("cue_power_2", "Power II", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_chalk_up",
            description: "Power II - max pull 5, max shot speed 30."),
        new UpgradeNodeDef("cue_power_3", "Power III", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_power_2",
            description: "Power III - max pull 6, max shot speed 35."),
        new UpgradeNodeDef("cue_power_4", "Power IV", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_power_3", null, "cue_break_shot",
            description: "Exclusive with Break Shot. Power IV - max pull 7, max shot speed 40."),
        new UpgradeNodeDef("cue_power_5", "Power V", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_power_4", "cue_break_shot",
            description: "Power V - max pull 8, max shot speed 45. After Break Shot it only reaches 7 / 40, since Power IV was skipped."),
        new UpgradeNodeDef("cue_break_shot", "Break Shot", UpgradeBranch.Cue, UpgradeNodeType.Conditional, "cue_power_3", null, "cue_power_4",
            description: "Exclusive with Power IV. The first shot of each round gets +50% power. Not in the game yet: buying it only skips Power IV."),
        new UpgradeNodeDef("cue_steady_hand_1", "Steady Hand I", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_power_2",
            description: "Shows a power meter while you pull back, and finer control at low and mid power."),
        new UpgradeNodeDef("cue_steady_hand_2", "Steady Hand II", UpgradeBranch.Cue, UpgradeNodeType.Stat, "cue_steady_hand_1",
            description: "Even finer control at low and mid power."),
        new UpgradeNodeDef("cue_stop_shot", "Stop Shot", UpgradeBranch.Cue, UpgradeNodeType.Twist, "cue_steady_hand_2",
            description: "The cue ball slows down fast after its first hit - easier to control where it ends up. (Not in the game yet.)"),
        new UpgradeNodeDef("cue_scratch_insurance", "Scratch Insurance", UpgradeBranch.Cue, UpgradeNodeType.Twist, "cue_steady_hand_2",
            description: "Scratching doesn't cost anything extra. (Not in the game yet.)"),

        // ---- Table (17): physics constants, then rail/pocket detection ----
        new UpgradeNodeDef("table_smooth_felt_1", "Smooth Felt I", UpgradeBranch.Table, UpgradeNodeType.Stat, CentreId,
            description: "Base felt (free): rolling friction 0.8."),
        new UpgradeNodeDef("table_lively_rails_1", "Lively Rails I", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_1",
            description: "Bouncier rails: bounciness 0.95."),
        new UpgradeNodeDef("table_lively_rails_2", "Lively Rails II", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_lively_rails_1",
            description: "Bouncier rails: bounciness 1.0 (the maximum)."),
        new UpgradeNodeDef("table_lively_rails_3", "Lively Rails III", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_lively_rails_2",
            description: "Rails are already at maximum bounce, so this has no extra effect yet."),
        new UpgradeNodeDef("table_know_the_rails", "Know the Rails", UpgradeBranch.Table, UpgradeNodeType.Milestone, "table_lively_rails_3", "table_cushion_cash",
            description: "Milestone. The last Lively Rails level (rails are already at maximum bounce, so no extra effect yet)."),
        new UpgradeNodeDef("table_call_your_pocket", "Call Your Pocket", UpgradeBranch.Table, UpgradeNodeType.Exclusive, "table_know_the_rails", null, "table_wide_open",
            description: "Exclusive with Wide Open. Pick a pocket each round - balls sunk there get x2 value. (Not in the game yet.)"),
        new UpgradeNodeDef("table_wide_open", "Wide Open", UpgradeBranch.Table, UpgradeNodeType.Exclusive, "table_know_the_rails", null, "table_call_your_pocket",
            description: "Exclusive with Call Your Pocket. Every pocket pays a little extra. (Not in the game yet.)"),
        new UpgradeNodeDef("table_bank_shot", "Bank Shot", UpgradeBranch.Table, UpgradeNodeType.Conditional, "table_lively_rails_2",
            description: "Balls that hit a rail before being pocketed pay +50% C. (Not in the game yet.)"),
        new UpgradeNodeDef("table_cushion_cash", "Cushion Cash", UpgradeBranch.Table, UpgradeNodeType.Twist, "table_bank_shot",
            description: "Balls gain a little M when they hit a rail. (Not in the game yet.)"),
        new UpgradeNodeDef("table_wide_pockets_1", "Wide Pockets I", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_1",
            description: "Bigger pocket radius. (Not in the game yet.)"),
        new UpgradeNodeDef("table_wide_pockets_2", "Wide Pockets II", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_wide_pockets_1",
            description: "Bigger pockets. (Not in the game yet.)"),
        new UpgradeNodeDef("table_wide_pockets_3", "Wide Pockets III", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_wide_pockets_2",
            description: "Bigger pockets. (Not in the game yet.)"),
        new UpgradeNodeDef("table_tight_table", "Tight Table", UpgradeBranch.Table, UpgradeNodeType.Conditional, "table_wide_pockets_3",
            description: "When 3 or fewer balls are left on the table, pockets get wider. (Not in the game yet.)"),
        new UpgradeNodeDef("table_side_action", "Side Action", UpgradeBranch.Table, UpgradeNodeType.Conditional, "table_wide_pockets_2",
            description: "Side pockets pay +50% - they're harder to hit. (Not in the game yet.)"),
        new UpgradeNodeDef("table_smooth_felt_2", "Smooth Felt II", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_1",
            description: "Less friction: 0.7."),
        new UpgradeNodeDef("table_smooth_felt_3", "Smooth Felt III", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_2",
            description: "Less friction: 0.6."),
        new UpgradeNodeDef("table_smooth_felt_4", "Smooth Felt IV", UpgradeBranch.Table, UpgradeNodeType.Stat, "table_smooth_felt_3",
            description: "Less friction: 0.5."),

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

    // Placeholder balance curve, only for nodes with no Upgrades binding (bound nodes use their Buy method's
    // cost). Not from the design doc. Nothing costed money at all until this was
    // added, so this just gives every node *some* price (a base amount for its type, plus a bit more
    // per step of chain depth) rather than leaving the tree free. Retune these two constants once the
    // economy (ball payouts, run length) actually gets a balance pass.
    static readonly Dictionary<UpgradeNodeType, int> BaseCostByType = new Dictionary<UpgradeNodeType, int>
    {
        { UpgradeNodeType.Root, 0 },
        { UpgradeNodeType.Stat, 40 },
        { UpgradeNodeType.Conditional, 90 },
        { UpgradeNodeType.Twist, 130 },
        { UpgradeNodeType.Milestone, 350 },
        { UpgradeNodeType.Exclusive, 280 },
        { UpgradeNodeType.Capstone, 800 },
    };
    const int CostPerDepth = 30;

    static Dictionary<string, int> depthCache;

    public static int CostOf(string id)
    {
        UpgradeNodeDef def = Get(id);
        return def == null ? 0 : BaseCostByType[def.type] + Depth(id) * CostPerDepth;
    }

    // How many links from the centre/a branch root this node sits - 0 for the centre itself and for
    // the capstone (it's gated by the four milestones, not a normal parent, so depth doesn't apply).
    static int Depth(string id)
    {
        if (depthCache == null) depthCache = new Dictionary<string, int>();
        if (depthCache.TryGetValue(id, out int cached)) return cached;

        UpgradeNodeDef def = Get(id);
        int depth = def?.parent == null ? 0 : Depth(def.parent) + 1;
        depthCache[id] = depth;
        return depth;
    }
}
