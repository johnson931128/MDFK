using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class CameraBounds2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private List<CameraRoomZone2D> zones = new();

    private CameraRoomZone2D currentZone;

    public void Configure(Transform followTarget, IEnumerable<CameraRoomZone2D> roomZones)
    {
        target = followTarget;
        zones = new List<CameraRoomZone2D>(roomZones);
        currentZone = null;
    }

    public Vector3 ClampPosition(Vector3 desiredPosition)
    {
        CameraRoomZone2D zone = SelectZone();
        if (zone == null)
        {
            return desiredPosition;
        }

        Camera camera = GetComponent<Camera>();
        Bounds bounds = zone.GetBounds();
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        desiredPosition.x = ClampAxis(desiredPosition.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth, bounds.center.x);
        desiredPosition.y = ClampAxis(desiredPosition.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight, bounds.center.y);
        return desiredPosition;
    }

    private CameraRoomZone2D SelectZone()
    {
        if (target == null || zones == null || zones.Count == 0)
        {
            return currentZone;
        }

        if (currentZone != null && currentZone.Contains(target.position))
        {
            return currentZone;
        }

        CameraRoomZone2D selected = null;
        foreach (CameraRoomZone2D zone in zones)
        {
            if (zone == null || !zone.Contains(target.position))
            {
                continue;
            }

            if (selected == null || zone.Priority > selected.Priority)
            {
                selected = zone;
            }
        }

        if (selected != null)
        {
            currentZone = selected;
        }

        return currentZone;
    }

    private static float ClampAxis(float value, float minimum, float maximum, float fallback)
    {
        return minimum > maximum ? fallback : Mathf.Clamp(value, minimum, maximum);
    }
}
