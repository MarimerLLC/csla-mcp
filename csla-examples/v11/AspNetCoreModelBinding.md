# ASP.NET Core Model Binding (CslaModelBinder)

This document applies to CSLA 11. It describes how `CslaModelBinder` binds posted form values to editable CSLA business objects in ASP.NET Core MVC and Razor Pages, including child objects and child lists at any depth.

## Summary for code generation

* `AddAspNetCore()` registers `CslaModelBinder` for editable business objects and editable lists. No other setup.
* The binder binds the **whole object graph**: root properties, editable child objects and editable child lists, nested to any depth (`Children[0].GrandChildren[1].Name`).
* **Creating** an object: take it as an action parameter. Every object the binder creates is new (`IsNew == true`).
* **Editing** an object: fetch it, then call `TryUpdateModelAsync(obj)`. The binder updates that object in place and sets only the posted values.
* The binder honors `[BindNever]`, `[Bind]` and the `TryUpdateModelAsync` include list.
* The binder waits for async business rules anywhere in the graph before it returns, so `IsValid`/`IsSavable` are final in your action.
* Posting a list never removes items. Remove items in controller code.

## Setup

```csharp
builder.Services.AddControllersWithViews();   // or AddRazorPages()
builder.Services.AddCsla(o => o.AddAspNetCore());
```

## Render the graph with indexed field names

Use loops with `for` (not `foreach`) so the tag helpers generate indexed names:

```html
@model OrderEdit

<form asp-action="Edit" asp-route-id="@Model.Id" method="post">
  <input asp-for="CustomerName" />
  <input asp-for="ShippingAddress.Street" />
  <input asp-for="ShippingAddress.City" />

  @for (var i = 0; i < Model.LineItems.Count; i++)
  {
    <input asp-for="LineItems[i].ProductId" />
    <input asp-for="LineItems[i].Quantity" />
    @for (var j = 0; j < Model.LineItems[i].Discounts.Count; j++)
    {
      <input asp-for="LineItems[i].Discounts[j].Percent" />
    }
  }
  <button type="submit">Save</button>
</form>
```

## Create: bind to an action parameter

```csharp
[HttpPost]
public async Task<IActionResult> Create(OrderEdit order)
{
    if (!order.IsValid)
        return View(order);

    order = await order.SaveAsync();
    return RedirectToAction(nameof(Details), new { id = order.Id });
}
```

All objects in a graph bound this way are new, including children, so they save as inserts. Don't use this for editing existing data.

## Edit: fetch, then `TryUpdateModelAsync`

```csharp
public class OrderController(IDataPortal<OrderEdit> orderPortal) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Edit(int id)
    {
        var order = await orderPortal.FetchAsync(id);
        if (await TryUpdateModelAsync(order) && order.IsValid)
        {
            order = await order.SaveAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(order);
    }
}
```

When binding into an existing object, the binder:

* updates the existing root, children and list items in place, so they keep their state and save as updates;
* sets only values that were posted; properties and children with no posted fields are left alone (not set to `null`);
* adds a new child item for a posted list index past the end of the list (unless the list's `AllowNew` is `false`, in which case extra items are ignored).

Razor Pages works the same way:

```csharp
public class EditModel(IDataPortal<OrderEdit> orderPortal) : PageModel
{
    public OrderEdit Order { get; set; } = default!;

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Order = await orderPortal.FetchAsync(id);
        if (!await TryUpdateModelAsync(Order, nameof(Order)) || !Order.IsValid)
            return Page();

        Order = await Order.SaveAsync();
        return RedirectToPage("Index");
    }
}
```

## Limiting what is bound

```csharp
// Only bind CustomerName and LineItems (an included child list or child object is bound as a whole)
await TryUpdateModelAsync(order, "", o => o.CustomerName, o => o.LineItems);
```

```csharp
[HttpPost]
public async Task<IActionResult> Create([Bind("CustomerName,LineItems")] OrderEdit order)
```

```csharp
public partial class OrderEdit : BusinessBase<OrderEdit>
{
    public static readonly PropertyInfo<decimal> CreditLimitProperty = RegisterProperty<decimal>(nameof(CreditLimit));
    [BindNever]   // never bound from a post, even if the field is present
    public decimal CreditLimit
    {
        get => GetProperty(CreditLimitProperty);
        set => SetProperty(CreditLimitProperty, value);
    }
}
```

`[Bind]` on a child's class applies to that child's properties.

## Limits

* New list items and child objects are created with the property's or list's declared type. Polymorphic children bind correctly only when they already exist (the `TryUpdateModelAsync` approach).
* Posting a list never removes items.
* Lists whose `AllowNew` is `false` ignore posted items beyond their existing ones.
* Read-only child objects aren't bound.
* A request that isn't a form post binds to a new object with no values set.
* ASP.NET Core MVC and Razor Pages don't support trimming, so the binder isn't trim-annotated.

## Changes from CSLA 10

| CSLA 10 | CSLA 11 |
|---|---|
| Bound only the root's properties (plus the items of a root list) | Binds child objects and child lists at any depth |
| `TryUpdateModelAsync(model)` bound into a separate new object; your object didn't get the values | Binds into the object you pass |
| Ignored `[BindNever]`, `[Bind]` and include lists | Honors them |
| Didn't wait for async rules | Waits for async rules in the whole graph |
| Set unposted child properties to `null` | Leaves them alone |
| List item count = posted fields / property count (wrong with child properties) | Uses the indexes in the field names |
| Non-form request threw | Binds to a new object |

## Related

* `AspNetCoreConfiguration.md` - ASP.NET Core setup
* `EditableRoot.md`, `EditableChild.md`, `EditableChildList.md` - the stereotypes being bound
* `UpgradingToCsla11.md` - all CSLA 10 to 11 changes
