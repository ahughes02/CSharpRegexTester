using System.ComponentModel;

namespace CSharpRegexTester.Models;

/// <summary>
/// Classifies the functional role of a match tree node.
/// </summary>
public enum MatchTreeNodeType
{
    Root,
    Match,
    CapturesContainer,
    Capture,
    GroupsContainer,
    Group,
    Info,
    Message
}

/// <summary>
/// Pure domain representation of a hierarchical tree node for match visualization,
/// decoupled from WPF UI controls so it can be built and processed on background threads.
/// </summary>
public sealed class MatchTreeNode : INotifyPropertyChanged
{
    private bool isExpanded;

    public string Header { get; }
    public MatchTreeNodeType NodeType { get; }
    public TextSpan? Span { get; }
    public IReadOnlyList<MatchTreeNode> Children { get; }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded != value)
            {
                isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MatchTreeNode(
        string header,
        MatchTreeNodeType nodeType = MatchTreeNodeType.Message,
        TextSpan? span = null,
        IReadOnlyList<MatchTreeNode>? children = null,
        bool isExpanded = false)
    {
        Header = header;
        NodeType = nodeType;
        Span = span;
        Children = children ?? [];
        this.isExpanded = isExpanded;
    }

    public override string ToString() => Header;
}
