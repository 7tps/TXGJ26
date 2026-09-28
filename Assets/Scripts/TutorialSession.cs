using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs the tutorial as an exact copy of the real game loop, on completely separate state.
//
// The tutorial has its own copies of the three game scenes - "Tutorial" (the table), "Tutorial Lobby" and
// "Tutorial Upgrade Tree" - which are ordinary copies of Main/Lobby/Upgrade Tree running the same scripts.
// The moment the first of them loads, the real Upgrades, wallet (UpgradeProgress) and Rent are set aside
// and fresh tutorial ones are swapped in; the moment any other scene loads, the tutorial ones are thrown
// away and the real ones come back. So everything in the tutorial works exactly like the real game but
// can never touch a real run.
//
// While the tutorial is running, Route() sends the usual scene names to the tutorial copies, so the
// table's "back to the Lobby", the Lobby's Play Game / Upgrade Tree buttons and the tree's Back button
// all stay inside the tutorial without any changes to the copied scenes.
public static class TutorialSession
{
    public const string SceneName = "Tutorial";
    public const string LobbySceneName = "Tutorial Lobby";
    public const string TreeSceneName = "Tutorial Upgrade Tree";
    const string FallbackReturnScene = "Main Menu";

    static readonly Dictionary<string, string> Routes = new Dictionary<string, string>
    {
        { "Main", SceneName },
        { "Lobby", LobbySceneName },
        { "Upgrade Tree", TreeSceneName },
    };

    public static bool Active { get; private set; }

    // The real scene the tutorial was opened from, where Exit/Finish go back to
    public static string ReturnScene { get; private set; } = FallbackReturnScene;

    static string currentScene;

    public static bool IsTutorialScene(Scene scene) => IsTutorialScene(scene.name);

    public static bool IsTutorialScene(string sceneName) =>
        sceneName == SceneName || sceneName == LobbySceneName || sceneName == TreeSceneName;

    // If a tutorial copy hasn't been made yet (Tools/Build Tutorial not run), the real scene loads
    // instead - but the tutorial stays running there on its own state (see StaysInTutorial)
    public static string Route(string sceneName) =>
        Active && Routes.TryGetValue(sceneName, out string tutorialCopy) && Application.CanStreamedLevelBeLoaded(tutorialCopy)
            ? tutorialCopy
            : sceneName;

    // The tutorial's name for a scene: real Main/Lobby/Upgrade Tree count as their tutorial copies
    public static string Normalize(string sceneName) =>
        Routes.TryGetValue(sceneName, out string tutorialCopy) ? tutorialCopy : sceneName;

    // While the tutorial runs, the real Lobby and Upgrade Tree also keep it running (used when their
    // tutorial copies don't exist yet). Only Exit/Finish or any other scene ends it.
    static bool StaysInTutorial(string sceneName) => IsTutorialScene(Normalize(sceneName)) && !exiting;

    static bool exiting;

    // Exit/Finish: ends the tutorial on the next load, whichever scene that is
    public static void ExitTo(string sceneName)
    {
        exiting = true;
        if (!Application.CanStreamedLevelBeLoaded(sceneName)) sceneName = "Lobby";
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        Active = false;
        exiting = false;
        ReturnScene = FallbackReturnScene;
        currentScene = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() => SceneManager.sceneLoaded += OnSceneLoaded;

    // sceneLoaded runs after the new scene's Awake/OnEnable but before any Start, so GameEngine and
    // CueStick (which read upgrades and money in Start) always see the right state. UI that already read
    // the wallet in OnEnable is refreshed by the Changed notification below.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        if (!Active && IsTutorialScene(scene))
        {
            ReturnScene = string.IsNullOrEmpty(currentScene) || IsTutorialScene(currentScene) ? FallbackReturnScene : currentScene;
            Enter();
        }
        else if (Active && !StaysInTutorial(scene.name))
        {
            Exit();
        }
        exiting = false;

        currentScene = scene.name;
        if (Active) TutorialDirector.OnSceneLoaded(scene);
    }

    static void Enter()
    {
        Active = true;
        Rent.EnterSandbox();
        Upgrades.EnterSandbox();
        UpgradeProgress.EnterSandbox();
        TutorialDirector.Create();
        UpgradeProgress.NotifyChanged();
    }

    static void Exit()
    {
        Active = false;
        TutorialDirector.DestroyInstance();
        UpgradeProgress.ExitSandbox();
        Upgrades.ExitSandbox();
        Rent.ExitSandbox();
        UpgradeProgress.NotifyChanged();
    }
}
