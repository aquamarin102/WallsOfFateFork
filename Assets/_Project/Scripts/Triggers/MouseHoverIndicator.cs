using UnityEngine;

public class MouseHoverIndicator : MonoBehaviour
{
    private GameObject interactionIndicator; 

    private void Start()
    {
        interactionIndicator = transform.Find("InteractionIndicator")?.gameObject;

        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(false);
        }
    }

    private bool FindSibling()
    {
        bool isSibling = false;
        // Проверяем, есть ли соседний элемент с именем "InteractionIndicator"
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling != transform && sibling.name == "InteractionIndicator")
                {
                    isSibling = sibling.gameObject.activeSelf;                   
                }
            }
        }
        return isSibling;
    }

    private void Update()
    {
        if (FindSibling())
        {
            interactionIndicator.SetActive(false);
        }

    }

    private void OnMouseEnter()
    {
        
        if (FindSibling()) return;

        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(true);
        }
    }

    private void OnMouseExit()
    {
        if (interactionIndicator != null || FindSibling())
        {
            interactionIndicator.SetActive(false);
        }
    }
}