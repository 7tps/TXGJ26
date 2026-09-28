using UnityEngine;
using UnityEngine.SceneManagement;

// Generic "load this scene" button. Point a Button's OnClick at Load() and set sceneName in the
// Inspector - used for the Upgrade Tree's Back button, and reusable anywhere else a menu button
// just needs to change scenes.
public class SceneButton : MonoBehaviour
{
    [SerializeField] string sceneName = "Lobby";

    public void Load() => SceneManager.LoadScene(TutorialSession.Route(sceneName)); // tutorial copies while in the tutorial
}
