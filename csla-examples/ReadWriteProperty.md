# Read-Write Property

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
