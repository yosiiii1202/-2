using UnityEngine;

public sealed class PachinkoPayoutGate : MonoBehaviour
{
    [SerializeField] private PachinkoGameController gameController;
    [SerializeField] private int laneIndex;
    [SerializeField] private string gateName = "入賞口";

    public void Configure(PachinkoGameController controller, int index, string label)
    {
        gameController = controller;
        laneIndex = index;
        gateName = label;
    }

    private void OnTriggerEnter(Collider other)
    {
        PachinkoBall ball = other.GetComponent<PachinkoBall>();
        if (ball == null || !ball.TryScore())
        {
            return;
        }

        gameController?.AwardPayout(ball, laneIndex, gateName);
    }
}
