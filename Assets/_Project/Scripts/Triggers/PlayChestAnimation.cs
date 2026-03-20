using Game;
using UnityEngine;

public class PlayChestAnimation : MonoBehaviour
{
    [SerializeField] private bool _dependFromQuests = false;
    [SerializeField] private string animationName = "Armature_Chest|Chest_Open";
    [Header("Audio")]
    [SerializeField] private AudioSource openAudioSource;
    [SerializeField] private AudioClip[] openSounds;
    [SerializeField] [Range(0f, 1f)] private float openVolume = 1f;
    [SerializeField] private bool configureSourceAs3D = true;

    private Animator _anim;
    private bool _opened;

    private void Awake()
    {
        _anim = GetComponentInChildren<Animator>();

        if (openAudioSource == null)
            openAudioSource = GetComponent<AudioSource>();

        if (openAudioSource == null)
            openAudioSource = gameObject.AddComponent<AudioSource>();

        ConfigureAudioSource();
    }

    //private void Start()
    //{
    //    influenceAria = this.GetComponent<InteractibleItemInfluenceArea>();

    //    influenceAria.OnItemInteracted += Triggered;
    //}

    // вызывается InteractManager-ом через TryTrigger(...)
    public void Triggered(TriggerEvent eventData) {
        if (!eventData.IsEnteracted) return;
        
        //dwCompositeTrigger compositeTrigger = this.gameObject.GetComponent<CompositeTrigger>();
        if (_opened) return;
        else {
            //if (_dependFromQuests) {
            //    if (!compositeTrigger.IsDone) return;
            //}
        }
        _opened = true;
        PlayOpenSound();
        if (_anim) _anim.Play(animationName);
    }

    private void PlayOpenSound()
    {
        if (openSounds == null || openSounds.Length == 0)
            return;

        AudioClip clip = openSounds[Random.Range(0, openSounds.Length)];
        if (clip == null)
            return;

        ConfigureAudioSource();
        openAudioSource.PlayOneShot(clip, openVolume);
    }

    private void ConfigureAudioSource()
    {
        if (openAudioSource == null)
            return;

        openAudioSource.playOnAwake = false;
        openAudioSource.loop = false;

        if (!configureSourceAs3D)
            return;

        openAudioSource.spatialBlend = 1f;
        openAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        openAudioSource.minDistance = 1.5f;
        openAudioSource.maxDistance = 12f;
        openAudioSource.dopplerLevel = 0f;
    }
}
