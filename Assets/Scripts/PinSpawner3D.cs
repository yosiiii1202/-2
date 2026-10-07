using UnityEngine;

public class PinSpawner3D : MonoBehaviour
{
    [SerializeField] private GameObject pinPrefab;

    [SerializeField] private int rows = 9;
    [SerializeField] private int columns = 11;

    [SerializeField] private float spacingX = 0.7f;
    [SerializeField] private float spacingY = 0.7f;

    [SerializeField] private float pinZ = -0.2f;

    private void Start()
    {
        CreatePins();
    }

    private void CreatePins()
    {
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                float posX =
                    (x - (columns - 1) / 2f) * spacingX;

                float posY =
                    ((rows - 1) / 2f - y) * spacingY;

                // Šï”s‚ð”¼•ª‚¸‚ç‚·
                if (y % 2 == 1)
                {
                    posX += spacingX * 0.5f;
                }

                Vector3 position = new Vector3(
                    posX,
                    posY,
                    pinZ
                );

                GameObject pin = Instantiate(
                    pinPrefab,
                    position,
                    Quaternion.identity,
                    transform
                );

                pin.name = $"Pin_{x}_{y}";
            }
        }
    }
}