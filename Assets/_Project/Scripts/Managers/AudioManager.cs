using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;

    [SerializeField] private AudioClip defaultMusic;
    [SerializeField] private AudioClip loadingMusic;

    [Header("Mixer Snapshots")]
    [SerializeField] private AudioMixerSnapshot normalSnapshot;
    [SerializeField] private AudioMixerSnapshot loadingSnapshot;
    [SerializeField] private float snapshotTransitionTime = 0.5f;

    [Header("Mini-game")]
    [SerializeField] private AudioClip miniGameMusic;

    private AudioClip _previousMusic;
    private bool _isInMiniGame;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        LoadVolumeSettings();
        ActivateNormalSnapshot();
        ChangeMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    public static AudioManager GetInstance()
    {
        return Instance;
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;
        if (musicSource.isPlaying && musicSource.clip == clip) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlayLoadingMusic()
    {
        if (loadingMusic != null)
        {
            PlayMusic(loadingMusic);
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip);
    }

    public void PlayUI(AudioClip clip)
    {
        if (clip == null || uiSource == null) return;
        uiSource.PlayOneShot(clip);
    }

    public void SetVolume(string parameter, float volume)
    {
        if (audioMixer == null) return;

        if (volume == 0f)
        {
            audioMixer.SetFloat(parameter, -80f);
        }
        else
        {
            audioMixer.SetFloat(parameter, Mathf.Log10(volume) * 20);
        }
    }

    private void LoadVolumeSettings()
    {
        float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        float uiVolume = PlayerPrefs.GetFloat("UIVolume", 1f);

        SetVolume("Volume_Music", musicVolume);
        SetVolume("Volume_SFX", sfxVolume);
        SetVolume("Volume_UI", uiVolume);
    }

    public void ChangeMusicForScene(string sceneName)
    {
        AudioClip newMusic = GetMusicForScene(sceneName);
        PlayMusic(newMusic);
    }

    public void ActivateLoadingSnapshot()
    {
        if (audioMixer != null)
        {
            audioMixer.SetFloat("Volume_SFX", -80f);
            audioMixer.SetFloat("Volume_UI", -80f);
        }

        if (loadingSnapshot != null)
        {
            loadingSnapshot.TransitionTo(snapshotTransitionTime);
        }
    }

    public void ActivateNormalSnapshot()
    {
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        float uiVolume = PlayerPrefs.GetFloat("UIVolume", 1f);
        SetVolume("Volume_SFX", sfxVolume);
        SetVolume("Volume_UI", uiVolume);

        if (normalSnapshot != null)
        {
            normalSnapshot.TransitionTo(snapshotTransitionTime);
        }
    }

    public void StartMiniGameMusic()
    {
        if (_isInMiniGame || miniGameMusic == null) return;

        _previousMusic = musicSource != null ? musicSource.clip : null;
        _isInMiniGame = true;
        PlayMusic(miniGameMusic);
    }

    public void StopMiniGameMusic()
    {
        if (!_isInMiniGame) return;

        _isInMiniGame = false;
        if (_previousMusic != null)
            PlayMusic(_previousMusic);
    }

    public void ReloadVolumeSettings()
    {
        float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        float uiVolume = PlayerPrefs.GetFloat("UIVolume", 1f);

        SetVolume("Volume_Music", musicVolume);
        SetVolume("Volume_SFX", sfxVolume);
        SetVolume("Volume_UI", uiVolume);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "LoadingScreen")
        {
            PlayLoadingMusic();
            return;
        }

        ReloadVolumeSettings();
        ActivateNormalSnapshot();
        ChangeMusicForScene(scene.name);
    }

    private AudioClip GetMusicForScene(string sceneName)
    {
        switch (sceneName)
        {
            case "LoadingScreen":
                return loadingMusic != null ? loadingMusic : defaultMusic;
            case "MainMenu":
            case "NewGameIntro":
            case "CreateCharacter":
            case "EndDayScreen":
            case "WinScreen":
                return LoadMusicClip("MainMenuMusic", defaultMusic);
            case "StartDay":
            case "StartDay 1":
            case "StartDayScreen":
                return LoadMusicClip("StartDayMusic", defaultMusic);
            case "MainRoom":
                return LoadMusicClip("MainRoomMusic", defaultMusic);
            case "Forge":
                return LoadMusicClip("ForgeMusic", defaultMusic);
            case "Storage":
                return LoadMusicClip("StorageMusic", defaultMusic);
        }

        if (sceneName.IndexOf("minigame", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return miniGameMusic != null ? miniGameMusic : defaultMusic;

        return defaultMusic;
    }

    private AudioClip LoadMusicClip(string resourceName, AudioClip fallback = null)
    {
        AudioClip clip = Resources.Load<AudioClip>($"Music/{resourceName}");
        return clip != null ? clip : fallback;
    }
}
