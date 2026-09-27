using TMPro;
using UnityEngine;

// Shows the shared upgrade currency (UpgradeProgress.Money) on a TMP_Text - used in both the Lobby
// (see EnsureUpgradeProgressInLobby) and the Upgrade Tree scene (see BuildUpgradeTreeScene), so the
// Main scene's payouts show up in both places the moment they're carried over. Refreshes off
// UpgradeProgress.Changed instead of polling every frame, and again on OnEnable so it's already
// correct the instant a scene loads - no need to wait for the next change.
public class UpgradeMoneyDisplay : MonoBehaviour
{
    [SerializeField] TMP_Text text;

    void OnEnable()
    {
        UpgradeProgress.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => UpgradeProgress.Changed -= Refresh;

    void Refresh()
    {
        if (text == null || UpgradeProgress.Instance == null) return;
        text.text = $"${UpgradeProgress.Instance.Money:N0}";
    }
}
