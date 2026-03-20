using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler
{
    [Header("Button visuals")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;

    [Header("Audio")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    private bool isPointerOver = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (InputModeTracker.UsingKeyboard) return;

        isPointerOver = true;
        SetHoverSprite();
        PlayHoverSound();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (InputModeTracker.UsingKeyboard) return;

        isPointerOver = false;
        SetNormalSprite();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClickSound();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!isPointerOver)
        {
            SetHoverSprite();
            PlayHoverSound();
        }
    }

    public void ForceExit()
    {
        isPointerOver = false;
        SetNormalSprite();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!isPointerOver)
            SetNormalSprite();
    }

    private void SetHoverSprite()
    {
        if (hoverSprite != null && buttonImage != null)
            buttonImage.sprite = hoverSprite;
    }

    private void SetNormalSprite()
    {
        if (normalSprite != null && buttonImage != null)
            buttonImage.sprite = normalSprite;
    }

    private void PlayHoverSound()
    {
        if (hoverSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayUI(hoverSound);
    }

    private void PlayClickSound()
    {
        if (clickSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayUI(clickSound);
    }

    private void OnEnable()
    {
        SetNormalSprite();
    }
}
