using System.Collections;
using UnityEngine;

public class MarbleBgmPlayer : MonoBehaviour
{
    [Header("BGM")]
    [Tooltip("对象生命周期开始时播放一次")]
    [SerializeField] private AudioClip startBgm;
    [Tooltip("开场曲结束后随机播放（不含开场曲），每首播完再随机下一首")]
    [SerializeField] private AudioClip[] loopClips;
    [Tooltip("BGM 播放器，不要和出币音效共用")]
    [SerializeField] private AudioSource bgmSource;

    private Coroutine playlist;

    private void Start()
    {
        if (bgmSource == null)
            return;

        bgmSource.playOnAwake = false;
        bgmSource.loop = false;
        bgmSource.pitch = 1f;
        playlist = StartCoroutine(PlayLifecycle());
    }

    private void OnDisable()
    {
        if (playlist != null)
        {
            StopCoroutine(playlist);
            playlist = null;
        }

        if (bgmSource != null)
            bgmSource.Stop();
    }

    private IEnumerator PlayLifecycle()
    {
        if (startBgm != null)
        {
            PlayClip(startBgm);
            yield return new WaitWhile(IsPlaying);
        }

        while (isActiveAndEnabled)
        {
            AudioClip next = PickRandomLoop();
            if (next == null)
                yield break;

            PlayClip(next);
            yield return new WaitWhile(IsPlaying);
        }
    }

    private bool IsPlaying()
    {
        return bgmSource != null && bgmSource.isPlaying;
    }

    private void PlayClip(AudioClip clip)
    {
        bgmSource.loop = false;
        bgmSource.pitch = 1f;
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    private AudioClip PickRandomLoop()
    {
        if (loopClips == null || loopClips.Length == 0)
            return null;

        int count = 0;
        for (int i = 0; i < loopClips.Length; i++)
        {
            if (loopClips[i] != null && loopClips[i] != startBgm)
                count++;
        }

        if (count <= 0)
            return null;

        int pick = Random.Range(0, count);
        for (int i = 0; i < loopClips.Length; i++)
        {
            AudioClip clip = loopClips[i];
            if (clip == null || clip == startBgm)
                continue;
            if (pick == 0)
                return clip;
            pick--;
        }

        return null;
    }
}
