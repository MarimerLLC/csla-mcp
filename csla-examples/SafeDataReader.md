# SafeDataReader and SafeSqlDataReader

The `SafeDataReader` and `SafeSqlDataReader` types are optional helper classes in CSLA .NET that simplify working with ADO.NET data readers by automatically handling null values and providing safe type conversions when reading data from databases.

## Overview

When using raw ADO.NET to interact with databases, developers must manually check for `DBNull` values before converting database values to .NET types. The `SafeDataReader` classes eliminate this repetitive null-checking code by automatically converting null database values to sensible default values for each .NET type.

## When to Use SafeDataReader

Use `SafeDataReader` when:
- Manually implementing data access code using ADO.NET
- Working with `IDataReader` objects in DataPortal operations
- You want to reduce boilerplate null-checking code
- You prefer automatic null-to-default-value conversions

## SafeDataReader Class

The `SafeDataReader` class wraps any `IDataReader` implementation and provides null-safe methods for retrieving data.

### Basic Usage

```csharp
using Csla.Data;
using System.Data;

public class PersonDal
{
  private readonly IDbConnection _connection;

  public PersonDal(IDbConnection connection)
  {
    _connection = connection;
  }

  public Person Fetch(int id)
  {
    using (var command = _connection.CreateCommand())
    {
      command.CommandText = "SELECT Id, Name, Age, Email FROM Person WHERE Id = @Id";
      var parameter = command.CreateParameter();
      parameter.ParameterName = "@Id";
      parameter.Value = id;
      command.Parameters.Add(parameter);
      
      using (var reader = command.ExecuteReader())
      using (var safeReader = new SafeDataReader(reader))
      {
        if (safeReader.Read())
        {
          var person = new Person();
          person.Id = safeReader.GetInt32("Id");
          person.Name = safeReader.GetString("Name");        // Returns "" for null
          person.Age = safeReader.GetInt32("Age");           // Returns 0 for null
          person.Email = safeReader.GetString("Email");      // Returns "" for null
          return person;
        }
      }
    }
    throw new DataNotFoundException();
  }
}
```

### Null Value Handling

`SafeDataReader` automatically converts null database values to type-appropriate default values:

| Method | Database Null Returns |
|--------|----------------------|
| `GetString()` | `""` (empty string) |
| `GetInt32()` | `0` |
| `GetInt64()` | `0` |
| `GetInt16()` | `0` |
| `GetDouble()` | `0.0` |
| `GetDecimal()` | `0` |
| `GetFloat()` | `0.0f` |
| `GetBoolean()` | `false` |
| `GetByte()` | `0` |
| `GetChar()` | `char.MinValue` |
| `GetDateTime()` | `DateTime.MinValue` |
| `GetDateTimeOffset()` | `DateTimeOffset.MinValue` |
| `GetGuid()` | `Guid.Empty` |
| `GetValue()` | `null` |

### Accessing Values by Name or Ordinal

`SafeDataReader` supports accessing column values by both name and ordinal position:

```csharp
// By column name
string name = safeReader.GetString("Name");
int age = safeReader.GetInt32("Age");

// By ordinal position
string name = safeReader.GetString(0);
int age = safeReader.GetInt32(1);
```

### SmartDate Support

`SafeDataReader` includes special support for CSLA's `SmartDate` type:

```csharp
// Returns SmartDate with MinValue for null
SmartDate birthDate = safeReader.GetSmartDate("BirthDate");

// Specify whether min or max date represents empty
SmartDate birthDate = safeReader.GetSmartDate("BirthDate", minIsEmpty: true);
SmartDate expiryDate = safeReader.GetSmartDate("ExpiryDate", minIsEmpty: false);
```

### Working with Multiple Result Sets

```csharp
using (var reader = command.ExecuteReader())
using (var safeReader = new SafeDataReader(reader))
{
  // First result set - people
  while (safeReader.Read())
  {
    var person = new Person
    {
      Id = safeReader.GetInt32("Id"),
      Name = safeReader.GetString("Name")
    };
    people.Add(person);
  }
  
  // Move to next result set - addresses
  if (safeReader.NextResult())
  {
    while (safeReader.Read())
    {
      var address = new Address
      {
        Id = safeReader.GetInt32("Id"),
        Street = safeReader.GetString("Street")
      };
      addresses.Add(address);
    }
  }
}
```

### Checking for Null Values

While `SafeDataReader` automatically converts nulls to defaults, you can still check for null values explicitly:

```csharp
if (safeReader.IsDBNull("MiddleName"))
{
  // Handle null case specifically
  person.HasMiddleName = false;
}
else
{
  person.MiddleName = safeReader.GetString("MiddleName");
  person.HasMiddleName = true;
}
```

### IDataReader Methods

`SafeDataReader` implements the full `IDataReader` interface and forwards calls to the underlying reader:

```csharp
// Navigation
bool hasData = safeReader.Read();
bool hasMoreResults = safeReader.NextResult();

// Metadata
int fieldCount = safeReader.FieldCount;
string columnName = safeReader.GetName(0);
Type fieldType = safeReader.GetFieldType(0);
DataTable schema = safeReader.GetSchemaTable();

// State
bool isClosed = safeReader.IsClosed;
int recordsAffected = safeReader.RecordsAffected;
```

## SafeSqlDataReader Class

`SafeSqlDataReader` is a specialized version of `SafeDataReader` that provides additional functionality when working specifically with SQL Server through `SqlDataReader`.

### Basic Usage

```csharp
using Csla.Data.SqlClient;
using System.Data.SqlClient;

public class PersonDal
{
  private readonly SqlConnection _connection;

  public PersonDal(SqlConnection connection)
  {
    _connection = connection;
  }

  public async Task<Person> FetchAsync(int id, CancellationToken ct)
  {
    using (var command = _connection.CreateCommand())
    {
      command.CommandText = "SELECT Id, Name, Age, Email FROM Person WHERE Id = @Id";
      command.Parameters.AddWithValue("@Id", id);
      
      using (var reader = await command.ExecuteReaderAsync(ct))
      using (var safeReader = new SafeSqlDataReader(reader))
      {
        if (await safeReader.ReadAsync(ct))
        {
          var person = new Person();
          person.Id = await safeReader.GetFieldValueAsync<int>(0, ct);
          person.Name = await safeReader.GetFieldValueAsync<string>(1, ct);
          person.Age = await safeReader.GetFieldValueAsync<int>(2, ct);
          person.Email = await safeReader.GetFieldValueAsync<string>(3, ct);
          return person;
        }
      }
    }
    throw new DataNotFoundException();
  }
}
```

### Async Methods

`SafeSqlDataReader` provides async versions of common operations:

```csharp
// Async navigation
bool hasData = await safeReader.ReadAsync();
bool hasData = await safeReader.ReadAsync(cancellationToken);
bool hasMoreResults = await safeReader.NextResultAsync();
bool hasMoreResults = await safeReader.NextResultAsync(cancellationToken);

// Async null checking
bool isNull = await safeReader.IsDbNullAsync(ordinal);
bool isNull = await safeReader.IsDbNullAsync(ordinal, cancellationToken);

// Async value retrieval with generic type support
int age = await safeReader.GetFieldValueAsync<int>(ordinal);
string name = await safeReader.GetFieldValueAsync<string>(ordinal, cancellationToken);
```

### When to Use SafeSqlDataReader vs SafeDataReader

Use `SafeSqlDataReader` when:
- Working specifically with SQL Server databases
- Using async/await patterns in your data access code
- You need the enhanced async capabilities of `SqlDataReader`

Use `SafeDataReader` when:
- Working with any database provider (Oracle, PostgreSQL, MySQL, etc.)
- Using synchronous data access patterns
- You need maximum portability across database providers

## Complete Data Access Example

Here's a complete example showing `SafeDataReader` in a DataPortal operation:

```csharp
using Csla;
using Csla.Data;
using System;
using System.Data.SqlClient;

[Serializable]
public class Person : BusinessBase<Person>
{
  public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(c => c.Id);
  public int Id
  {
    get => GetProperty(IdProperty);
    private set => LoadProperty(IdProperty, value);
  }

  public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(c => c.Name);
  public string Name
  {
    get => GetProperty(NameProperty);
    set => SetProperty(NameProperty, value);
  }

  public static readonly PropertyInfo<int> AgeProperty = RegisterProperty<int>(c => c.Age);
  public int Age
  {
    get => GetProperty(AgeProperty);
    set => SetProperty(AgeProperty, value);
  }

  public static readonly PropertyInfo<SmartDate> BirthDateProperty = RegisterProperty<SmartDate>(c => c.BirthDate);
  public SmartDate BirthDate
  {
    get => GetProperty(BirthDateProperty);
    set => SetProperty(BirthDateProperty, value);
  }

  [Fetch]
  private void Fetch(int id, [Inject] IPersonDal dal)
  {
    var data = dal.Fetch(id);
    using (BypassPropertyChecks)
    {
      Id = data.Id;
      Name = data.Name;
      Age = data.Age;
      BirthDate = data.BirthDate;
    }
  }

  [Insert]
  private void Insert([Inject] IPersonDal dal)
  {
    using (BypassPropertyChecks)
    {
      var data = new PersonDto
      {
        Name = Name,
        Age = Age,
        BirthDate = BirthDate
      };
      Id = dal.Insert(data);
    }
  }

  [Update]
  private void Update([Inject] IPersonDal dal)
  {
    using (BypassPropertyChecks)
    {
      var data = new PersonDto
      {
        Id = Id,
        Name = Name,
        Age = Age,
        BirthDate = BirthDate
      };
      dal.Update(data);
    }
  }
}

public interface IPersonDal
{
  PersonDto Fetch(int id);
  int Insert(PersonDto data);
  void Update(PersonDto data);
}

public class PersonDal : IPersonDal
{
  private readonly SqlConnection _connection;

  public PersonDal(SqlConnection connection)
  {
    _connection = connection;
  }

  public PersonDto Fetch(int id)
  {
    using (var command = _connection.CreateCommand())
    {
      command.CommandText = "SELECT Id, Name, Age, BirthDate FROM Person WHERE Id = @Id";
      command.Parameters.AddWithValue("@Id", id);
      
      using (var reader = command.ExecuteReader())
      using (var safeReader = new SafeDataReader(reader))
      {
        if (safeReader.Read())
        {
          return new PersonDto
          {
            Id = safeReader.GetInt32("Id"),
            Name = safeReader.GetString("Name"),
            Age = safeReader.GetInt32("Age"),
            BirthDate = safeReader.GetSmartDate("BirthDate")
          };
        }
      }
    }
    throw new DataNotFoundException($"Person with Id {id} not found");
  }

  public int Insert(PersonDto data)
  {
    using (var command = _connection.CreateCommand())
    {
      command.CommandText = @"
        INSERT INTO Person (Name, Age, BirthDate)
        VALUES (@Name, @Age, @BirthDate);
        SELECT SCOPE_IDENTITY();";
      
      command.Parameters.AddWithValue("@Name", data.Name);
      command.Parameters.AddWithValue("@Age", data.Age);
      command.Parameters.AddWithValue("@BirthDate", 
        data.BirthDate.Date != DateTime.MinValue ? (object)data.BirthDate.Date : DBNull.Value);
      
      return Convert.ToInt32(command.ExecuteScalar());
    }
  }

  public void Update(PersonDto data)
  {
    using (var command = _connection.CreateCommand())
    {
      command.CommandText = @"
        UPDATE Person
        SET Name = @Name, Age = @Age, BirthDate = @BirthDate
        WHERE Id = @Id";
      
      command.Parameters.AddWithValue("@Id", data.Id);
      command.Parameters.AddWithValue("@Name", data.Name);
      command.Parameters.AddWithValue("@Age", data.Age);
      command.Parameters.AddWithValue("@BirthDate", 
        data.BirthDate.Date != DateTime.MinValue ? (object)data.BirthDate.Date : DBNull.Value);
      
      command.ExecuteNonQuery();
    }
  }
}

public class PersonDto
{
  public int Id { get; set; }
  public string Name { get; set; }
  public int Age { get; set; }
  public SmartDate BirthDate { get; set; }
}
```

## Benefits

1. **Reduced Boilerplate**: No need to manually check for `DBNull` before accessing values
2. **Type Safety**: Methods are strongly typed for specific .NET types
3. **Consistent Defaults**: Predictable default values for null database fields
4. **SmartDate Integration**: Built-in support for CSLA's `SmartDate` type
5. **Async Support**: `SafeSqlDataReader` provides full async capabilities for modern code

## Best Practices

1. **Always Dispose**: Wrap `SafeDataReader` in a `using` statement to ensure proper disposal
2. **Consider Business Logic**: Understand that null values are converted to defaults; ensure this matches your business rules
3. **Explicit Null Checks**: Use `IsDBNull()` when you need to distinguish between null and default values
4. **Column Names vs Ordinals**: Use column names for readability; use ordinals for performance in tight loops
5. **Async When Possible**: Use `SafeSqlDataReader` with async methods for better scalability

## Alternatives

While `SafeDataReader` is convenient, you can also:
- Use the standard `IDataReader` with explicit null checks
- Use micro-ORMs like Dapper for automatic mapping
- Use Entity Framework or other full ORMs for complete abstraction
- Use the DataMapper utility class (see [DataMapper.md](DataMapper.md))

The choice depends on your application's needs, performance requirements, and architectural preferences.

## Related Documentation

- [Data Access Guide](Data-Access.md)
- [DataMapper](DataMapper.md)
- [DataPortal Guide](DataPortalGuide.md)
