using UnityEngine;

namespace Assets.Scripts
{
    /// <summary>
    ///     Script for orientating the game object towards the user.
    /// </summary>
    public class LookAtCamera : MonoBehaviour
    {
        private Transform cameraTransform;

        private void Start()
        {
            cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            this.gameObject.transform.LookAt(cameraTransform);
            this.gameObject.transform.Rotate(Vector3.up, 180f);
        }
    }
}
