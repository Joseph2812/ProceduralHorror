namespace Scripts.Generation.Interior;

public abstract class TreeNode
{
    public abstract bool Evaluate();
}

public class ValueNode : TreeNode
{
    public bool Value;

    public ValueNode(bool value = false) { Value = value; }

    public override bool Evaluate() => Value;
    public override string ToString() => Value.ToString();
}

public abstract class OperatorNode : TreeNode
{
    public TreeNode Left { get; set; }
    public override string ToString() => $"[OP: {Left}]";
}

// Unary //
public class NotNode : OperatorNode
{
    public override bool Evaluate() => !Left.Evaluate();
    public override string ToString() => $"[NOT: {Left}]";
}

// Binary //
public abstract class BinaryNode : OperatorNode
{
    public TreeNode Right { get; set; }
    public bool Complete => Left != null && Right != null;

    public override string ToString() => $"[BINARY: {Left}, {Right}]";
}
public class AndNode : BinaryNode
{
    public override bool Evaluate() => Left.Evaluate() && Right.Evaluate();
    public override string ToString() => $"[AND: {Left}, {Right}]";
}
public class OrNode : BinaryNode
{
    public override bool Evaluate() => Left.Evaluate() || Right.Evaluate();
    public override string ToString() => $"[OR: {Left}, {Right}]";
}
public class XorNode : BinaryNode
{
    public override bool Evaluate() => Left.Evaluate() != Right.Evaluate();
    public override string ToString() => $"[XOR: {Left}, {Right}]";
}
