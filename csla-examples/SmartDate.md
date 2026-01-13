# SmartDate

The `SmartDate` type is a specialized data type that provides seamless translation between string and `DateTime` values. It's particularly useful for data binding scenarios where you need to connect date fields to text input controls in the UI while maintaining strong typing and validation.

## Overview

`SmartDate` is a struct that wraps a `DateTime` value and provides intelligent string parsing and formatting capabilities. It understands the concept of an "empty date" value, supports keystroke shortcuts for common date entries, and automatically handles conversions between text and date representations.

## Key Features

- **String to DateTime conversion**: Automatically parses text input into valid dates
- **Empty date handling**: Treats empty/null values as valid dates (either `DateTime.MinValue` or `DateTime.MaxValue`)
- **Keystroke shortcuts**: Provides convenient shortcuts for common date entries
- **Format string support**: Configurable date format strings for display
- **Database integration**: Proper handling of `DBNull` for database operations
- **Type safety**: Maintains strong typing while allowing string-based data binding

## Use Cases

The primary use case is in business classes where you want to:
1. Expose a `string` property for UI data binding
2. Store the value internally in a `SmartDate` field
3. Let `SmartDate` handle all conversion logic automatically

This pattern allows text input controls to bind directly to the property while the field manages validation and conversion.

## Defining a SmartDate Property

Here's the recommended pattern for defining a property that uses `SmartDate`:

```csharp
public static readonly PropertyInfo<SmartDate> BirthDateProperty = 
    RegisterProperty<SmartDate>(nameof(BirthDate), null, new SmartDate());

public string BirthDate
{
    get => GetPropertyConvert<SmartDate, string>(BirthDateProperty);
    set => SetPropertyConvert<SmartDate, string>(BirthDateProperty, value);
}
```

In this pattern:
- The backing field is typed as `SmartDate`
- The public property is typed as `string`
- CSLA's property management handles the conversion automatically
- The UI can bind to a text input that users can type into directly

## Creating SmartDate Instances

### Basic Constructors

```csharp
// Create with current date
var date1 = new SmartDate(DateTime.Now);

// Create from string
var date2 = new SmartDate("12/25/2025");

// Create empty date (defaults to MinValue)
var date3 = new SmartDate();

// Create from nullable DateTime
DateTime? nullableDate = GetDateFromDatabase();
var date4 = new SmartDate(nullableDate);
```

### Empty Date Behavior

SmartDate has a special concept of "empty dates". You can configure whether an empty date represents the minimum or maximum possible date:

```csharp
// Empty date is MinValue (default)
var date1 = new SmartDate(true);

// Empty date is MaxValue
var date2 = new SmartDate(false);

// Using EmptyValue enum
var date3 = new SmartDate(SmartDate.EmptyValue.MinDate);
var date4 = new SmartDate(SmartDate.EmptyValue.MaxDate);
```

This affects comparison operations and is important when sorting or filtering dates.

## Keystroke Shortcuts

SmartDate recognizes several convenient text shortcuts for common date entries:

| Shortcut | Meaning | Result |
|----------|---------|--------|
| `t` or `today` or `.` | Today | Current date |
| `y` or `yesterday` or `-` | Yesterday | Current date minus 1 day |
| `tom` or `tomorrow` or `+` | Tomorrow | Current date plus 1 day |

These shortcuts are case-insensitive. For example:

```csharp
var today = new SmartDate("t");      // Today's date
var yesterday = new SmartDate("-");  // Yesterday's date
var tomorrow = new SmartDate("+");   // Tomorrow's date
```

## Working with Text

### Format Strings

You can customize how dates are formatted when converted to strings:

```csharp
var date = new SmartDate(DateTime.Now);

// Set format for this instance
date.FormatString = "MM/dd/yyyy";
string formatted = date.Text; // e.g., "01/12/2026"

// Set global default format for all new SmartDates
SmartDate.SetDefaultFormatString("yyyy-MM-dd");
```

The default format string is `"d"` (short date pattern) unless changed globally.

### Getting and Setting Text

The `Text` property provides string access to the date value:

```csharp
var date = new SmartDate();

// Set date from string
date.Text = "12/25/2025";

// Get date as string
string dateString = date.Text;

// Empty dates return empty strings
var emptyDate = new SmartDate();
Console.WriteLine(emptyDate.Text); // ""
```

### Custom Parsing

You can provide a custom parser function for specialized date formats:

```csharp
SmartDate.CustomParser = (input) =>
{
    // Custom parsing logic
    if (input == "fiscal-year-end")
        return new DateTime(2025, 6, 30);
    
    // Return null to let SmartDate try default parsing
    return null;
};
```

The custom parser is called first, and if it returns `null`, SmartDate falls back to its standard parsing logic.

## Working with DateTime

### Date Property

Access the underlying `DateTime` value directly:

```csharp
var smartDate = new SmartDate("12/25/2025");
DateTime actualDate = smartDate.Date;

// Modify the date
smartDate.Date = DateTime.Now;
```

### Conversion Methods

```csharp
var smartDate = new SmartDate(DateTime.Now);

// Convert to DateTimeOffset
DateTimeOffset offset = smartDate.ToDateTimeOffset();

// Convert to nullable DateTime
DateTime? nullable = smartDate.ToNullableDate();

// Empty dates convert to null
var emptyDate = new SmartDate();
DateTime? result = emptyDate.ToNullableDate(); // null
```

## Database Integration

### DBValue Property

The `DBValue` property is specifically designed for database operations:

```csharp
var date = new SmartDate();

// Empty dates return DBNull.Value
object dbValue = date.DBValue; // DBNull.Value

// Non-empty dates return the DateTime
date.Date = DateTime.Now;
dbValue = date.DBValue; // DateTime value
```

This is particularly useful when setting SQL command parameters:

```csharp
using (var ctx = ConnectionManager.GetManager())
{
    using (var cmd = ctx.Connection.CreateCommand())
    {
        cmd.CommandText = "INSERT INTO Orders (OrderDate) VALUES (@OrderDate)";
        cmd.Parameters.AddWithValue("@OrderDate", obj.OrderDate.DBValue);
        cmd.ExecuteNonQuery();
    }
}
```

### Reading from Database with SafeDataReader

When using `SafeDataReader`, you can read nullable dates directly into SmartDate:

```csharp
using (var dr = new SafeDataReader(cmd.ExecuteReader()))
{
    if (dr.Read())
    {
        // GetSmartDate handles null values automatically
        obj.OrderDate = dr.GetSmartDate("OrderDate");
    }
}
```

See [SafeDataReader](SafeDataReader.md) for more information on reading data.

## Empty Date Handling

### Checking for Empty Dates

```csharp
var date = new SmartDate();

if (date.IsEmpty)
{
    Console.WriteLine("Date is empty");
}

// Check empty date configuration
bool emptyIsMin = date.EmptyIsMin; // true if empty = MinValue
```

### Why Empty Dates Matter

Empty dates are important for:
1. **Comparison operations**: Empty dates can be meaningfully compared with real dates
2. **Sorting**: Empty dates sort to the beginning or end of a list
3. **Optional fields**: Represent fields where a date hasn't been provided yet

```csharp
// Comparison with empty dates
var emptyDate = new SmartDate(SmartDate.EmptyValue.MinDate);
var realDate = new SmartDate("12/25/2025");

if (emptyDate.CompareTo(realDate) < 0)
{
    Console.WriteLine("Empty date is less than real date");
}
```

## Parsing and Conversion

### Static Parsing Methods

```csharp
// Parse with default empty value (MinDate)
SmartDate date1 = SmartDate.Parse("12/25/2025");

// Parse with explicit empty value
SmartDate date2 = SmartDate.Parse("12/25/2025", SmartDate.EmptyValue.MaxDate);

// Try parse with error handling
SmartDate result = new SmartDate();
if (SmartDate.TryParse("12/25/2025", ref result))
{
    Console.WriteLine("Parse succeeded: " + result.Text);
}
else
{
    Console.WriteLine("Parse failed");
}
```

### String to DateTime Conversion

```csharp
// Convert string to DateTime
DateTime date1 = SmartDate.StringToDate("12/25/2025");

// Empty string returns MinValue or MaxValue
DateTime emptyDate = SmartDate.StringToDate("", true); // MinValue
DateTime maxDate = SmartDate.StringToDate("", false);  // MaxValue

// Using shortcuts
DateTime today = SmartDate.StringToDate("t");
DateTime yesterday = SmartDate.StringToDate("-");
```

### DateTime to String Conversion

```csharp
DateTime date = DateTime.Now;

// Convert with format string
string formatted = SmartDate.DateToString(date, "yyyy-MM-dd");

// Empty dates return empty string
string empty = SmartDate.DateToString(DateTime.MinValue, "d", true);
Console.WriteLine(empty); // ""
```

## Comparison and Manipulation

### Comparison Methods

```csharp
var date1 = new SmartDate("12/25/2025");
var date2 = new SmartDate("12/26/2025");

// Compare two SmartDates
int result = date1.CompareTo(date2); // -1 (date1 is earlier)

// Compare with DateTime
result = date1.CompareTo(DateTime.Now);

// Compare with string
result = date1.CompareTo("12/27/2025");
```

### Arithmetic Operations

```csharp
var date = new SmartDate("12/25/2025");

// Add TimeSpan
DateTime newDate = date.Add(TimeSpan.FromDays(7)); // 1/1/2026

// Subtract TimeSpan
DateTime earlier = date.Subtract(TimeSpan.FromDays(7)); // 12/18/2025

// Note: Empty dates return themselves unchanged
var emptyDate = new SmartDate();
DateTime result = emptyDate.Add(TimeSpan.FromDays(1)); // Still MinValue
```

## Operators

SmartDate supports various operators for convenience:

```csharp
var date1 = new SmartDate("12/25/2025");
var date2 = new SmartDate("12/26/2025");

// Equality
bool equal = (date1 == date2); // false
bool notEqual = (date1 != date2); // true

// Comparison
bool less = (date1 < date2); // true
bool greater = (date1 > date2); // false
bool lessOrEqual = (date1 <= date2); // true
bool greaterOrEqual = (date1 >= date2); // false

// Implicit conversion from DateTime
SmartDate date3 = DateTime.Now;

// Implicit conversion from string
SmartDate date4 = "12/25/2025";

// Explicit conversion to DateTime
DateTime dt = (DateTime)date1;

// Explicit conversion to string
string str = (string)date1;
```

## Common Patterns

### Business Object Property

The most common pattern is using SmartDate as a backing field for a string property:

```csharp
[Serializable]
public class Customer : BusinessBase<Customer>
{
    public static readonly PropertyInfo<SmartDate> BirthDateProperty = 
        RegisterProperty<SmartDate>(c => c.BirthDate, null, new SmartDate());

    public string BirthDate
    {
        get => GetPropertyConvert<SmartDate, string>(BirthDateProperty);
        set => SetPropertyConvert<SmartDate, string>(BirthDateProperty, value);
    }
    
    // Data portal operations
    [Fetch]
    private void Fetch([Inject] IDataPortal<Customer> portal)
    {
        using (var ctx = ConnectionManager.GetManager())
        {
            using (var cmd = ctx.Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT BirthDate FROM Customers WHERE Id = @Id";
                using (var dr = new SafeDataReader(cmd.ExecuteReader()))
                {
                    if (dr.Read())
                    {
                        LoadProperty(BirthDateProperty, dr.GetSmartDate("BirthDate"));
                    }
                }
            }
        }
    }
    
    [Insert]
    private void Insert()
    {
        using (var ctx = ConnectionManager.GetManager())
        {
            using (var cmd = ctx.Connection.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO Customers (BirthDate) VALUES (@BirthDate)";
                cmd.Parameters.AddWithValue("@BirthDate", 
                    ReadProperty(BirthDateProperty).DBValue);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
```

### UI Data Binding

In a UI (WPF, WinForms, Blazor, etc.), bind directly to the string property:

```xml
<!-- WPF -->
<TextBox Text="{Binding BirthDate}" />
```

```razor
<!-- Blazor -->
<input @bind="customer.BirthDate" />
```

Users can type date values, use shortcuts, or leave the field empty, and SmartDate handles all the conversion logic.

### Validation Rules

You can add business rules to validate SmartDate properties:

```csharp
protected override void AddBusinessRules()
{
    base.AddBusinessRules();
    
    BusinessRules.AddRule(new DateNotInFuture(BirthDateProperty));
}

private class DateNotInFuture : BusinessRule
{
    protected override void Execute(IRuleContext context)
    {
        var date = (SmartDate)context.InputPropertyValues[PrimaryProperty];
        if (!date.IsEmpty && date.Date > DateTime.Now)
        {
            context.AddErrorResult("Birth date cannot be in the future");
        }
    }
}
```

## Best Practices

1. **Use string properties with SmartDate backing fields**: This provides the best UI experience while maintaining type safety

2. **Configure empty date behavior appropriately**: Choose `MinDate` or `MaxDate` based on your sorting and comparison needs

3. **Use DBValue for database operations**: This ensures proper `NULL` handling

4. **Set global format strings early**: Call `SetDefaultFormatString()` during application startup for consistent formatting

5. **Leverage keystroke shortcuts**: Document shortcuts for users to improve data entry efficiency

6. **Handle empty dates in validation**: Check `IsEmpty` in business rules when dates are required

7. **Use with SafeDataReader**: Combine SmartDate with SafeDataReader for seamless database integration

## Implementation Notes

- SmartDate is a **struct** (value type), not a class
- It implements `IComparable`, `IFormattable`, `IConvertible`, and `IMobileObject`
- The default format string is `"d"` (short date pattern)
- Empty strings and `null` values are treated as empty dates
- Text parsing is case-insensitive
- The custom parser is static and shared across all SmartDate instances

## Related Topics

- [SafeDataReader](SafeDataReader.md) - For reading dates from databases
- [Data Access](Data-Access.md) - General data access patterns
- [BusinessRules](BusinessRules.md) - Implementing validation for date properties
- [ObjectStereotypes](ObjectStereotypes.md) - Business object patterns that use SmartDate

## See Also

- CSLA .NET Documentation: https://cslanet.com
- Source Code: https://github.com/MarimerLLC/csla/blob/main/Source/Csla/SmartDate.cs
