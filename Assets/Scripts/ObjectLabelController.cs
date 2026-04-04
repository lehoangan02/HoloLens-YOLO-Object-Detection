using TMPro;
using UnityEngine;

namespace Assets.Scripts
{
    /// <summary>
    ///     Manages the visual aspects of the object label.
    /// </summary>
    public class ObjectLabelController : MonoBehaviour
    {
        /// <summary>
        ///     Parent of the displayed label.
        /// </summary>
        public GameObject ContentParent;

        /// <summary>
        ///     Renderer for showing a line between the center of the object and the label.
        /// </summary>
        public LineRenderer LineRenderer;

        /// <summary>
        ///     Text mesh for displaying the class of the object.
        /// </summary>
        public TextMeshPro TextMesh;

        private Vector3 targetPosition;
        private bool positionInitialized;
        private const float LerpSpeed = 8f;

        /// <summary>
        ///     Sets the display text.
        /// </summary>
        public string Text
        {
            set => this.TextMesh.text = value;
            get => this.TextMesh.text;
        }

        private void Update()
        {
            if (positionInitialized && transform.position != targetPosition)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * LerpSpeed);
                this.LineRenderer.SetPosition(0, this.ContentParent.transform.position);
                this.LineRenderer.SetPosition(1, this.transform.position);
            }
        }

        /// <summary>
        ///     Updates the target position; the label smoothly interpolates towards it.
        /// </summary>
        /// <param name="newPosition">New position of the object.</param>
        public void UpdatePosition(Vector3 newPosition)
        {
            targetPosition = newPosition;
            if (!positionInitialized)
            {
                this.transform.position = newPosition;
                positionInitialized = true;
            }

            this.LineRenderer.SetPosition(0, this.ContentParent.transform.position);
            this.LineRenderer.SetPosition(1, this.transform.position);
        }
    }
}
