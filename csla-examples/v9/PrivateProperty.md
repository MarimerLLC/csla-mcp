# Private Property

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
