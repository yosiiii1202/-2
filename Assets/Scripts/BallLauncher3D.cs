using UnityEngine;
using UnityEngine.InputSystem;

public class BallLauncher3D : MonoBehaviour
{
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform launchPoint;

    [SerializeField] private float launchPower = 8f;

    private void Update()
    {
        // スペースキーを押した瞬間
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            LaunchBall();
        }
    }

    public void LaunchBall()
    {
        if (ballPrefab == null)
        {
            Debug.LogError("Ball Prefabが設定されていません");
            return;
        }

        if (launchPoint == null)
        {
            Debug.LogError("Launch Pointが設定されていません");
            return;
        }

        // 玉を生成
        GameObject ball = Instantiate(
            ballPrefab,
            launchPoint.position,
            Quaternion.identity
        );

        // Rigidbody取得
        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // 左下から右上へ飛ばす
            Vector3 direction = new Vector3(
                0.8f,
                1.5f,
                0f
            ).normalized;

            rb.linearVelocity = direction * launchPower;
        }
        else
        {
            Debug.LogError("BallにRigidbodyがありません");
        }
    }
}