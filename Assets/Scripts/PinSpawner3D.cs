using UnityEngine;

public class PinSpawner3D : MonoBehaviour
{
    [SerializeField] private GameObject pinPrefab;
    [SerializeField] private float pinZ = 0.16f;

    // Authored lanes follow the domed top, leave room around the LCD and Heso,
    // then converge into the lower guide paths. The coordinates are intentionally
    // hand-set instead of generated as a uniform rectangular grid.
    private static readonly float[][] PinRows =
    {
        new[] { -2.55f, -1.85f, -1.15f, -0.4f, 0.35f, 1.1f, 1.8f, 2.5f },
        new[] { -3.0f, -2.35f, -1.7f, -1.05f, -0.4f, 0.4f, 1.05f, 1.7f, 2.35f, 3.0f },
        new[] { -3.2f, -2.65f, -2.05f, -1.45f, -0.85f, 0.85f, 1.45f, 2.05f, 2.65f, 3.2f },
        new[] { -3.3f, -2.7f, -2.1f, -1.5f, -0.9f, 0.9f, 1.5f, 2.1f, 2.7f, 3.3f },
        new[] { -3.3f, -2.75f, -2.2f, -1.65f, -1.1f, 1.1f, 1.65f, 2.2f, 2.75f, 3.3f },
        new[] { -3.25f, -2.7f, -2.15f, -1.6f, 1.6f, 2.15f, 2.7f, 3.25f },
        new[] { -3.2f, -2.65f, -2.1f, 2.1f, 2.65f, 3.2f },
        new[] { -3.1f, -2.55f, -2.0f, 2.0f, 2.55f, 3.1f },
        new[] { -3.0f, -2.4f, -1.85f, 1.85f, 2.4f, 3.0f },
        new[] { -2.9f, -2.25f, -1.6f, 1.6f, 2.25f, 2.9f },
        new[] { -2.75f, -2.1f, -1.45f, -0.75f, 0.75f, 1.45f, 2.1f, 2.75f },
        new[] { -2.65f, -2.0f, -1.35f, -0.7f, 0.7f, 1.35f, 2.0f, 2.65f },
        new[] { -2.6f, -1.95f, -1.3f, -0.65f, 0.65f, 1.3f, 1.95f, 2.6f },
        new[] { -2.55f, -1.9f, -1.25f, -0.6f, 0.6f, 1.25f, 1.9f, 2.55f },
        new[] { -2.5f, -1.85f, -1.2f, -0.55f, 0.55f, 1.2f, 1.85f, 2.5f },
        new[] { -2.4f, -1.7f, -1.0f, -0.3f, 0.3f, 1.0f, 1.7f, 2.4f }
    };

    private void Start()
    {
        CreatePins();
    }

    private void CreatePins()
    {
        if (pinPrefab == null)
        {
            Debug.LogError("PinSpawner3D: Pin Prefab が設定されていません。", this);
            return;
        }

        for (int row = 0; row < PinRows.Length; row++)
        {
            float y = 4.05f - row * 0.5f;
            foreach (float x in PinRows[row])
            {
                // Keep a narrow route clear along the left wall so the ball reaches
                // the top before it meets the upper-left pins on its way back down.
                float launchLaneX = -3.05f + (y + 3.52f) * 0.03f;
                if (Mathf.Abs(x - launchLaneX) < 0.52f) continue;

                Vector3 position = new Vector3(x, y, pinZ);
                GameObject pin = Instantiate(pinPrefab, position, Quaternion.identity, transform);
                pin.name = $"Pin_{row}_{x:0.00}";
            }
        }
    }
}
