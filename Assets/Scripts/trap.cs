using UnityEngine;
using UnityEngine.SceneManagement;

public class trap : MonoBehaviour
{
    void OnCollisionEnter2D(Collision2D trap)
    {
        if(trap.collider.tag=="Player")
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
