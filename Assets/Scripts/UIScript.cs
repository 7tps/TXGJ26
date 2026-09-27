using UnityEngine;

public class UIScript : MonoBehaviour
{
    public CueStick cueStick;
    public GameEngine gameEngine;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void redrawCueStick()
    {
        Ball cueBall = gameEngine.getBall(0);
        cueStick.Show(cueBall);
    }
}
