using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenu : MonoBehaviour
{
    [SerializeField] string gameScene = "SampleScene";
    [SerializeField] GameObject mainPanel, settingsPanel, tutorialPanel;

    public void StartGame() => SceneManager.LoadScene(gameScene);
    public void ShowMain() => Show(mainPanel);
    public void ShowSettings() => Show(settingsPanel);
    public void ShowTutorial() => Show(tutorialPanel);
    public void QuitGame() => Application.Quit();

    void Show(GameObject panel)
    {
        mainPanel.SetActive(panel == mainPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        tutorialPanel.SetActive(panel == tutorialPanel);
    }
}