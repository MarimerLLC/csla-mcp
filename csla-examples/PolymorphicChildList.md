# Polymorphic Child List Pattern

This pattern shows how to create a `BusinessListBase` collection that safely holds
a **heterogeneous (polymorphic) set of editable child objects** — for example a
single `SurveyResultList` that contains a mix of `AssetSurveyResult` and
`ContactSurveyResult` items, where both derive from a common `SurveyResult` base.

CSLA supports this, but three framework-specific constraints must be satisfied or
the objects will fail to register their properties, fail to serialize, or fail to
compile. This document explains each constraint and shows a complete, working
implementation.

**Key Characteristics**:

* A list (`BusinessListBase<TList, TItem>`) whose item type `TItem` is a common
  **interface** shared by every concrete child type
* Concrete child types derive from an abstract base that uses the
  **curiously recurring generic pattern (CRTP)**: `Base<T> where T : Base<T>`
* Each concrete child is a normal editable child (`[FetchChild]`, `[InsertChild]`,
  `[UpdateChild]`, `[DeleteSelfChild]`)
* The list materializes the correct concrete type per row during fetch

**Common use cases**: A survey containing different kinds of survey results, a
document with different kinds of line items, an inbox containing different kinds
of messages — any collection where items share a base type but have distinct
properties and behavior.

## The Three Constraints (Read First)

These are the non-obvious rules that make or break the pattern. Each is explained
in detail with the exact runtime error it produces if violated.

1. **The shared base must be generic (CRTP).** A non-generic shared base such as
   `SurveyResult : BusinessBase<SurveyResult>` cannot back subclasses that add
   their own managed properties.
2. **The list's item type must be an interface that extends `IBusinessBase`.**
   Extending only `IEditableBusinessObject` compiles but fails to serialize.
3. **Managed properties must be hand-coded.** The `[CslaImplementProperties]`
   source generator does not generate properties for types in this hierarchy.

## Implementation Example

### 1. The common interface

The list is typed against this interface, so it can hold any concrete survey
result. It **must** extend `IBusinessBase` (see Constraint 2 below).

```csharp
using Csla;

namespace MyApp.Business;

public interface ISurveyResult : IBusinessBase
{
    int Id { get; }
    DateTime SurveyDate { get; set; }
    string Describe();
}
```

### 2. The CRTP abstract base

The base uses the curiously recurring generic pattern so that each concrete leaf
type closes the generic with itself. This gives every leaf its own property
registration identity (see Constraint 1 below). Properties here are hand-coded
(see Constraint 3 below).

```csharp
using Csla;

namespace MyApp.Business;

public abstract class SurveyResult<T> : BusinessBase<T>, ISurveyResult
    where T : SurveyResult<T>
{
    public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
    public int Id
    {
        get => GetProperty(IdProperty);
        protected set => SetProperty(IdProperty, value);
    }

    public static readonly PropertyInfo<DateTime> SurveyDateProperty = RegisterProperty<DateTime>(nameof(SurveyDate));
    public DateTime SurveyDate
    {
        get => GetProperty(SurveyDateProperty);
        set => SetProperty(SurveyDateProperty, value);
    }

    public abstract string Describe();

    // Helper so each leaf can load the shared properties from its data row.
    protected void LoadCommon(SurveyResultData data)
    {
        LoadProperty(IdProperty, data.Id);
        LoadProperty(SurveyDateProperty, data.SurveyDate);
    }
}
```

### 3. The concrete child types

Each leaf closes the generic with itself and adds its own properties and data
portal operations.

```csharp
using Csla;

namespace MyApp.Business;

public class AssetSurveyResult : SurveyResult<AssetSurveyResult>
{
    public static readonly PropertyInfo<string> AssetTagProperty = RegisterProperty<string>(nameof(AssetTag));
    public string AssetTag
    {
        get => GetProperty(AssetTagProperty)!;
        set => SetProperty(AssetTagProperty, value);
    }

    public static readonly PropertyInfo<string> ConditionProperty = RegisterProperty<string>(nameof(Condition));
    public string Condition
    {
        get => GetProperty(ConditionProperty)!;
        set => SetProperty(ConditionProperty, value);
    }

    public override string Describe() => $"Asset {AssetTag} ({Condition})";

    [FetchChild]
    private void FetchChild(SurveyResultData data)
    {
        LoadCommon(data);
        LoadProperty(AssetTagProperty, data.AssetTag);
        LoadProperty(ConditionProperty, data.Condition);
        BusinessRules.CheckRules();
    }

    [InsertChild]
    private async Task InsertChild([Inject] ISurveyResultDal dal)
    {
        await dal.InsertAssetAsync(ReadProperty(IdProperty), ReadProperty(AssetTagProperty), ReadProperty(ConditionProperty));
    }

    [UpdateChild]
    private async Task UpdateChild([Inject] ISurveyResultDal dal)
    {
        await dal.UpdateAssetAsync(ReadProperty(IdProperty), ReadProperty(AssetTagProperty), ReadProperty(ConditionProperty));
    }

    [DeleteSelfChild]
    private async Task DeleteSelfChild([Inject] ISurveyResultDal dal)
    {
        await dal.DeleteAsync(ReadProperty(IdProperty));
    }
}
```

```csharp
using Csla;

namespace MyApp.Business;

public class ContactSurveyResult : SurveyResult<ContactSurveyResult>
{
    public static readonly PropertyInfo<string> ContactNameProperty = RegisterProperty<string>(nameof(ContactName));
    public string ContactName
    {
        get => GetProperty(ContactNameProperty)!;
        set => SetProperty(ContactNameProperty, value);
    }

    public static readonly PropertyInfo<string> ContactEmailProperty = RegisterProperty<string>(nameof(ContactEmail));
    public string ContactEmail
    {
        get => GetProperty(ContactEmailProperty)!;
        set => SetProperty(ContactEmailProperty, value);
    }

    public override string Describe() => $"Contact {ContactName} <{ContactEmail}>";

    [FetchChild]
    private void FetchChild(SurveyResultData data)
    {
        LoadCommon(data);
        LoadProperty(ContactNameProperty, data.ContactName);
        LoadProperty(ContactEmailProperty, data.ContactEmail);
        BusinessRules.CheckRules();
    }

    [InsertChild]
    private async Task InsertChild([Inject] ISurveyResultDal dal)
    {
        await dal.InsertContactAsync(ReadProperty(IdProperty), ReadProperty(ContactNameProperty), ReadProperty(ContactEmailProperty));
    }

    [UpdateChild]
    private async Task UpdateChild([Inject] ISurveyResultDal dal)
    {
        await dal.UpdateContactAsync(ReadProperty(IdProperty), ReadProperty(ContactNameProperty), ReadProperty(ContactEmailProperty));
    }

    [DeleteSelfChild]
    private async Task DeleteSelfChild([Inject] ISurveyResultDal dal)
    {
        await dal.DeleteAsync(ReadProperty(IdProperty));
    }
}
```

### 4. The polymorphic root list

The list's item type is the `ISurveyResult` interface. During fetch it inspects a
discriminator on each data row and uses the matching `IChildDataPortal<TLeaf>` to
materialize the correct concrete type, adding each item to the list as the shared
interface.

```csharp
using Csla;

namespace MyApp.Business;

public class SurveyResultList : BusinessListBase<SurveyResultList, ISurveyResult>
{
    [Fetch]
    private async Task Fetch(
        [Inject] ISurveyResultDal dal,
        [Inject] IChildDataPortal<AssetSurveyResult> assetPortal,
        [Inject] IChildDataPortal<ContactSurveyResult> contactPortal)
    {
        // ONE database call to get all rows (avoids the N+1 query problem).
        var rows = await dal.GetAllAsync();

        using (LoadListMode)
        {
            foreach (var row in rows)
            {
                // The 'Kind' discriminator decides which concrete type to create.
                ISurveyResult child = row.Kind switch
                {
                    "Asset" => await assetPortal.FetchChildAsync(row),
                    "Contact" => await contactPortal.FetchChildAsync(row),
                    _ => throw new InvalidOperationException($"Unknown kind '{row.Kind}'")
                };
                Add(child);
            }
        }
    }

    [Update]
    private void Update()
    {
        // BusinessListBase has no FieldManager; Child_Update drives the
        // [InsertChild]/[UpdateChild]/[DeleteSelfChild] operations on every item,
        // including async ones.
        Child_Update();
    }
}
```

The flat data row used by the data access layer carries a `Kind` discriminator
plus the union of all child fields:

```csharp
public class SurveyResultData
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";   // "Asset" or "Contact"
    public DateTime SurveyDate { get; set; }

    // Asset-specific
    public string AssetTag { get; set; } = "";
    public string Condition { get; set; } = "";

    // Contact-specific
    public string ContactName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
}
```

## Constraint 1: The shared base must be generic (CRTP)

It is tempting to write a single non-generic base class:

```csharp
// DOES NOT WORK when subclasses add managed properties
public abstract class SurveyResult : BusinessBase<SurveyResult> { ... }
public class AssetSurveyResult : SurveyResult { ... }    // adds AssetTag, Condition
public class ContactSurveyResult : SurveyResult { ... }  // adds ContactName, ContactEmail
```

CSLA registers each managed property against a **containing type**, and for a
class deriving from `BusinessBase<T>` that containing type is `T`. With the
non-generic base above, `T` is `SurveyResult` for *every* subclass, so all leaf
properties are registered against the single `SurveyResult` type. A type's
property list is frozen the first time an instance of it is created, so as soon as
one leaf is instantiated the next leaf can no longer register its properties:

```text
Cannot register property ContactName after containing type (SurveyResult) has been instantiated
```

The CRTP base solves this. Because `AssetSurveyResult` derives from
`SurveyResult<AssetSurveyResult>` and `ContactSurveyResult` derives from
`SurveyResult<ContactSurveyResult>`, each closed generic is a distinct containing
type with its own independent property list. There is no collision.

> A non-generic shared base is only safe if the subclasses add **no** managed
> properties of their own (behavior-only specialization). The moment a subclass
> registers a property, you need the CRTP base.

## Constraint 2: The list item type must extend `IBusinessBase`

Because the CRTP leaves have *different* base types
(`SurveyResult<AssetSurveyResult>` vs `SurveyResult<ContactSurveyResult>`), the
only type they share is the `ISurveyResult` interface — so that interface is the
list's item type.

`BusinessListBase<T, C>` only constrains `C` to `IEditableBusinessObject`, so this
compiles even if `ISurveyResult` extends only `IEditableBusinessObject`. However,
`MobileFormatter` serializes a collection by checking the collection's **item type
parameter** (not the runtime type of each item) against `IMobileObject`. If the
declared item type is not an `IMobileObject`, serialization throws:

```text
Cannot serialize collections not of type IMobileObject
```

`IEditableBusinessObject` does **not** extend `IMobileObject`, but `IBusinessBase`
does. Declaring `public interface ISurveyResult : IBusinessBase` therefore makes
the list serialize correctly through the data portal and `Clone()`. As a bonus,
`IBusinessBase` exposes `Id`-style members, `IsDirty`, `IsValid`, etc. on the
interface.

## Constraint 3: Properties must be hand-coded

The CSLA `[CslaImplementProperties]` source generator (from the
`Csla.Generator.AutoImplementProperties.CSharp` package) only generates property
implementations for a class whose **direct** base is a framework base class such
as `BusinessBase<T>`. For any type in this pattern it emits an empty partial and
the `partial` properties never get an implementation:

```text
error CS9248: Partial property 'AssetSurveyResult.AssetTag' must have an implementation part.
```

This happens both for the generic CRTP base and for the concrete leaf types
(whose direct base is the user-defined `SurveyResult<T>` rather than
`BusinessBase<T>` directly). Do **not** apply `[CslaImplementProperties]` to any
type in a polymorphic hierarchy. Implement the managed properties by hand with
`RegisterProperty` plus `GetProperty`/`SetProperty`, as shown above. (Use the
null-forgiving `GetProperty(...)!` for reference-type getters to match the
nullable behavior the generator would have produced.)

## Using the Polymorphic List

```csharp
// Inject IDataPortal<SurveyResultList> via dependency injection.
var list = await listPortal.FetchAsync();

foreach (var item in list)
{
    // item is typed as ISurveyResult but is really AssetSurveyResult,
    // ContactSurveyResult, etc.
    Console.WriteLine($"{item.GetType().Name}: {item.Describe()}");

    if (item is AssetSurveyResult asset)
        asset.Condition = "Excellent";
}

// Add a new polymorphic child through its own child data portal.
var newContact = await contactPortal.CreateChildAsync();
newContact.ContactName = "Jane Doe";
list.Add(newContact);

// Save the whole list; Child_Update drives each item's child operations.
list = await list.SaveAsync();
```

Serialization (including `Clone()` and any remote data portal round trip)
preserves the concrete runtime type of every item, because `MobileFormatter`
serializes each child by its actual type.

## Key Points

* Use a **CRTP abstract base** (`Base<T> where T : Base<T>`) so each concrete child
  gets its own property-registration identity. A shared non-generic base fails the
  moment a subclass adds a managed property.
* Type the list against a **common interface that extends `IBusinessBase`**, not
  just `IEditableBusinessObject`, or `MobileFormatter` refuses to serialize the
  collection.
* **Hand-code managed properties** — `[CslaImplementProperties]` does not work for
  these types.
* In the list's `[Fetch]`, read all rows in one query and use a per-type
  `IChildDataPortal<TLeaf>` selected by a discriminator to materialize each item;
  add it to the list as the interface.
* The list's `[Update]` calls **`Child_Update()`** (BusinessListBase has no
  `FieldManager`); it drives synchronous and asynchronous child operations alike.
* See `EditableRootList.md` and `EditableChild.md` for the non-polymorphic
  baseline of these stereotypes.
