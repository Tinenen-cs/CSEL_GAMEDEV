using UnityEngine;

// Shows a win message and the run time when the player reaches the flag.
public class FinishLine : MonoBehaviour
{
    private bool finished;
    private float finishTime;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!finished && other.CompareTag("Player"))
        {
            finished = true;
            finishTime = Time.timeSinceLevelLoad;
        }
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.UpperCenter };
        style.normal.textColor = Color.black;
        if (finished)
        {
            GUI.Label(new Rect(0, Screen.height / 3f, Screen.width, 120),
                "FINISH!\nTime: " + finishTime.ToString("0.0") + "s", style);
        }
        else
        {
            style.fontSize = 22;
            style.alignment = TextAnchor.UpperLeft;
            GUI.Label(new Rect(16, 12, 300, 40), "Time: " + Time.timeSinceLevelLoad.ToString("0.0") + "s", style);
        }
    }
}
