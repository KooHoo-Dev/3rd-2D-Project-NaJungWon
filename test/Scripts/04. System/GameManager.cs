using UnityEngine;
using UnityEngine.Profiling;

public class GameManager : MonoBehaviour
{
    private static GameManager gameManager; 
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (gameManager != null && gameManager != this)
        {
            Destroy(gameObject);
            return;
        }

        gameManager = this;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
