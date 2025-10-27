# Implementing a Data Access Layer

As discussed in `Glossary.md`, there are four models for implementing or invoking a data access layer (DAL). This document focuses on the _Encapsulated invocation_ model.

In this model there are typically three projects involved:

1. Business logic layer (class library project containing the business domain types)
2. Data access abstractions (class library project containing interfaces and entity or data transfer (DTO) types)
3. Data access implementation (class library project containing the concrete implementation of the interfaces in the data access abstractions project)

This separation of concerns allows multiple concrete implementations of the data access layer and provides total separation between the business layer and any data access layer artifacts or technologies.

Many people prefer to use Entity Framework as their abstraction, in which case projects 2 and 3 might be combined into a single EF project.

> **Note:** The patterns shown in this document apply to any RDBMS supported by ADO.NET and/or Entity Framework, including SQL Server, PostgreSQL, SQLite, MySQL, Oracle, and others. The examples use SQL Server, but the same principles apply to other databases with appropriate provider-specific adjustments.

## Business classes

The business domain classes use the data portal operation attributes to implement the data portal operations, and they rely on dependency injection to inject the appropriate data access services by using the interfaces defined in the data access abstractions project.

There are examples documents for each type of data portal operation implementation, so they are not repeated here.

## ADO.NET implementation

The following sticks with the three separate projects, using ADO.NET directly to implement the concrete DAL.

### Data access abstractions

This class library is referenced by both the business layer and the data access implementation project, so the types defined in this project are available to business classes and to the concrete data access implementation(s).

Typically, for each type of data entity, the following are included:

1. A data access interface
2. An entity class designed for easy serialization, often called a POCO (plain old CLR object) or DTO (data transfer object) or entity class

For example, if the data store has the concept of a "Person", these types would be available:

```csharp
public interface IPersonDal
{
    Task<List<PersonData>> GetPersons();
    Task<PersonData> GetPersonById(int id);
    Task<PersonData> InsertPerson(PersonData entity);
    Task<PersonData> UpdatePerson(PersonData entity);
    Task DeletePerson(int id);
}

public class PersonData
{
    public int Id { get; set; }
    public string Name { get; set; }
    // properties representing other columns
}
```

### Data access implementation

The data access implementation project references the data access abstractions project and so can use the types defined there.

The implementation project can use any data access technology or technologies to interact with one or more data stores (such as a database, file system, etc.). This example uses ADO.NET directly.

In the application host project, register the DAL so the business layer receives it via dependency injection. The following snippet assumes the host also manages the lifetime of the open `SqlConnection`:

```csharp
services.AddScoped<SqlConnection>(_ =>
{
    var connection = new SqlConnection(configuration.GetConnectionString("Persons"));
    connection.Open();
    return connection;
});

services.AddScoped<IPersonDal, PersonDal>();
```

The data portal creates a dependency injection scope for each root data portal invocation, and so the scoped database connection (and any other scoped services) will be automatically disposed when the data portal operation completes.

```csharp
public class PersonDal : IPersonDal
{
    private readonly SqlConnection _connection;

    public PersonDal(SqlConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<List<PersonData>> GetPersons()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Id, Name FROM dbo.Persons ORDER BY Name";

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var results = new List<PersonData>();
        var idOrdinal = reader.GetOrdinal("Id");
        var nameOrdinal = reader.GetOrdinal("Name");

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            results.Add(new PersonData
            {
                Id = reader.GetInt32(idOrdinal),
                Name = reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal)
            });
        }

        return results;
    }

    public async Task<PersonData> GetPersonById(int id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Id, Name FROM dbo.Persons WHERE Id = @Id";
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow).ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            return null;
        }

        var idOrdinal = reader.GetOrdinal("Id");
        var nameOrdinal = reader.GetOrdinal("Name");

        return new PersonData
        {
            Id = reader.GetInt32(idOrdinal),
            Name = reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal)
        };
    }

    public async Task<PersonData> InsertPerson(PersonData entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        using var command = _connection.CreateCommand();
        command.CommandText = @"INSERT INTO dbo.Persons (Name)
                                VALUES (@Name);
                                SELECT CAST(SCOPE_IDENTITY() AS int);";
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = entity.Name ?? (object)DBNull.Value;

        entity.Id = (int)await command.ExecuteScalarAsync().ConfigureAwait(false);
        return entity;
    }

    public async Task<PersonData> UpdatePerson(PersonData entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        using var command = _connection.CreateCommand();
        command.CommandText = @"UPDATE dbo.Persons
                                SET Name = @Name
                                WHERE Id = @Id";
        command.Parameters.Add("@Id", SqlDbType.Int).Value = entity.Id;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = entity.Name ?? (object)DBNull.Value;

        var rows = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        if (rows == 0)
        {
            throw new InvalidOperationException($"Person {entity.Id} was not found.");
        }

        return entity;
    }

    public async Task DeletePerson(int id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.Persons WHERE Id = @Id";
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        var rows = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        if (rows == 0)
        {
            throw new InvalidOperationException($"Person {id} was not found.");
        }
    }
}
```

## Entity Framework Core implementation

Modern EF Core (6.0 and later) provides a lightweight way to build the concrete DAL while keeping the interface-based abstraction intact. The business layer can stay unchanged because it continues to depend on `IPersonDal`.

### DbContext configuration

Create a DbContext that maps the `PersonData` type to the target table. The DbContext lives in the data access implementation project alongside the EF Core `PersonDal`.

```csharp
using Microsoft.EntityFrameworkCore;

public sealed class PersonDbContext : DbContext
{
    public PersonDbContext(DbContextOptions<PersonDbContext> options)
        : base(options)
    {
    }

    public DbSet<PersonData> Persons => Set<PersonData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var person = modelBuilder.Entity<PersonData>();
        person.ToTable("Persons", "dbo");
        person.HasKey(p => p.Id);
        person.Property(p => p.Id).ValueGeneratedOnAdd();
        person.Property(p => p.Name).HasMaxLength(100);
    }
}
```

Register the DbContext and the DAL implementation in the service container (typically in the host project):

```csharp
services.AddDbContext<PersonDbContext>(builder =>
    builder.UseSqlServer(configuration.GetConnectionString("Persons")));

services.AddScoped<IPersonDal, PersonDal>();
```

### EF Core data access implementation

The EF Core-based DAL injects the DbContext and uses LINQ queries alongside the async EF Core APIs.

```csharp
using Microsoft.EntityFrameworkCore;

public sealed class PersonDal : IPersonDal
{
    private readonly PersonDbContext _dbContext;

    public PersonDal(PersonDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<PersonData>> GetPersons()
    {
        return await _dbContext.Persons
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<PersonData> GetPersonById(int id)
    {
        return await _dbContext.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id)
            .ConfigureAwait(false);
    }

    public async Task<PersonData> InsertPerson(PersonData entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        await _dbContext.Persons.AddAsync(entity).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        return entity;
    }

    public async Task<PersonData> UpdatePerson(PersonData entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        _dbContext.Persons.Update(entity);
        var rows = await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        if (rows == 0)
        {
            throw new InvalidOperationException($"Person {entity.Id} was not found.");
        }

        return entity;
    }

    public async Task DeletePerson(int id)
    {
        var existing = await _dbContext.Persons
            .FirstOrDefaultAsync(p => p.Id == id)
            .ConfigureAwait(false);

        if (existing == null)
        {
            throw new InvalidOperationException($"Person {id} was not found.");
        }

        _dbContext.Persons.Remove(existing);
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);
    }
}
```
