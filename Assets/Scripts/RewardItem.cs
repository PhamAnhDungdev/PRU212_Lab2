using UnityEngine;

namespace GameRewards
{

    // Script for rewards that can be collected by the player
    public class RewardItem : MonoBehaviour
    {
        [Header("Reward Settings")]
        public int pointValue = 10;
        public bool autoRotate = true;
        public float rotationSpeed = 30f;

        [Header("Visual Effects")]
        public GameObject collectEffectPrefab;
        public AudioClip collectSound;

        private void Start()
        {
            // Optional initialization code
            if (autoRotate)
            {
                // Start rotation animation
                StartCoroutine(AnimateRotation());
            }
        }

        // Animate the reward with rotation and bobbing
        private System.Collections.IEnumerator AnimateRotation()
        {
            float bobSpeed = 1f;
            float bobHeight = 0.2f;
            Vector3 startPos = transform.position;

            while (true)
            {
                // Rotate around Y axis
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

                // Bob up and down
                float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2) * bobHeight;
                transform.position = new Vector3(startPos.x, newY, startPos.z);

                yield return null;
            }
        }

        // Called when the reward is collected
        public void Collect()
        {
            // Play collection effect if assigned
            if (collectEffectPrefab != null)
            {
                Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
            }

            // Play sound if assigned
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position);
            }

            // Reward has been collected, so destroy the object
            Destroy(gameObject);
        }
    }
}