using UnityEngine;

namespace Game
{
    internal static class PlayerObjectUtility
    {
        private const string PlayerTag = "Player";

        public static bool TryGetPlayerObject(Collider collider, out GameObject playerObject)
        {
            return TryGetPlayerObject(collider != null ? collider.gameObject : null, out playerObject);
        }

        public static bool TryGetPlayerObject(GameObject sourceObject, out GameObject playerObject)
        {
            playerObject = null;
            if (sourceObject == null)
            {
                return false;
            }

            PlayerMoveController playerController = sourceObject.GetComponent<PlayerMoveController>() ??
                                                    sourceObject.GetComponentInParent<PlayerMoveController>() ??
                                                    sourceObject.GetComponentInChildren<PlayerMoveController>(true);
            if (playerController != null)
            {
                playerObject = playerController.gameObject;
                return true;
            }

            Transform current = sourceObject.transform;
            while (current != null)
            {
                if (current.CompareTag(PlayerTag))
                {
                    playerObject = current.gameObject;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        public static void NormalizeSpawnedPlayer(PlayerMoveController playerController)
        {
            if (playerController == null)
            {
                return;
            }

            int playerLayer = LayerMask.NameToLayer(PlayerTag);
            Transform[] allTransforms = playerController.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < allTransforms.Length; index++)
            {
                Transform transform = allTransforms[index];
                if (transform == null)
                {
                    continue;
                }

                GameObject currentObject = transform.gameObject;
                if (playerLayer >= 0)
                {
                    currentObject.layer = playerLayer;
                }

                if (transform == playerController.transform || currentObject.GetComponent<Collider>() != null)
                {
                    TrySetPlayerTag(currentObject);
                }
            }
        }

        private static void TrySetPlayerTag(GameObject targetObject)
        {
            if (targetObject == null || targetObject.CompareTag(PlayerTag))
            {
                return;
            }

            try
            {
                targetObject.tag = PlayerTag;
            }
            catch (UnityException)
            {
            }
        }
    }
}
