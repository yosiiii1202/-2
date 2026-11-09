using UnityEngine;

public sealed class PachinkoBall : MonoBehaviour
{
    public bool HasScored { get; private set; }

    public bool TryScore()
    {
        if (HasScored)
        {
            return false;
        }

        HasScored = true;
        return true;
    }
}
