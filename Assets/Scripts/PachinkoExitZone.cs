using UnityEngine;

public sealed class PachinkoExitZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PachinkoBall ball = other.GetComponent<PachinkoBall>();
        if (ball == null || !ball.TryScore())
        {
            return;
        }

        PachinkoGameController controller = FindFirstObjectByType<PachinkoGameController>();
        controller?.LoseBall(ball);
    }
}
