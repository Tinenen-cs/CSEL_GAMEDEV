using UnityEngine;

public class CameraFollow : MonoBehaviour
{
	public GameObject followObject;

	public Vector2 followOffset;

	public float speed = 3f;

	private Vector2 threshold;

	private Rigidbody2D rb;

	private void Start()
	{
		threshold = calculateThreshold();
		rb = followObject.GetComponent<Rigidbody2D>();
	}

	private void FixedUpdate()
	{
		Vector2 vector = followObject.transform.position;
		float f = Vector2.Distance(Vector2.right * base.transform.position.x, Vector2.right * vector.x);
		float f2 = Vector2.Distance(Vector2.up * base.transform.position.y, Vector2.up * vector.y);
		Vector3 position = base.transform.position;
		if (Mathf.Abs(f) >= threshold.x)
		{
			position.x = vector.x;
		}
		if (Mathf.Abs(f2) >= threshold.y)
		{
			position.y = vector.y;
		}
		float num = ((!(rb.linearVelocity.magnitude > speed)) ? speed : rb.linearVelocity.magnitude);
		base.transform.position = Vector3.MoveTowards(base.transform.position, position, num * Time.deltaTime);
	}

	private Vector3 calculateThreshold()
	{
		Rect pixelRect = Camera.main.pixelRect;
		Vector2 vector = new Vector2(Camera.main.orthographicSize * pixelRect.width / pixelRect.height, Camera.main.orthographicSize);
		vector.x -= followOffset.x;
		vector.y -= followOffset.y;
		return vector;
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.blue;
		Vector2 vector = calculateThreshold();
		Gizmos.DrawWireCube(base.transform.position, new Vector3(vector.x * 2f, vector.y * 2f, 1f));
	}
}
