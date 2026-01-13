# Serialization in CSLA .NET

CSLA .NET relies on deep serialization for core functionality including:

* Cloning objects (n-level undo, snapshot support)
* Transferring objects through the data portal (especially remote scenarios)
* Saving and restoring object state, including non-public properties or fields
* The shape of the object graph must be preserved perfectly. A deserialized object graph must be identical to the serialized object graph, without losing or adding any object instances

**Key Characteristics**:

* CSLA uses a pluggable serialization architecture
* Serializers must implement `ISerializationFormatter`
* `MobileFormatter` is the default serializer
* Configuration is done via the CSLA fluent configuration API
* Source generator-based serializers offer significant performance improvements

## Serialization Architecture

### ISerializationFormatter Interface

All CSLA serializers must implement the `ISerializationFormatter` interface:

```csharp
namespace Csla.Serialization
{
    public interface ISerializationFormatter
    {
        // Deserialize from stream
        object? Deserialize(Stream serializationStream);
        
        // Deserialize from byte array
        object? Deserialize(byte[] serializationStream);
        
        // Serialize to stream
        void Serialize(Stream serializationStream, object? graph);
        
        // Serialize to byte array
        byte[] Serialize(object? graph);
    }
}
```

### Default MobileFormatter

CSLA provides `MobileFormatter` as the default serializer. It is designed for cross-platform serialization and works with objects implementing `IMobileObject`. All CSLA base types implement this interface automatically.

> ℹ️ It is NOT necessary to use the `Serializable` attribute on CSLA classes, and this attribute has been deprecated by Microsoft. Avoid using the `Serializable` attribute.

## Configuring Serialization

Serialization is configured through the `AddCsla` method using the fluent API.

### Using the Default MobileFormatter

If no serializer is explicitly configured, CSLA defaults to `MobileFormatter`:

```csharp
services.AddCsla();
```

This is equivalent to:

```csharp
services.AddCsla(options => options
    .Serialization(so => so.UseMobileFormatter()));
```

### Configuring MobileFormatter Options

`MobileFormatter` supports several configuration options:

```csharp
services.AddCsla(options => options
    .Serialization(so => so.UseMobileFormatter(mf =>
    {
        // Enable strong name type checking (default is enabled)
        mf.EnableStrongNamesCheck();
        
        // Or disable for cross-assembly scenarios
        mf.DisableStrongNamesCheck();
        
        // Configure custom reader/writer types
        mf.MobileReader<CslaBinaryReader>();
        mf.MobileWriter<CslaBinaryWriter>();
    })));
```

### Using a Custom Serializer

To use a custom serializer, use `UseSerializationFormatter<T>`:

```csharp
services.AddCsla(options => options
    .Serialization(so => so.UseSerializationFormatter<MyCustomFormatter>()));
```

## Custom Serializers for Non-CSLA Types

When `MobileFormatter` encounters a type that doesn't implement `IMobileObject`, you can register custom serializers.

### Using PocoSerializer

For simple POCO types, CSLA provides `PocoSerializer<T>` which uses `System.Text.Json`:

```csharp
services.AddCsla(options => options
    .Serialization(so => so.UseMobileFormatter(mf =>
    {
        mf.CustomSerializers.Add(
            new TypeMap<AddressInfo, PocoSerializer<AddressInfo>>());
    })));
```

### Implementing IMobileSerializer

For more control, implement `IMobileSerializer`:

```csharp
public class CustomAddressSerializer : IMobileSerializer
{
    public bool CanSerialize(Type type) => type == typeof(AddressInfo);
    
    public void Serialize(object obj, SerializationInfo info)
    {
        var address = (AddressInfo)obj;
        info.AddValue("Street", address.Street);
        info.AddValue("City", address.City);
        info.AddValue("ZipCode", address.ZipCode);
    }
    
    public object Deserialize(SerializationInfo info)
    {
        return new AddressInfo
        {
            Street = info.GetValue<string>("Street"),
            City = info.GetValue<string>("City"),
            ZipCode = info.GetValue<string>("ZipCode")
        };
    }
}
```

## Source Generator Serializers

CSLA 10 supports source generator-based serializers that offer significant performance benefits over the reflection-based `MobileFormatter`.

### CSLA AutoSerialization (Built-in)

CSLA includes a source generator for serializing POCO types. Mark your class with `[AutoSerializable]`:

```csharp
using Csla.Serialization;

[AutoSerializable]
public partial class AddressPOCO
{
    public string? AddressLine1 { get; set; }
    public string AddressLine2 { get; set; } = string.Empty;
    public string Town { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
}
```

**How it works**:

1. The source generator detects the `[AutoSerializable]` attribute
2. At compile time, it generates serialization code that implements `IMobileObject`
3. No reflection is needed at runtime

**Requirements**:

* Add the `Csla.Generator.AutoSerialization.CSharp` NuGet package
* Make the class `partial`
* Mark with `[AutoSerializable]`

**Generated code example**:

```csharp
// Auto-generated
[Serializable]
public partial class AddressPOCO : IMobileObject
{
    void IMobileObject.GetState(SerializationInfo info)
    {
        info.AddValue(nameof(AddressLine1), AddressLine1);
        info.AddValue(nameof(AddressLine2), AddressLine2);
        info.AddValue(nameof(Town), Town);
        info.AddValue(nameof(County), County);
        info.AddValue(nameof(Postcode), Postcode);
    }
    
    void IMobileObject.SetState(SerializationInfo info)
    {
        AddressLine1 = info.GetValue<string?>(nameof(AddressLine1));
        AddressLine2 = info.GetValue<string>(nameof(AddressLine2));
        Town = info.GetValue<string>(nameof(Town));
        County = info.GetValue<string>(nameof(County));
        Postcode = info.GetValue<string>(nameof(Postcode));
    }
    
    void IMobileObject.GetChildren(SerializationInfo info, MobileFormatter formatter) { }
    void IMobileObject.SetChildren(SerializationInfo info, MobileFormatter formatter) { }
}
```

### CslaGeneratorSerialization (Third-Party)

For maximum performance, consider [CslaGeneratorSerialization](https://github.com/JasonBock/CslaGeneratorSerialization) by Jason Bock. This is a complete replacement serializer that offers dramatic performance improvements.

**Features**:

* Works with CSLA version 10 and above
* Uses binary serialization with source generators
* Significantly faster than `MobileFormatter`
* Uses a fraction of the memory

**Performance Comparison** (from benchmark results):

| Scenario | Serializer | Time | Memory |
|----------|------------|------|--------|
| BusinessBase roundtrip | GeneratorFormatter | 1.6 µs | 3.9 KB |
| BusinessBase roundtrip | MobileFormatter | 6.4 µs | 22.0 KB |
| BusinessListBase roundtrip | GeneratorFormatter | 66 µs | 240 KB |
| BusinessListBase roundtrip | MobileFormatter | 488 µs | 1,373 KB |

**Configuration**:

1. Add the `CslaGeneratorSerialization` NuGet package
2. Mark your business objects with `[GeneratorSerializable]`:

```csharp
using CslaGeneratorSerialization;

[GeneratorSerializable]
[Serializable]
public sealed partial class Person : BusinessBase<Person>
{
    public static readonly PropertyInfo<Guid> IdProperty =
        RegisterProperty<Guid>(_ => _.Id);
    [Required]
    public Guid Id
    {
        get => GetProperty(IdProperty);
        set => SetProperty(IdProperty, value);
    }

    public static readonly PropertyInfo<string> NameProperty =
        RegisterProperty<string>(_ => _.Name);
    [Required]
    public string Name
    {
        get => GetProperty(NameProperty);
        set => SetProperty(NameProperty, value);
    }

    public static readonly PropertyInfo<uint> AgeProperty =
        RegisterProperty<uint>(_ => _.Age);
    [Required]
    public uint Age
    {
        get => GetProperty(AgeProperty);
        set => SetProperty(AgeProperty, value);
    }

    [Create]
    private void Create() => Id = Guid.NewGuid();
}
```

3. Configure CSLA to use the generator serializer:

```csharp
var services = new ServiceCollection();
services.AddCsla(options => options
    .Serialization(so => so.SerializationFormatter(typeof(GeneratorFormatter))));
services.AddCslaGeneratorSerialization();
var provider = services.BuildServiceProvider();
```

**Custom Type Serialization**:

Register custom serializers for types the generator doesn't know:

```csharp
services.AddCslaGeneratorSerialization();
services.AddSingleton(
    new CustomSerialization<int[]>(
        (data, writer) =>
        {
            writer.Write(data.Length);
            foreach (var item in data)
            {
                writer.Write(item);
            }
        },
        (reader) =>
        {
            var data = new int[reader.ReadInt32()];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = reader.ReadInt32();
            }
            return data;
        }));
```

**Custom State in Business Objects**:

Implement `IGeneratorSerializableCustomization` for custom serialization logic:

```csharp
[GeneratorSerializable]
public sealed partial class Data : BusinessBase<Data>, IGeneratorSerializableCustomization
{
    public void GetCustomState(BinaryReader reader)
    {
        Custom = reader.ReadInt32();
    }

    public void SetCustomState(BinaryWriter writer)
    {
        writer.Write(Custom);
    }

    public int Custom { get; set; }
    
    // Normal CSLA properties...
}
```

**Fallback Behavior**:

If `CslaGeneratorSerialization` encounters an object that doesn't implement `IGeneratorSerializable`, it falls back to `MobileFormatter`. This allows gradual migration.

## Using Serialization in Code

CSLA uses the configured serializer automatically for all internal operations. You can also use it directly:

### Object Cloning

```csharp
// Using ApplicationContext (injected)
var cloner = applicationContext.GetRequiredService<ObjectCloner>();
var clone = cloner.Clone(originalObject);
```

The `ObjectCloner` uses the configured `ISerializationFormatter` internally.

### Direct Serialization

```csharp
// Get the configured formatter
var formatter = applicationContext.GetRequiredService<ISerializationFormatter>();

// Serialize to byte array
byte[] data = formatter.Serialize(myObject);

// Deserialize from byte array
var restored = (MyBusinessObject)formatter.Deserialize(data);

// Or use streams
using var stream = new MemoryStream();
formatter.Serialize(stream, myObject);
stream.Position = 0;
var restored2 = (MyBusinessObject)formatter.Deserialize(stream);
```

## Best Practices

1. **Use the default** - `MobileFormatter` works for most scenarios without configuration

2. **Consider generator serializers for performance** - If serialization is a bottleneck, evaluate source generator options

3. **Mark classes partial** - When using source generators, classes must be partial

4. **Apply attributes consistently** - When using `[GeneratorSerializable]`, apply it to all business objects in the graph

5. **Disable strong name checks when needed** - If objects are serialized and deserialized across different assemblies with different versions, consider disabling strong name checks

6. **Fallback works** - `CslaGeneratorSerialization` falls back to `MobileFormatter` for unmarked types, enabling gradual migration

7. **Test serialization** - Always test that your objects serialize and deserialize correctly, especially when using custom serializers

## Troubleshooting

### Object doesn't implement IMobileObject

Error: `MustImplementIMobileObject`

**Cause**: A type in the object graph doesn't implement `IMobileObject` and has no custom serializer registered.

**Solution**: 
- For POCO types, use `[AutoSerializable]` attribute
- Register a custom serializer in `MobileFormatterOptions.CustomSerializers`
- Use `PocoSerializer<T>` for simple types

### Type resolution fails during deserialization

**Cause**: Assembly or type name doesn't match during deserialization.

**Solution**:
- Ensure the same type exists in both serializing and deserializing contexts
- Consider disabling strong name checks: `mf.DisableStrongNamesCheck()`

### Performance is slow

**Solution**: Consider using `CslaGeneratorSerialization` which can be 4-8x faster with 5-6x less memory allocation.
