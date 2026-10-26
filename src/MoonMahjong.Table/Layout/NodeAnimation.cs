// Ported from Practice-Riichi-Mahjong Engine/Rendering/World/WorldObjectAnimation.vala, Properties/Animation.vala and
// Properties/Path.vala (LinearPath3D, LinearPathQuat) (OpenRiichi, GPLv3).
using System.Numerics;

namespace MoonMahjong.Table.Layout;

// A position and/or rotation tween on one SceneNode. Semantics follow Animation.process exactly, in continuous time:
// - the timer starts when the animation becomes the head of its node's queue (HeadSince);
// - nothing happens until elapsed > Pre (strictly), then the start pose is captured from the node;
// - progress = (elapsed - Pre) / Time, clamped to 1, mapped by the curve, then lerp / slerp from start to end;
// - the animation leaves the queue once elapsed >= Total, so Post holds back the next queued animation.
internal sealed class NodeAnimation
{
    private readonly TableAnimationTime times;
    private readonly Curve curve;

    private bool hasPosition;
    private bool positionRelative;
    private Vector3 positionEnd;
    private Vector3 positionStart;

    private bool hasRotation;
    private Quaternion rotationEnd;
    private Quaternion rotationStart;

    private bool timerStarted;
    private bool animationStarted;
    private bool animationDone;

    public NodeAnimation(TableAnimationTime times, Curve curve)
    {
        this.times = times;
        this.curve = curve;
    }

    public TableAnimationTime Times => times;

    // When the animation became the head of its node's queue; its timer starts there.
    public float HeadSince { get; set; }

    public float StartTime { get; private set; }

    public bool Done { get; private set; }

    public void DoAbsolutePosition(Vector3 end)
    {
        hasPosition = true;
        positionRelative = false;
        positionEnd = end;
    }

    public void DoRelativePosition(Vector3 delta)
    {
        hasPosition = true;
        positionRelative = true;
        positionEnd = delta;
    }

    public void DoAbsoluteRotation(Quaternion end)
    {
        hasRotation = true;
        rotationEnd = end;
    }

    public void Process(SceneNode node, float time)
    {
        if (Done)
            return;

        if (!timerStarted)
        {
            timerStarted = true;
            StartTime = HeadSince;
        }

        float elapsed = time - StartTime;
        if (!(elapsed > times.Pre))
            return;

        if (!animationStarted)
        {
            animationStarted = true;
            Start(node);
        }

        if (!animationDone)
        {
            float t = (elapsed - times.Pre) / times.Time;

            if (t >= 1 || times.Time <= 0)
            {
                t = 1;
                animationDone = true;
            }

            t = curve.Map(t);

            if (hasPosition)
                node.Position = Vec3Math.Lerp(positionStart, positionEnd, t);
            if (hasRotation)
                node.Rotation = QuatMath.Slerp(rotationStart, rotationEnd, t);
        }

        if (elapsed >= times.Total)
            Done = true;
    }

    // WorldObject.start_animation followed by the paths' init (relative ends are resolved against the start).
    private void Start(SceneNode node)
    {
        if (hasPosition)
        {
            positionStart = node.Position;
            if (positionRelative)
                positionEnd = positionStart + positionEnd;
        }

        if (hasRotation)
            rotationStart = node.Rotation;
    }
}
