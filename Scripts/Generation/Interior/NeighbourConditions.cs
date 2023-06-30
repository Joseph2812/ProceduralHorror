using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using static Scripts.Generation.MapGenerator;

namespace Scripts.Generation.Interior;

/// <summary>
/// Used to specify the conditions around a cell and whether they are satisfied.<br/>
/// Builds a tree from a given string which contains a boolean expression, that can then be reused with different values for the directions.<para/>
/// 
/// Call <see cref="ParseIntoTree(string)"/> first before calling <see cref="IsSatisfied(Scripts.Generation.NeighbourInfo[], float)"/> to initialise the tree.
/// </summary>
public class NeighbourConditions
{
    private static readonly ValueNode[] _valueNodes = new ValueNode[Enum.GetValues(typeof(All3x3x3Dir)).Length];
    private static readonly ValueNode _alwaysTrueNode = new ValueNode(true);

    private TreeNode _root;

    static NeighbourConditions()
    {
        for (int i = 0; i < _valueNodes.Length; i++) { _valueNodes[i] = new(); }
    }

    /// <summary>
    /// Converts the given string into a tree of nodes for dynamic boolean expression computation. Use <see cref="IsSatisfied(Scripts.Generation.NeighbourInfo[], float)"/> to get the result based on the neighbours.<br/>
    /// <paramref name="conditionsToParse"/> is a string that represents a boolean expression, with directions (lowercase), brackets, and operators.<para/>
    /// 
    /// Directions: left[i], forward[i], right[i], back[i], nw[i], ne[i], se[i], sw[i]. Where [i] = 0 or 1 or 2, to represent relative y (1 = Middle).<br/>
    /// Operators: <![CDATA[! (NOT), & (AND), | (OR), ^ (XOR).]]><para/>
    /// 
    /// Example:<br/><![CDATA[(!left0 & right1) | (forward2 ^ back0)]]> 
    /// </summary>
    public void ParseIntoTree(string conditionsToParse)
    {
#if DEBUG
        GD.Print($"=== Parsing ===\n{conditionsToParse}\n");
#endif
        if (conditionsToParse.Length == 0)
        {
            _root = _alwaysTrueNode;
            return;
        }

        _root = GetNode
        (
            conditionsToParse.Replace("\n", string.Empty).Replace(" ", string.Empty)
        )
        .Item1;
    }

    public bool IsSatisfied(NeighbourInfo[] all3x3x3Neighbours, float rotationY = 0f)
    {
        UpdateValueNodes(NeighbourInfo.RotateNeighbours(all3x3x3Neighbours, rotationY));
        return _root.Evaluate();
    }

    /// <returns>(Root <see cref="TreeNode"/>, End index)</returns>
    private (TreeNode, int) GetNode(string conditions, int startIdx = 0)
    {
        Stack<TreeNode> stack = new();  
        for (int i = startIdx; i < conditions.Length; i++)
        {
            char c = conditions[i];
            switch (c)
            {
                // Brackets //
                case '(':
#if DEBUG
                    GD.Print("--- Entering sub-stack ---");
#endif
                    (TreeNode rootNode, int endIdx) = GetNode(conditions, i + 1);
                    i = endIdx;
#if DEBUG
                    GD.Print("--- Returning from sub-stack ---");
#endif
                    stack.Push(rootNode);
                    CheckStackForMerging(stack);
                    break;

                case ')':
                    return (stack.Pop(), i); // There should only be one thing left in the stack at this point

                // Unary Operators //
                case '!':
                    stack.Push(new NotNode());
                    break;

                // Binary Operators //
                case '&':
                    stack.Push(new AndNode() { Left = stack.Pop() });
                    break;

                case '|':
                    stack.Push(new OrNode() { Left = stack.Pop() });
                    break;

                case '^':
                    stack.Push(new XorNode() { Left = stack.Pop() });
                    break;

                // Variable //
                default:
                    StringBuilder directionName = new(c.ToString());

                    int j;
                    for (j = i + 1; j < conditions.Length; j++)
                    {
                        char nextC = conditions[j];
                        if (IsOperator(nextC) || IsBracket(nextC) )
                        {
                            i = j - 1;
                            break;
                        }
                        directionName.Append(nextC);
                    }
                    if (j == conditions.Length) { i = j; } // Reached end of string

                    stack.Push
                    (
                        GetValueNode(directionName.ToString())
                    );
                    CheckStackForMerging(stack);
                    break;
            }
#if DEBUG
            int num = 0;
            foreach (TreeNode node in stack)
            {
                GD.Print($"[{num++}] = {node}");
            }
#endif
        }
        return (stack.Pop(), conditions.Length); // There should only be one thing left in the stack at this point
    }

    private bool IsOperator(char c) => c == '!' || c == '&' || c == '|' || c == '^';
    private bool IsBracket(char c) => c == '(' || c == ')';

    private void CheckStackForMerging(Stack<TreeNode> stack)
    {
        TreeNode topNode = stack.Peek();
        if
        (
            (topNode is not BinaryNode) ||
            (topNode is BinaryNode bl && bl.Complete)
        )
        {
            while (stack.Count > 1)
            {
                topNode = stack.Pop();
                TreeNode nextNode = stack.Peek();
#if DEBUG
                GD.Print($"{nextNode} <-MERGE- {topNode}");
#endif
                if (nextNode is NotNode notNode)         { notNode.Left = topNode; }
                else if (nextNode is BinaryNode binNode) { binNode.Right = topNode; }
            }
        }
    }

    private void UpdateValueNodes(NeighbourInfo[] all3x3x3Neighbours) // TODO: Maybe this gets rotated to be relative to object's rotation
    {
        for (int i = 0; i < _valueNodes.Length; i++)
        {
            _valueNodes[i].Value = !all3x3x3Neighbours[i].Empty;
        }
    }

    private ValueNode GetValueNode(string directionName)
    {
        switch (directionName)
        {
            case "left0"   : return _valueNodes[(int)All3x3x3Dir.Left0];
            case "forward0": return _valueNodes[(int)All3x3x3Dir.Forward0];
            case "right0"  : return _valueNodes[(int)All3x3x3Dir.Right0];
            case "back0"   : return _valueNodes[(int)All3x3x3Dir.Back0];
            case "nw0"     : return _valueNodes[(int)All3x3x3Dir.NW0];
            case "ne0"     : return _valueNodes[(int)All3x3x3Dir.NE0];
            case "se0"     : return _valueNodes[(int)All3x3x3Dir.SE0];
            case "sw0"     : return _valueNodes[(int)All3x3x3Dir.SW0];

            case "left1"   : return _valueNodes[(int)All3x3x3Dir.Left1];
            case "forward1": return _valueNodes[(int)All3x3x3Dir.Forward1];
            case "right1"  : return _valueNodes[(int)All3x3x3Dir.Right1];
            case "back1"   : return _valueNodes[(int)All3x3x3Dir.Back1];
            case "nw1"     : return _valueNodes[(int)All3x3x3Dir.NW1];
            case "ne1"     : return _valueNodes[(int)All3x3x3Dir.NE1];
            case "se1"     : return _valueNodes[(int)All3x3x3Dir.SE1];
            case "sw1"     : return _valueNodes[(int)All3x3x3Dir.SW1];

            case "left2"   : return _valueNodes[(int)All3x3x3Dir.Left2];
            case "forward2": return _valueNodes[(int)All3x3x3Dir.Forward2];
            case "right2"  : return _valueNodes[(int)All3x3x3Dir.Right2];
            case "back2"   : return _valueNodes[(int)All3x3x3Dir.Back2];
            case "nw2"     : return _valueNodes[(int)All3x3x3Dir.NW2];
            case "ne2"     : return _valueNodes[(int)All3x3x3Dir.NE2];
            case "se2"     : return _valueNodes[(int)All3x3x3Dir.SE2];
            case "sw2"     : return _valueNodes[(int)All3x3x3Dir.SW2];

            default: throw new NotImplementedException($"\"{directionName}\" is not a valid direction.");
        }
    }
}
