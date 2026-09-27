using UnityEngine;

// Gives a hinged Rigidbody2D a push each time it swings through the bottom so it never runs out of energy.
[RequireComponent(typeof(Rigidbody2D))]
public class Pendulum : MonoBehaviour
{
    public float minAngularSpeed = 150f;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        float angle = Mathf.DeltaAngle(0f, rb.rotation);
        if (Mathf.Abs(angle) < 5f && Mathf.Abs(rb.angularVelocity) < minAngularSpeed && rb.angularVelocity != 0f)
        {
            rb.angularVelocity = Mathf.Sign(rb.angularVelocity) * minAngularSpeed;
        }
    }
}
