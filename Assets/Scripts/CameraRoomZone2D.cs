using UnityEngine;

public sealed class CameraRoomZone2D : MonoBehaviour
{
    [SerializeField] private Vector2 size = new(20f, 10f);
    [SerializeField] private int priority;

    public int Priority => priority;
    public Vector2 Size => size;

    public void Configure(Vector2 zoneSize, int zonePriority)
    {
        size = zoneSize;
        priority = zonePriority;
    }

    public bool Contains(Vector3 worldPosition)
    {
        Bounds bounds = GetBounds();
        return bounds.Contains(worldPosition);
    }

    public Bounds GetBounds()
    {
        return new Bounds(transform.position, new Vector3(size.x, size.y, 1f));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 1f));
    }
}
