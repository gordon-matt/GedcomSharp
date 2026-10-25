using System.Diagnostics;

namespace GedcomSharp;

/// <summary>
/// One node of a GEDCOM dataset: a tag, an optional cross-reference id, a payload that is either text or a pointer,
/// and its substructures. Every line of a file maps onto this tree, including tags this library knows nothing about,
/// so a file read and written again keeps all of its data. The typed records in <see cref="Model"/> are views over it.
/// </summary>
/// <remarks>
/// Payload text is stored decoded: CONC lines are joined, CONT lines become <c>\n</c>, and @@ escapes are undone.
/// Xrefs and pointers are stored without their surrounding @ signs (<c>I1</c>, not <c>@I1@</c>).
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class GedcomStructure
{
    /// <summary>The pointer value GEDCOM 7 uses for a deliberately empty pointer (<c>@VOID@</c>).</summary>
    public const string VOID_POINTER = "VOID";

    private string tagName;
    private string? xrefId;
    private string? text;
    private string? pointerTarget;

    public GedcomStructure(string tag, string? value = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        tagName = tag;
        text = value;
        Children = new GedcomStructureCollection(this);
    }

    public static GedcomStructure CreatePointer(string tag, string pointer) => new(tag) { Pointer = pointer };

    public string Tag
    {
        get => tagName;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            tagName = value;
        }
    }

    /// <summary>The record's cross-reference id without @ signs. Only records (level 0) should have one.</summary>
    public string? Xref
    {
        get => xrefId;
        set
        {
            string? previous = xrefId;
            xrefId = NormalizeXref(value);
            if (previous != xrefId)
            {
                Owner?.OnXrefChanged();
            }
        }
    }

    /// <summary>The text payload, or null. Setting a non-null value clears <see cref="Pointer"/>.</summary>
    public string? Value
    {
        get => text;
        set
        {
            text = value;
            if (value is not null)
            {
                pointerTarget = null;
            }
        }
    }

    /// <summary>The xref this structure points to, without @ signs, or null. Setting a non-null value clears <see cref="Value"/>.</summary>
    public string? Pointer
    {
        get => pointerTarget;
        set
        {
            pointerTarget = NormalizeXref(value);
            if (pointerTarget is not null)
            {
                text = null;
            }
        }
    }

    public bool HasPointer => pointerTarget is not null;

    public bool IsVoidPointer => pointerTarget == VOID_POINTER;

    public bool HasValue => !string.IsNullOrEmpty(text);

    public GedcomStructureCollection Children { get; }

    public GedcomStructure? Parent { get; internal set; }

    /// <summary>Set while the structure is a record in a <see cref="GedcomFile"/>.</summary>
    internal GedcomFile? Owner { get; set; }

    /// <summary>The 1-based line this structure was read from, or 0 when it was created in code.</summary>
    public int LineNumber { get; internal set; }

    public int Level => Parent is null ? 0 : Parent.Level + 1;

    public GedcomStructure Root => Parent?.Root ?? this;

    /// <summary>The file the structure belongs to, found through its record.</summary>
    public GedcomFile? File => Root.Owner;

    /// <summary>Extension tags start with an underscore (<c>_PRIM</c>, <c>_UID</c>).</summary>
    public bool IsExtension => tagName[0] == '_';

    public bool HasChildren => Children.Count > 0;

    public GedcomStructure? Child(string tag)
    {
        foreach (var child in Children)
        {
            if (child.Tag == tag)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>The first child whose tag is any of <paramref name="tags"/>. Handy for vendor variants such as EMAIL/_EMAIL.</summary>
    public GedcomStructure? Child(params ReadOnlySpan<string> tags)
    {
        foreach (var child in Children)
        {
            if (tags.Contains(child.Tag))
            {
                return child;
            }
        }

        return null;
    }

    public IEnumerable<GedcomStructure> ChildrenWithTag(string tag) => Children.Where(child => child.Tag == tag);

    public IEnumerable<GedcomStructure> ChildrenWithTag(params string[] tags) => Children.Where(child => tags.Contains(child.Tag));

    public string? ChildValue(string tag) => Child(tag)?.Value;

    /// <summary>Follows a tag path from this structure, e.g. <c>Find("GEDC", "VERS")</c> on HEAD.</summary>
    public GedcomStructure? Find(params ReadOnlySpan<string> path)
    {
        var current = this;
        foreach (string tag in path)
        {
            current = current.Child(tag);
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    public GedcomStructure AddChild(string tag, string? value = null)
    {
        var child = new GedcomStructure(tag, value);
        Children.Add(child);
        return child;
    }

    public GedcomStructure AddPointer(string tag, string pointer)
    {
        var child = CreatePointer(tag, pointer);
        Children.Add(child);
        return child;
    }

    /// <summary>
    /// Sets the value of the first child with <paramref name="tag"/>, adding it when missing.
    /// A null or empty value removes the child, unless it has substructures of its own.
    /// </summary>
    public GedcomStructure? SetChildValue(string tag, string? value)
    {
        var child = Child(tag);
        if (string.IsNullOrEmpty(value))
        {
            if (child is not null)
            {
                if (child.HasChildren)
                {
                    child.Value = null;
                    return child;
                }

                Children.Remove(child);
            }

            return null;
        }

        if (child is null)
        {
            return AddChild(tag, value);
        }

        child.Value = value;
        return child;
    }

    public GedcomStructure? SetChildPointer(string tag, string? pointer)
    {
        var child = Child(tag);
        if (string.IsNullOrEmpty(pointer))
        {
            if (child is not null)
            {
                Children.Remove(child);
            }

            return null;
        }

        if (child is null)
        {
            return AddPointer(tag, pointer);
        }

        child.Pointer = pointer;
        return child;
    }

    /// <summary>Returns the first child with <paramref name="tag"/>, adding an empty one when missing.</summary>
    public GedcomStructure GetOrAddChild(string tag) => Child(tag) ?? AddChild(tag);

    public int RemoveChildren(string tag)
    {
        int removed = 0;
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i].Tag == tag)
            {
                Children.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Detaches this structure from its parent, or from its file when it is a record.</summary>
    public bool Remove()
    {
        if (Parent is not null)
        {
            return Parent.Children.Remove(this);
        }

        return Owner?.Records.Remove(this) ?? false;
    }

    /// <summary>All structures below this one, depth first, in file order.</summary>
    public IEnumerable<GedcomStructure> Descendants()
    {
        var stack = new Stack<GedcomStructure>();
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            stack.Push(Children[i]);
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            for (int i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    public IEnumerable<GedcomStructure> DescendantsAndSelf() => Descendants().Prepend(this);

    /// <summary>The tags from the record down to this structure, e.g. <c>INDI.BIRT.DATE</c>.</summary>
    public string Path => Parent is null ? tagName : $"{Parent.Path}.{tagName}";

    /// <summary>A detached deep copy. Line numbers are kept so issues on a converted copy still point at the source.</summary>
    public GedcomStructure Clone()
    {
        var copy = new GedcomStructure(tagName)
        {
            xrefId = xrefId,
            text = text,
            pointerTarget = pointerTarget,
            LineNumber = LineNumber
        };

        foreach (var child in Children)
        {
            copy.Children.Add(child.Clone());
        }

        return copy;
    }

    /// <summary>True when both trees hold the same tags, xrefs, payloads and children in the same order.</summary>
    public bool ContentEquals(GedcomStructure? other)
    {
        if (other is null
            || other.tagName != tagName
            || other.xrefId != xrefId
            || other.text != text
            || other.pointerTarget != pointerTarget
            || other.Children.Count != Children.Count)
        {
            return false;
        }

        for (int i = 0; i < Children.Count; i++)
        {
            if (!Children[i].ContentEquals(other.Children[i]))
            {
                return false;
            }
        }

        return true;
    }

    internal static string? NormalizeXref(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '@' && trimmed[^1] == '@')
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(Level).Append(' ');
        if (xrefId is not null)
        {
            builder.Append('@').Append(xrefId).Append("@ ");
        }

        builder.Append(tagName);
        if (pointerTarget is not null)
        {
            builder.Append(" @").Append(pointerTarget).Append('@');
        }
        else if (!string.IsNullOrEmpty(text))
        {
            builder.Append(' ').Append(text.Replace("\n", "\\n", StringComparison.Ordinal));
        }

        return builder.ToString();
    }

    private string DebuggerDisplay => $"{ToString()} ({Children.Count} children)";
}
