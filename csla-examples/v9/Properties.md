# Implementing CSLA Properties

There are several types of property supported by CSLA:

| Property | Description |
| --- | --- |
| Private property | A property with `private` scope containing values for use inside a class |
| Read-only property | A property with a `private` setter |
| Read-write property | A public property |

## Private Property

This snippet demonstrates how to define a private property using the CSLA property registration system. This is useful for creating private properties that are part of any business class that derives from a CSLA base class, such as `BusinessBase<T>`, `ReadOnlyBase<T>`, or `CommandBase<T>`.

Note that the getter uses `ReadProperty` instead of `GetProperty`, which is appropriate for private properties. The `ReadProperty` method is used to read the value of a property without triggering any business rules or validation.

The property setter uses the `LoadProperty` method to set the property's value internally within the class. The `LoadProperty` method bypasses any business rules or validation.

```csharp
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        private int Id
        {
            get => ReadProperty(IdProperty);
            set => LoadProperty(IdProperty, value);
        }
```

## Read-Only Property

This snippet demonstrates how to define a read-only property using the CSLA property registration system. This is useful for creating read-only properties that are part of an editable business class that derives from `BusinessBase<T>` or a read-only business class that derives from `ReadOnlyBase<T>`.

Note that the property has a private setter, which is typically used in conjunction with the `LoadProperty` method to set the property's value internally within the class. The `LoadProperty` method bypasses any business rules or validation, making it suitable for initializing read-only properties.

```csharp
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => LoadProperty(IdProperty, value);
        }
```

## Read-Write Property

This snippet demonstrates how to define a read-write property using the CSLA property registration system. This is usefule for creating read-write properties that are part of an editable business class that derives from `BusinessBase<T>`.

```csharp
        public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
        public string Name
        {
            get => GetProperty(NameProperty);
            set => SetProperty(NameProperty, value);
        }
```

This snippet demonstrates how to define a read-write property that uses validation rules based on the `System.ComponentModel.DataAnnotations` namespace. In this example, the `Name` property is decorated with the `[Required]` and `[StringLength]` attributes to enforce that the property must have a value and that the value must be between 2 and 50 characters in length. Any appropriate DataAnnotations attributes can be used in this way.

```csharp
        public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string Name
        {
            get => GetProperty(NameProperty);
            set => SetProperty(NameProperty, value);
        }
```
