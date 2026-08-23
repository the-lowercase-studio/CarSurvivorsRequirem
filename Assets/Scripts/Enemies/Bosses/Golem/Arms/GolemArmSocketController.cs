using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Arms
{
    public interface IGolemArmSocketController
    {
        GolemArmProjectile LeftArm { get; }
        GolemArmProjectile RightArm { get; }
        bool AreBothArmsDocked { get; }
        bool IsAnyArmDocked { get; }
        void Initialize();
        void ResetAllArms();
    }

    public class GolemArmSocketController : MonoBehaviour, IGolemArmSocketController
    {
        [SerializeField] private Transform _leftArmSocket;
        [SerializeField] private Transform _rightArmSocket;
        [SerializeField] private GameObject _leftRigArmVisual;
        [SerializeField] private GameObject _rightRigArmVisual;
        [SerializeField] private GolemArmProjectile _leftArmProjectile;
        [SerializeField] private GolemArmProjectile _rightArmProjectile;

        public GolemArmProjectile LeftArm => _leftArmProjectile;
        public GolemArmProjectile RightArm => _rightArmProjectile;

        public bool AreBothArmsDocked =>
            (_leftArmProjectile == null || _leftArmProjectile.IsDocked) &&
            (_rightArmProjectile == null || _rightArmProjectile.IsDocked);

        public bool IsAnyArmDocked =>
            (_leftArmProjectile != null && _leftArmProjectile.IsDocked) ||
            (_rightArmProjectile != null && _rightArmProjectile.IsDocked);

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_leftArmProjectile != null && _leftArmSocket != null)
            {
                _leftArmProjectile.Initialize(_leftArmSocket, _leftRigArmVisual);
            }

            if (_rightArmProjectile != null && _rightArmSocket != null)
            {
                _rightArmProjectile.Initialize(_rightArmSocket, _rightRigArmVisual);
            }
        }

        public void ResetAllArms()
        {
            if (_leftArmProjectile != null)
            {
                _leftArmProjectile.DockToSocket();
            }

            if (_rightArmProjectile != null)
            {
                _rightArmProjectile.DockToSocket();
            }
        }
    }
}
