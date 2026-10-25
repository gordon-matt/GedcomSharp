using System.Collections.ObjectModel;

namespace GedcomSharp;

/// <summary>The substructures of a <see cref="GedcomStructure"/>. Keeps <see cref="GedcomStructure.Parent"/> in step.</summary>
public sealed class GedcomStructureCollection : Collection<GedcomStructure>
{
    private readonly GedcomStructure owner;

    internal GedcomStructureCollection(GedcomStructure parent) => owner = parent;

    public void AddRange(IEnumerable<GedcomStructure> structures)
    {
        foreach (var structure in structures)
        {
            Add(structure);
        }
    }

    protected override void InsertItem(int index, GedcomStructure item)
    {
        Attach(item);
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, GedcomStructure item)
    {
        var previous = this[index];
        if (ReferenceEquals(previous, item))
        {
            return;
        }

        Attach(item);
        previous.Parent = null;
        base.SetItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        this[index].Parent = null;
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        foreach (var item in this)
        {
            item.Parent = null;
        }

        base.ClearItems();
    }

    private void Attach(GedcomStructure item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Parent is not null || item.Owner is not null)
        {
            throw new InvalidOperationException($"The {item.Tag} structure already belongs to another structure or file. Remove it first, or add a Clone().");
        }

        for (var ancestor = owner; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, item))
            {
                throw new InvalidOperationException("A structure cannot be added below itself.");
            }
        }

        item.Parent = owner;
    }
}

/// <summary>
/// The records of a <see cref="GedcomFile"/> in file order, excluding HEAD and TRLR.
/// Changes keep the file's xref index current.
/// </summary>
public sealed class GedcomRecordCollection : Collection<GedcomStructure>
{
    private readonly GedcomFile owner;

    internal GedcomRecordCollection(GedcomFile file) => owner = file;

    public void AddRange(IEnumerable<GedcomStructure> records)
    {
        foreach (var record in records)
        {
            Add(record);
        }
    }

    protected override void InsertItem(int index, GedcomStructure item)
    {
        Attach(item);
        base.InsertItem(index, item);
        owner.OnXrefChanged();
    }

    protected override void SetItem(int index, GedcomStructure item)
    {
        var previous = this[index];
        if (ReferenceEquals(previous, item))
        {
            return;
        }

        Attach(item);
        previous.Owner = null;
        base.SetItem(index, item);
        owner.OnXrefChanged();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Owner = null;
        base.RemoveItem(index);
        owner.OnXrefChanged();
    }

    protected override void ClearItems()
    {
        foreach (var item in this)
        {
            item.Owner = null;
        }

        base.ClearItems();
        owner.OnXrefChanged();
    }

    private void Attach(GedcomStructure item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Parent is not null || item.Owner is not null)
        {
            throw new InvalidOperationException($"The {item.Tag} record already belongs to a structure or file. Remove it first, or add a Clone().");
        }

        if (item.Tag is GedcomTags.HEAD or GedcomTags.TRLR)
        {
            throw new InvalidOperationException($"{item.Tag} is not a record. Use GedcomFile.Header for the header; the trailer is written automatically.");
        }

        item.Owner = owner;
    }
}
