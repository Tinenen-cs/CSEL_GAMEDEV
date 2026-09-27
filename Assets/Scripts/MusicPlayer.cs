using UnityEngine;

// Keeps the background music playing (and looping) across scene reloads caused by trap.cs.
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    private static MusicPlayer instance;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        AudioSource source = GetComponent<AudioSource>();
        source.loop = true;
        if (!source.isPlaying) source.Play();
    }
}
