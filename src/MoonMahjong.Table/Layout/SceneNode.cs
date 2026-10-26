// Ported from Practice-Riichi-Mahjong Engine/Rendering/World/WorldObject.vala and Properties/Transform.vala
// (OpenRiichi, GPLv3): the parts that place objects (hierarchy, transforms, buffered animations), not rendering.
using System.Collections.Generic;
using System.Numerics;

namespace MoonMahjong.Table.Layout;

// A local transform (position, rotation, uniform scale) composed like Calculations.get_model_matrix (T * R * S) and
// Transform.get_full_matrix (parent * local). Every scale in the table is uniform, so the composition is exact without
// matrices. That also avoids Mat4.get_rotation, which can lose a component for exact half-turns (see table-layout.md).
internal readonly record struct NodeTransform(Vector3 Position, Quaternion Rotation, float Scale)
{
    public static readonly NodeTransform Identity = new(Vector3.Zero, QuatMath.Identity, 1);

    // this * local: local expressed in this transform's parent space.
    public NodeTransform Compose(NodeTransform local) => new(
        Position + QuatMath.Rotate(Rotation, local.Position * Scale),
        QuatMath.Mul(Rotation, local.Rotation),
        Scale * local.Scale);

    // inverse(this) * world: world expressed in this transform's space.
    public NodeTransform Relative(NodeTransform world)
    {
        var inverse = QuatMath.Inverse(Rotation);
        return new NodeTransform(
            QuatMath.Rotate(inverse, world.Position - Position) / Scale,
            QuatMath.Mul(inverse, world.Rotation),
            world.Scale / Scale);
    }
}

// WorldObject: a node in the table's scene graph with a local transform, a parent, children and a queue of buffered
// animations (only the head runs; animate() appends, cancel_buffered_animations() clears).
internal sealed class SceneNode
{
    private readonly SceneGraph graph;
    private readonly List<SceneNode> children = new();
    private readonly List<NodeAnimation> bufferedAnimations = new();

    public SceneNode(SceneGraph graph)
    {
        this.graph = graph;
    }

    public Vector3 Position { get; set; } = Vector3.Zero;
    public Quaternion Rotation { get; set; } = QuatMath.Identity;
    public float Scale { get; set; } = 1;
    public bool Visible { get; set; } = true;

    public SceneNode? Parent { get; private set; }
    public IReadOnlyList<SceneNode> Children => children;

    internal bool Registered { get; set; }
    internal bool HasAnimations => bufferedAnimations.Count > 0;

    public NodeTransform LocalTransform => new(Position, Rotation, Scale);

    public NodeTransform WorldTransform => Parent == null ? LocalTransform : Parent.WorldTransform.Compose(LocalTransform);

    // add_object: re-parents and keeps the child's local transform.
    public void AddObject(SceneNode child)
    {
        if (child.Parent == this)
            return;
        Reparent(child);
    }

    // convert_object: re-parents and keeps the child's world transform.
    public void ConvertObject(SceneNode child)
    {
        if (child.Parent == this)
            return;

        var local = WorldTransform.Relative(child.WorldTransform);
        Reparent(child);
        child.Position = local.Position;
        child.Rotation = local.Rotation;
        child.Scale = local.Scale;
    }

    // remove_object: detaches the child; its local transform is kept.
    public void RemoveObject(SceneNode child)
    {
        if (children.Remove(child))
            child.Parent = null;
    }

    public void Animate(NodeAnimation animation)
    {
        if (bufferedAnimations.Count == 0)
            animation.HeadSince = graph.CurrentTime;
        bufferedAnimations.Add(animation);
        graph.Register(this);
    }

    public void CancelBufferedAnimations() => bufferedAnimations.Clear();

    // Runs the head animation up to `time`; when it finishes, the next one becomes head at the moment it finished.
    internal void AdvanceAnimations(float time)
    {
        while (bufferedAnimations.Count > 0)
        {
            var head = bufferedAnimations[0];
            head.Process(this, time);
            if (!head.Done)
                return;

            bufferedAnimations.RemoveAt(0);
            if (bufferedAnimations.Count > 0)
                bufferedAnimations[0].HeadSince = head.StartTime + head.Times.Total;
        }
    }

    private void Reparent(SceneNode child)
    {
        child.Parent?.children.Remove(child);
        children.Add(child);
        child.Parent = this;
    }
}

// The clock shared by every node: animations are advanced lazily, in time order, to the time the model is asked about.
internal sealed class SceneGraph
{
    private readonly List<SceneNode> animating = new();

    // Set when an animation is queued; advancing again to the same time is a no-op otherwise.
    private bool animationQueued;

    public SceneGraph(float startTime)
    {
        CurrentTime = startTime;
    }

    public float CurrentTime { get; private set; }

    public void Register(SceneNode node)
    {
        animationQueued = true;
        if (node.Registered)
            return;
        node.Registered = true;
        animating.Add(node);
    }

    public void AdvanceTo(float time)
    {
        if (time < CurrentTime)
            throw new System.InvalidOperationException($"The scene clock cannot run backwards ({time} < {CurrentTime}).");

        if (time == CurrentTime && !animationQueued)
            return;

        CurrentTime = time;
        animationQueued = false;
        foreach (var node in animating)
            node.AdvanceAnimations(time);

        animating.RemoveAll(node =>
        {
            if (node.HasAnimations)
                return false;
            node.Registered = false;
            return true;
        });
    }
}
