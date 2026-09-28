using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Table Fees scene: after every night the bar bills you for the table before you can play again.
// Paying (Upgrades.PayTableFee) lifts the Main / Upgrade Tree lock and sends you back to the Lobby, where
// the next night starts - and the next bill is bigger. Walking away (the only option left when you can't
// afford it) loses the run: every upgrade and the wallet are wiped and you're sent back to the Main Menu.
//
// Only reachable while the fees are due (see NightClock.IsLocked). The scene itself is built by
// Tools > Build Table Fees Scene (see BuildTableFeesScene), which wires up the references below.
public class TableFees : MonoBehaviour
{
    const string MainMenuScene = "Main Menu";

    [SerializeField] TMP_Text billText;
    [SerializeField] Button payButton;
    [SerializeField] TMP_Text payButtonText;
    [SerializeField] Button walkAwayButton;
    [SerializeField] float loseDelay = 2f; // how long the "thrown out" message shows before the Main Menu

    bool leaving; // stops a money change redrawing the bill over the "thrown out" message

    // Refreshes off UpgradeProgress.Changed like UpgradeMoneyDisplay, since the balance decides whether Pay is allowed
    void OnEnable()
    {
        UpgradeProgress.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => UpgradeProgress.Changed -= Refresh;

    void Refresh()
    {
        Upgrades up = Upgrades.Instance;
        if (leaving || up == null) return;

        int fee = up.CurrentTableFee;
        bool canPay = up.CanAffordTableFee;

        billText.text = $"Night {up.NightsPaid + 1} is over.\nThe bar wants ${fee:N0} for the table." +
            (canPay ? "" : "\nYou can't cover it.");
        payButtonText.text = $"Pay ${fee:N0}";
        payButton.interactable = canPay;
    }

    public void Pay()
    {
        if (leaving || Upgrades.Instance == null || !Upgrades.Instance.PayTableFee()) return;

        leaving = true;
        NightClock.TryLoadScene(NightClock.LobbyScene);
    }

    public void WalkAway()
    {
        if (leaving) return;

        leaving = true;
        payButton.interactable = false;
        walkAwayButton.interactable = false;
        billText.text = "You couldn't pay up.\nThe bar throws you out.";
        StartCoroutine(LoseAfterDelay());
    }

    // Wipes the whole run, then back to the Main Menu to start over
    IEnumerator LoseAfterDelay()
    {
        yield return new WaitForSeconds(loseDelay);
        Upgrades.ResetRun();
        UpgradeProgress.ResetRun();
        SceneManager.LoadScene(MainMenuScene);
    }
}
