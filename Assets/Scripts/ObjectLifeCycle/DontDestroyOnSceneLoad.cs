using UnityEngine;

namespace Assets.Scripts.ObjectLifecycle
{
    public class DontDestroyOnSceneLoad : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
