using System;
using System.Collections;
using UnityEngine;

public class AudioPlaySystem : MonoBehaviour
{
    private AudioSource audioSource;
    [SerializeField] private AudioClip[] endAudioClip;
    [SerializeField] private AudioClip[] startAudioClip;
    private Coroutine playCoroutine;
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void GameEnd()
    {
        PlayClipWithCallback(endAudioClip);
    }

    public void GameStart()
    {
        PlayClipWithCallback(startAudioClip);
    }

    private void PlayClipWithCallback(AudioClip[] audios)
    {
        audioSource.Stop();
        audioSource.clip = audios[UnityEngine.Random.Range(0, audios.Length)];
        audioSource.Play();
        
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
        }
        
        playCoroutine = StartCoroutine(WaitForEnd(() => PlayClipWithCallback(audios)));
    }

    private IEnumerator WaitForEnd(Action onFinished)
    {
        yield return new WaitWhile(() => audioSource.isPlaying);
        onFinished?.Invoke();
    }
}
