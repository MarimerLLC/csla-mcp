# Implementing CSLA Properties

In CSLA 10, properties are defined using the `partial` keyword in combination with the `[CslaImplementProperties]` attribute on the containing class. The code generator automatically implements the property backing fields and accessor methods.

> **Note:** You can still declare CSLA properties using the CSLA 9 explicit style, but CSLA 10 code generation is the recommended approach.

## Property Declaration Syntax

### Class with Generated Properties

```csharp
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class Customer : BusinessBase<Customer>
    {
        // Properties are declared as partial
        public partial int Id { get; private set; }
        public partial string Name { get; set; }
    }
}
```

**What CSLA Generates**:

For each partial property, CSLA generates:

* A static `PropertyInfo<T>` field (e.g., `NameProperty`)
* Property getter/setter implementation using `GetProperty` and `SetProperty`
* Automatic participation in rules engine, change tracking, and serialization

## Types of Property

Properties may be read-write, read-only, private, or ignored by CSLA.

### Read-Write Property

Used in editable objects where users can modify the value.

```csharp
public partial string Name { get; set; }
```

**Use in**: `BusinessBase<T>` (editable root and editable child)

### Read-Only Property

Has public getter with private setter. Used for properties set only by data portal operations or business logic.

```csharp
public partial int Id { get; private set; }
public partial DateTime CreatedDate { get; private set; }
```

**Use in**:

* `BusinessBase<T>` (for ID, timestamps, calculated fields)
* `ReadOnlyBase<T>` (all properties are read-only)
* `CommandBase<T>` (output/result properties)

### Private Property

Used for internal state management. Value is available to rules engine and serialized, but not exposed publicly.

```csharp
private partial DateTime LastModified { get; set; }
private partial string InternalToken { get; set; }
```

**Use cases**:

* Tracking internal state
* Temporary values needed by business rules
* Values that should serialize but not be public API

### CSLA Ignored Property

Excluded from CSLA processing - no rules engine, no serialization, no change tracking.

```csharp
[CslaIgnoreProperty]
public string DisplayText { get; set; }

[CslaIgnoreProperty]
public DateTime LocalCalculation => CreatedDate.ToLocalTime();
```

**Use cases**:

* UI-only calculated properties
* Properties managed manually
* Temporary display values

**Important**: Ignored properties are NOT partial - you must provide the full implementation.

## Data Annotations

CSLA automatically converts `System.ComponentModel.DataAnnotations` attributes into business rules.

### Built-In Annotations

All of these are available in the `System.ComponentModel.DataAnnotations` namespace:

```csharp
[Display(Name = "Full Name")]          // Friendly name (supports localization)
[Required]                              // Property must have a value
[StringLength(50)]                      // Maximum string length
[StringLength(50, MinimumLength = 2)]   // Min and max length
[Range(1, 100)]                         // Numeric range
[EmailAddress]                          // Email format validation
[Phone]                                 // Phone format validation
[Url]                                   // URL format validation
[RegularExpression(@"pattern")]         // Custom regex pattern
[CreditCard]                            // Credit card number validation
[Compare("OtherProperty")]              // Compare two property values
[MaxLength(100)]                        // Maximum array/string length
[MinLength(2)]                          // Minimum array/string length
[FileExtensions]                        // Valid file extensions
```

### Example with Multiple Annotations

```csharp
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    public partial int Id { get; private set; }
    
    [Display(Name = "Full Name")]
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public partial string Name { get; set; }
    
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public partial string Email { get; set; }
    
    [Range(18, 120)]
    public partial int Age { get; set; }
    
    [Phone]
    public partial string PhoneNumber { get; set; }
    
    public partial DateTime CreatedDate { get; private set; }
}
```

## Property Backing Fields

CSLA generates a static `PropertyInfo<T>` field for each property with the suffix "Property".

```csharp
public partial string Name { get; set; }
// Generates: public static readonly PropertyInfo<string> NameProperty
```

**Usage in code**:

```csharp
// In data portal operations
LoadProperty(NameProperty, data.Name);          // Set without triggering rules
var name = ReadProperty(NameProperty);          // Read internal value

// In business rules
BusinessRules.AddRule(new Required(NameProperty));
BusinessRules.AddRule(new MaxLength(NameProperty, 50));
```

## Calculated Properties

Calculated properties should NOT be partial. Implement them as standard properties.

```csharp
[CslaImplementProperties]
public partial class OrderItem : BusinessBase<OrderItem>
{
    public partial int Quantity { get; set; }
    public partial decimal Price { get; set; }
    
    // Calculated property - not partial
    public decimal Total => Quantity * Price;
    
    // Calculated property with logic
    public string Status
    {
        get
        {
            if (Quantity <= 0) return "Invalid";
            if (Quantity > 100) return "Bulk Order";
            return "Standard";
        }
    }
}
```

**Note**: Calculated properties are not serialized and are recalculated each time they're accessed.

## Property Access Patterns

### In Data Portal Operations

Use `LoadProperty` and `ReadProperty`:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal dal)
{
    var data = await dal.GetAsync(id);
    
    // Load without triggering validation or change tracking
    LoadProperty(IdProperty, data.Id);
    LoadProperty(NameProperty, data.Name);
    
    await BusinessRules.CheckRulesAsync();
}

[Insert]
private async Task Insert([Inject] ICustomerDal dal)
{
    // Read values to send to DAL
    var data = new CustomerData
    {
        Name = ReadProperty(NameProperty),
        Email = ReadProperty(EmailProperty)
    };
    
    var result = await dal.InsertAsync(data);
    LoadProperty(IdProperty, result.Id);
}
```

**Alternative: Using `BypassPropertyChecks`**

You can also use normal property syntax with `BypassPropertyChecks` to bypass authorization and validation:

```csharp
[Fetch]
private async Task Fetch(int id, [Inject] ICustomerDal dal)
{
    var data = await dal.GetAsync(id);
    
    using (BypassPropertyChecks)
    {
        // Normal property syntax, but bypasses authorization and validation
        Id = data.Id;
        Name = data.Name;
        Email = data.Email;
        CreatedDate = data.CreatedDate;
    }
    
    await BusinessRules.CheckRulesAsync();
}

[Update]
private async Task Update([Inject] ICustomerDal dal)
{
    using (BypassPropertyChecks)
    {
        // Read properties even if user doesn't have read authorization
        var data = new CustomerData
        {
            Id = Id,
            Name = Name,
            Email = Email
        };
        
        var result = await dal.UpdateAsync(data);
        ModifiedDate = result.ModifiedDate;
        ModifiedBy = result.ModifiedBy;
    }
}
```

**When to use each approach:**

* **`LoadProperty`/`ReadProperty`** - Explicit control, clearer intent, bypasses authorization and validation
* **`BypassPropertyChecks`** - More natural property syntax, useful when setting many properties at once
* Both approaches bypass authorization rules and don't trigger change tracking or validation

### In Business Logic

Use normal property syntax:

```csharp
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    public partial string FirstName { get; set; }
    public partial string LastName { get; set; }
    
    public void UpdateName(string first, string last)
    {
        // Normal property syntax in business methods
        FirstName = first;
        LastName = last;
    }
}
```

## Commands and Read-Only Objects

### Command Properties

Commands use `LoadProperty` to set output values:

```csharp
[CslaImplementProperties]
public partial class CheckCustomerExists : CommandBase<CheckCustomerExists>
{
    public partial bool Exists { get; private set; }
    
    [Execute]
    private async Task Execute(string email, [Inject] ICustomerDal dal)
    {
        var customer = await dal.GetByEmailAsync(email);
        LoadProperty(ExistsProperty, customer != null);
    }
}
```

### Read-Only Object Properties

All properties in read-only objects have private setters:

```csharp
[CslaImplementProperties]
public partial class CustomerInfo : ReadOnlyBase<CustomerInfo>
{
    public partial int Id { get; private set; }
    public partial string Name { get; private set; }
    public partial string Email { get; private set; }
    
    [FetchChild]
    private void FetchChild(CustomerData data)
    {
        LoadProperty(IdProperty, data.Id);
        LoadProperty(NameProperty, data.Name);
        LoadProperty(EmailProperty, data.Email);
    }
}
```

## Best Practices

1. **Use partial properties** for all CSLA-managed properties in CSLA 10
2. **Use data annotations** for common validation rules instead of manual rules
3. **Use read-only properties** (private set) for IDs, timestamps, and system-managed values
4. **Use calculated properties** (non-partial) for derived values
5. **Use LoadProperty/ReadProperty** in data portal operations
6. **Use normal property syntax** in business logic and UI code
7. **Group related properties** with blank lines for readability

## Common Patterns

### Entity with ID and Audit Fields

```csharp
[CslaImplementProperties]
public partial class Customer : BusinessBase<Customer>
{
    // Primary key - read-only
    public partial int Id { get; private set; }
    
    // User-editable properties
    [Required, StringLength(100)]
    public partial string Name { get; set; }
    
    [Required, EmailAddress]
    public partial string Email { get; set; }
    
    // Audit fields - read-only
    public partial DateTime CreatedDate { get; private set; }
    public partial string CreatedBy { get; private set; }
    public partial DateTime? ModifiedDate { get; private set; }
    public partial string ModifiedBy { get; private set; }
}
```

### Parent with Child Reference

```csharp
[CslaImplementProperties]
public partial class Order : BusinessBase<Order>
{
    public partial int Id { get; private set; }
    public partial DateTime OrderDate { get; set; }
    
    // Child object property - read-only, populated by data portal
    public partial OrderItemList Items { get; private set; }
}
```
