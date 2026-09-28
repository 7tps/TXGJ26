using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Lobby's TABLE FEES board. Shows the next bill, pays it when it's due, keeps Play Game locked
// until it's paid, and shows the eviction screen when the wallet can't cover a bill that's due.
// Rules and state live on Rent; this is only the Lobby's view of it.
public class TableFees : MonoBehaviour
{
    [SerializeField] Button payButton; // the Table Fees board itself
    [SerializeField] TMP_Text label;
    [SerializeField] Button playGameButton;
    [SerializeField] GameObject evictedPanel;
    [SerializeField] TMP_Text evictedText;
    [SerializeField] string titleScene = "Main Menu";

    void OnEnable()
    {
        UpgradeProgress.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => UpgradeProgress.Changed -= Refresh;

    // Wired to the Table Fees button
    public void Pay()
    {
        if (Rent.Instance != null && Rent.Instance.TryPay()) Refresh();
    }

    public void Refresh()
    {
        Rent rent = Rent.Instance;
        if (rent == null) return;

        bool due = rent.IsDue;
        int amount = rent.AmountDue;
        int money = UpgradeProgress.Instance != null ? UpgradeProgress.Instance.Money : 0;

        if (label != null)
        {
            int nights = rent.NightsUntilDue;
            label.text = due
                ? $"TABLE FEES\n${amount:N0} DUE NOW - CLICK TO PAY"
                : $"TABLE FEES\n${amount:N0} due after {nights} more night{(nights == 1 ? "" : "s")}";
        }

        if (payButton != null) payButton.interactable = due && money >= amount;
        if (playGameButton != null) playGameButton.interactable = !due;

        bool evicted = due && money < amount;
        if (evictedPanel != null) evictedPanel.SetActive(evicted);
        if (evicted && evictedText != null)
            evictedText.text = $"You owe ${amount:N0} in table fees and only have ${money:N0}.\nThe bar throws you out. Your run is over.";
    }

    // Wired to the eviction screen's button
    public void StartOver()
    {
        Rent.StartNewRun();
        SceneManager.LoadScene(titleScene);
    }
}
