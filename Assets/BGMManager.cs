using UnityEngine;
using System.Collections.Generic;

// 배경음악을 랜덤 셔플로 반복 재생
public class BGMManager : MonoBehaviour
{
    [Header("BGM 설정")]
    [Tooltip("재생할 음악 클립 목록")]
    public AudioClip[] bgmClips;

    [Range(0f, 1f)]
    public float volume = 0.3f;

    private AudioSource _audioSource;
    private List<int> _shuffledOrder = new List<int>();
    private int _currentIndex = 0;

    void Start()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.loop = false;
        _audioSource.volume = volume;
        _audioSource.spatialBlend = 0f; // 2D 사운드 (어디서든 동일하게 들림)

        if (bgmClips != null && bgmClips.Length > 0)
        {
            Shuffle();
            PlayNext();
        }
    }

    void Update()
    {
        // 현재 곡이 끝나면 다음 곡 재생
        if (!_audioSource.isPlaying && bgmClips != null && bgmClips.Length > 0)
        {
            PlayNext();
        }
    }

    private void PlayNext()
    {
        if (_currentIndex >= _shuffledOrder.Count)
        {
            Shuffle(); // 다 돌았으면 다시 셔플
        }

        _audioSource.clip = bgmClips[_shuffledOrder[_currentIndex]];
        _audioSource.volume = volume;
        _audioSource.Play();
        _currentIndex++;
    }

    private void Shuffle()
    {
        _shuffledOrder.Clear();
        for (int i = 0; i < bgmClips.Length; i++)
            _shuffledOrder.Add(i);

        // Fisher-Yates 셔플
        for (int i = _shuffledOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = _shuffledOrder[i];
            _shuffledOrder[i] = _shuffledOrder[j];
            _shuffledOrder[j] = temp;
        }

        _currentIndex = 0;
    }
}
