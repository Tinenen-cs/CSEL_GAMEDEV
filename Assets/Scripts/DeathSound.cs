using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays a sound whenever the player dies. trap.cs reloads the scene on death, so this object
// survives reloads (like MusicPlayer) and plays the clip each time the level loads again.
[RequireComponent(typeof(AudioSource))]
public class DeathSound : MonoBehaviour
{
    public float playSeconds = 2f;
    public float fadeSeconds = 0.5f;

    private static DeathSound instance;
    private AudioSource source;
    private float volume;
    private bool firstLoad = true;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        source = GetComponent<AudioSource>();
        volume = source.volume;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (firstLoad)
        {
            firstLoad = false; // the level starting for the first time is not a death
            return;
        }
        StopAllCoroutines();
        StartCoroutine(PlayOnce());
    }

    IEnumerator PlayOnce()
    {
        source.Stop();
        source.time = 0f;
        source.volume = volume;
        source.Play();
        yield return new WaitForSeconds(Mathf.Max(0f, playSeconds - fadeSeconds));
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            source.volume = Mathf.Lerp(volume, 0f, t / fadeSeconds);
            yield return null;
        }
        source.Stop();
        source.volume = volume;
    }
}
