using UnityEngine;

// Real rent ("table fees"): a bill comes due every few nights and is paid from the shared wallet
// (UpgradeProgress) in the Lobby. Created automatically before the first scene loads, like Upgrades and
// UpgradeProgress, and carried across scene loads for the whole run. GameEngine counts nights here;
// TableFees (in the Lobby) shows the bill, takes the payment, blocks play while it's due, and evicts.
public class Rent : MonoBehaviour
{
    public static Rent Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance()
    {
        Instance = null;
        stashed = null;
    }

    // Tutorial sandbox (see TutorialSession): the real rent schedule is set aside for a fresh one
    static Rent stashed;

    public static void EnterSandbox()
    {
        if (stashed != null) return;
        stashed = Instance;
        if (stashed != null) stashed.gameObject.SetActive(false);
        Instance = null;
        new GameObject("Rent (Tutorial)").AddComponent<Rent>();
    }

    public static void ExitSandbox()
    {
        if (Instance != null && Instance != stashed) Destroy(Instance.gameObject);
        Instance = stashed;
        if (Instance != null) Instance.gameObject.SetActive(true);
        stashed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfMissing()
    {
        if (Instance != null) return;
        new GameObject("Rent").AddComponent<Rent>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [Header("Rules")]
    [SerializeField] int nightsBetweenBills = 3;
    [SerializeField] int firstBill = 200;
    [SerializeField] int increasePerBill = 100; // each bill costs this much more than the last

    [Header("Run state")]
    [SerializeField] int nightsPlayed;
    [SerializeField] int billsPaid;

    public int NightsBetweenBills => Mathf.Max(1, nightsBetweenBills);
    public int FirstBill => firstBill;
    public int NightsPlayed => nightsPlayed;
    public int AmountDue => firstBill + increasePerBill * billsPaid;

    // Bill n (counting from 1) is due once n x NightsBetweenBills nights have been played
    int DueAfterNight => (billsPaid + 1) * NightsBetweenBills;
    public bool IsDue => nightsPlayed >= DueAfterNight;
    public int NightsUntilDue => Mathf.Max(0, DueAfterNight - nightsPlayed);
    public bool CanAffordBill => UpgradeProgress.Instance != null && UpgradeProgress.Instance.Money >= AmountDue;

    public int BillsPaid => billsPaid;

    // Called by GameEngine each time a night starts (in the tutorial, on the tutorial's own Rent)
    public void RecordNightStarted() => nightsPlayed++;

    // Tutorial only: brings the next bill due right away so it can be practised
    public void MakeDueNow() => nightsPlayed = Mathf.Max(nightsPlayed, DueAfterNight);

    public bool TryPay()
    {
        if (!IsDue || UpgradeProgress.Instance == null || !UpgradeProgress.Instance.TrySpend(AmountDue)) return false;
        billsPaid++;
        return true;
    }

    // Evicted: wipe the wallet, every upgrade and the rent schedule so the next Start is a brand new run
    public static void StartNewRun()
    {
        UpgradeProgress.ResetForNewRun();
        Upgrades.ResetForNewRun();

        if (Instance != null) Destroy(Instance.gameObject);
        Instance = null;
        new GameObject("Rent").AddComponent<Rent>();
    }
}
