using UnityEngine;

/// <summary>Independent angles avoid accumulated drift and make each reset deterministic.</summary>
public sealed class BoneInspectionPose
{
    public float Turning { get; private set; }
    public float Tilt { get; private set; }
    public Quaternion Rotation => Quaternion.AngleAxis(Tilt, Vector3.right) *
                                  Quaternion.AngleAxis(Turning, Vector3.up);

    public void Apply(Vector2 stick, float seconds, bool resetTurning, bool resetTilt,
        float turnSpeed = 90f, float tiltSpeed = 60f, float deadZone = 0.15f)
    {
        if (resetTurning)
            Turning = 0f;
        else if (Mathf.Abs(stick.x) > deadZone)
            Turning = Mathf.Repeat(Turning - stick.x * turnSpeed * seconds + 180f, 360f) - 180f;

        if (resetTilt)
            Tilt = 0f;
        else if (Mathf.Abs(stick.y) > deadZone)
            Tilt = Mathf.Clamp(Tilt + stick.y * tiltSpeed * seconds, -90f, 90f);
    }

    public void Reset()
    {
        Turning = 0f;
        Tilt = 0f;
    }
}
