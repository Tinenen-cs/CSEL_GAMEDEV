using System.Collections;
using UnityEngine;

// Kinematic platform that turns into a falling Rigidbody2D shortly after the player lands on it.
[RequireComponent(typeof(Rigidbody2D))]
public class FallingPlatform : MonoBehaviour
{
    public float fallDelay = 0.6f;
    public float destroyAfter = 4f;

    private Rigidbody2D rb;
    private bool triggered;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (!triggered && other.collider.CompareTag("Player"))
        {
            triggered = true;
            StartCoroutine(Fall());
        }
    }

    IEnumerator Fall()
    {
        yield return new WaitForSeconds(fallDelay);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2f;
        Destroy(gameObject, destroyAfter);
    }
}
