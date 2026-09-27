using UnityEngine;

// Works alongside CameraFollow (which handles the horizontal follow): keeps the camera height
// tied to the ground the player is standing on, so it does not bob up and down with every jump.
// The camera re-centres smoothly after landing on higher or lower ground and follows the player
// down if they fall.
public class CameraGroundLock : MonoBehaviour
{
    public Transform player;
    public Transform groundCheck;
    public LayerMask whatIsGround;
    public float heightAboveGround = 0.8f;
    public float smoothTime = 0.3f;

    [Header("Keep the view below the ceilings of the start scene")]
    public float clampUntilX = 24f;
    public float clampMaxY = 0.2f;

    private float targetY;
    private float velocity;

    void Start()
    {
        targetY = transform.position.y;
    }

    void LateUpdate()
    {
        bool grounded = Physics2D.OverlapCircle(groundCheck.position, 0.2f, whatIsGround);
        if (grounded)
            targetY = player.position.y + heightAboveGround;
        else if (player.position.y < targetY - heightAboveGround - 1.5f)
            targetY = player.position.y + heightAboveGround; // falling: follow the player down

        float wanted = targetY;
        if (player.position.x < clampUntilX) wanted = Mathf.Min(wanted, clampMaxY);

        Vector3 position = transform.position;
        position.y = Mathf.SmoothDamp(position.y, wanted, ref velocity, smoothTime);
        transform.position = position;
    }
}
