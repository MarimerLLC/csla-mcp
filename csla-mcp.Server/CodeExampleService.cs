namespace csla_mcp.Server;

public class CodeExampleService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CodeExampleService> _logger;
    private readonly string _examplesPath;

    public CodeExampleService(IWebHostEnvironment environment, ILogger<CodeExampleService> logger)
    {
        _environment = environment;
        _logger = logger;
        _examplesPath = Path.Combine(_environment.ContentRootPath, "CodeExamples");
    }

    public async Task<List<CodeExample>> GetExamplesByConcept(string concept, string? category = null)
    {
        var examples = new List<CodeExample>();
        var conceptPath = Path.Combine(_examplesPath, concept);

        if (!Directory.Exists(conceptPath))
        {
            _logger.LogWarning("Concept directory not found: {ConceptPath}", conceptPath);
            return examples;
        }

        var searchPattern = category != null ? Path.Combine(conceptPath, category) : conceptPath;
        var files = Directory.GetFiles(searchPattern, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".md"))
            .ToList();

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            var relativePath = Path.GetRelativePath(_examplesPath, file);
            
            examples.Add(new CodeExample
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Content = FormatExampleContent(file, content),
                Concept = concept,
                Category = category ?? GetCategoryFromPath(relativePath),
                FilePath = relativePath
            });
        }

        return examples;
    }

    public async Task<List<ConceptInfo>> GetAvailableConcepts()
    {
        var concepts = new List<ConceptInfo>();

        if (!Directory.Exists(_examplesPath))
        {
            Directory.CreateDirectory(_examplesPath);
            await CreateDefaultExamples();
        }

        var conceptDirs = Directory.GetDirectories(_examplesPath);

        foreach (var dir in conceptDirs)
        {
            var conceptName = Path.GetFileName(dir);
            var categories = Directory.GetDirectories(dir)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>()
                .ToList();

            // Add files in root as "basic" category
            var rootFiles = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".cs") || f.EndsWith(".md"));
            
            if (rootFiles.Any() && !categories.Contains("basic"))
            {
                categories.Insert(0, "basic");
            }

            concepts.Add(new ConceptInfo
            {
                Name = conceptName,
                Description = GetConceptDescription(conceptName),
                Categories = categories
            });
        }

        return concepts;
    }

    public async Task<List<CodeExample>> SearchExamples(string query)
    {
        var allExamples = new List<CodeExample>();
        var concepts = await GetAvailableConcepts();

        foreach (var concept in concepts)
        {
            var examples = await GetExamplesByConcept(concept.Name);
            allExamples.AddRange(examples);
        }

        // Simple text-based search - could be enhanced with semantic search using AI
        var searchResults = allExamples
            .Where(ex => 
                ex.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                ex.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                ex.Concept.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return searchResults;
    }

    private string FormatExampleContent(string filePath, string content)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLower();
        
        if (extension == ".cs")
        {
            return $"## {fileName}\n\n```csharp\n{content}\n```";
        }
        else if (extension == ".md")
        {
            return $"## {fileName}\n\n{content}";
        }
        
        return content;
    }

    private string GetCategoryFromPath(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar);
        return parts.Length > 2 ? parts[1] : "basic";
    }

    private string GetConceptDescription(string conceptName)
    {
        return conceptName switch
        {
            "business-object" => "CSLA business object patterns and implementations",
            "data-portal" => "Data portal configuration and usage examples",
            "authorization" => "CSLA authorization rules and security patterns",
            "validation" => "Business and property validation rules",
            "serialization" => "Object serialization and state management",
            "ui-binding" => "UI data binding patterns and helpers",
            "dependency-injection" => "Dependency injection with CSLA objects",
            "blazor" => "Blazor-specific CSLA patterns and components",
            "aspnetcore" => "ASP.NET Core integration patterns",
            _ => $"Examples and patterns for {conceptName}"
        };
    }

    private async Task CreateDefaultExamples()
    {
        // Create some default example structure
        await CreateBusinessObjectExamples();
        await CreateDataPortalExamples();
        await CreateValidationExamples();
    }

    private async Task CreateBusinessObjectExamples()
    {
        var boPath = Path.Combine(_examplesPath, "business-object", "basic");
        Directory.CreateDirectory(boPath);

        var simpleBoExample = @"using Csla;

namespace MyApp.Library
{
    [Serializable]
    public class Customer : BusinessBase<Customer>
    {
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => SetProperty(IdProperty, value);
        }

        public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
        public string Name
        {
            get => GetProperty(NameProperty);
            set => SetProperty(NameProperty, value);
        }

        public static readonly PropertyInfo<string> EmailProperty = RegisterProperty<string>(nameof(Email));
        public string Email
        {
            get => GetProperty(EmailProperty);
            set => SetProperty(EmailProperty, value);
        }

        [Fetch]
        private async Task Fetch(int id, [Inject] ICustomerDal dal)
        {
            var data = await dal.GetAsync(id);
            using (BypassPropertyChecks)
            {
                Id = data.Id;
                Name = data.Name;
                Email = data.Email;
            }
        }

        [Insert]
        private async Task Insert([Inject] ICustomerDal dal)
        {
            using (BypassPropertyChecks)
            {
                var data = await dal.InsertAsync(Name, Email);
                Id = data.Id;
            }
        }

        [Update]
        private async Task Update([Inject] ICustomerDal dal)
        {
            await dal.UpdateAsync(Id, Name, Email);
        }

        [Delete]
        private async Task Delete([Inject] ICustomerDal dal)
        {
            await dal.DeleteAsync(Id);
        }
    }
}";

        await File.WriteAllTextAsync(Path.Combine(boPath, "SimpleBusinessObject.cs"), simpleBoExample);
    }

    private async Task CreateDataPortalExamples()
    {
        var dpPath = Path.Combine(_examplesPath, "data-portal", "basic");
        Directory.CreateDirectory(dpPath);

        var configExample = @"// Program.cs or Startup.cs configuration for Data Portal

using Csla.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add CSLA services
builder.Services.AddCsla(options => options
    .DataPortal(cfg => cfg
        .AddClientSideDataPortal()
        .AddServerSideDataPortal())
    .Security(cfg => cfg
        .AddWindowsIdentity()));

// Register your DAL services
builder.Services.AddScoped<ICustomerDal, CustomerDal>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCslaDataPortal();

app.Run();";

        await File.WriteAllTextAsync(Path.Combine(dpPath, "DataPortalConfiguration.cs"), configExample);
    }

    private async Task CreateValidationExamples()
    {
        var valPath = Path.Combine(_examplesPath, "validation", "basic");
        Directory.CreateDirectory(valPath);

        var validationExample = @"using Csla;
using Csla.Rules;
using Csla.Rules.CommonRules;

namespace MyApp.Library
{
    [Serializable]
    public class Customer : BusinessBase<Customer>
    {
        public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
        public string Name
        {
            get => GetProperty(NameProperty);
            set => SetProperty(NameProperty, value);
        }

        public static readonly PropertyInfo<string> EmailProperty = RegisterProperty<string>(nameof(Email));
        public string Email
        {
            get => GetProperty(EmailProperty);
            set => SetProperty(EmailProperty, value);
        }

        public static readonly PropertyInfo<DateTime> BirthDateProperty = RegisterProperty<DateTime>(nameof(BirthDate));
        public DateTime BirthDate
        {
            get => GetProperty(BirthDateProperty);
            set => SetProperty(BirthDateProperty, value);
        }

        protected override void AddBusinessRules()
        {
            // Property rules
            BusinessRules.AddRule(new Required(NameProperty));
            BusinessRules.AddRule(new MaxLength(NameProperty, 50));
            
            BusinessRules.AddRule(new Required(EmailProperty));
            BusinessRules.AddRule(new RegExMatch(EmailProperty, @""^[^@\s]+@[^@\s]+\.[^@\s]+$""));
            
            BusinessRules.AddRule(new Required(BirthDateProperty));
            BusinessRules.AddRule(new CommonRules.MinValue<DateTime>(BirthDateProperty, 
                new DateTime(1900, 1, 1)));
            BusinessRules.AddRule(new CommonRules.MaxValue<DateTime>(BirthDateProperty, 
                DateTime.Today));

            // Object-level business rule
            BusinessRules.AddRule(new AgeValidation(BirthDateProperty));
        }
    }

    public class AgeValidation : BusinessRule
    {
        public AgeValidation(IPropertyInfo primaryProperty) : base(primaryProperty)
        {
            InputProperties = new List<IPropertyInfo> { primaryProperty };
        }

        protected override void Execute(IRuleContext context)
        {
            var birthDate = (DateTime)context.InputPropertyValues[PrimaryProperty];
            var age = DateTime.Today.Year - birthDate.Year;
            
            if (birthDate.Date > DateTime.Today.AddYears(-age))
                age--;

            if (age < 18)
            {
                context.AddErrorResult(""Customer must be at least 18 years old."");
            }
        }
    }
}";

        await File.WriteAllTextAsync(Path.Combine(valPath, "ValidationRules.cs"), validationExample);
    }
}

public class CodeExample
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Concept { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class ConceptInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
}