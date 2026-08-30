using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private CameraBounds2D bounds;
    [SerializeField] private Vector3 offset = new(0f, 1f, -10f);
    [SerializeField] private float smoothTime = 0.15f;

    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        desiredPosition.z = offset.z;
        if (bounds != null)
        {
            desiredPosition = bounds.ClampPosition(desiredPosition);
        }
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
    }

    public void Configure(Transform followTarget)
    {
        target = followTarget;
    }

    public void ConfigureBounds(CameraBounds2D cameraBounds)
    {
        bounds = cameraBounds;
    }
}
