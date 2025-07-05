# AutoMapperLite

A lightweight and convention-friendly object-to-object mapper for .NET, inspired by AutoMapper — but simplified for small to medium projects and high control over mapping behavior.

---

## 🚀 Features

- [x] Simple and clean API
- [x] Supports `.ForMember` and `.ForPath`
- [x] Auto mapping for matching property names
- [x] Nested object mapping
- [x] Collection mapping (e.g., `List<T>`)
- [x] Profile-based mapping organization
- [x] Dependency Injection (DI) ready

---

## 📦 Installation

### 1.  Install from NuGet Package

To install via NuGet, run the following command:

```bash
dotnet add package AutoMapperLite --version 2.0.4
```

### 2. Register AutoMapperLite in `Program.cs`

```csharp
using AutoMapperLite;

builder.Services.AddAutoMapperLite(Assembly.GetExecutingAssembly());
````

### 3. Define your models
```csharp
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Department Department { get; set; }
}

public class Department
{
    public string Name { get; set; }
}
```

### 4. Define your view model
```csharp
public class UserViewModel
{
    public string FullName { get; set; }
    public DepartmentViewModel DepartmentViewModel { get; set; }
}

public class DepartmentViewModel
{
    public string DepartmentName { get; set; }
}
```

### 5. Create a mapping profile
```csharp
public class MappingProfile : Profile
{
    public override void Configure(IMapperConfig config)
    {
        config.CreateMap<User, UserViewModel>()
              .ForMember(dest => dest.FullName, src => src.FirstName + " " + src.LastName)
              .ForPath("DepartmentViewModel.DepartmentName", src => src.Department.Name);
    }
}
```

### 6. Inject and use IMapper
```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMapper _mapper;

    public UsersController(IMapper mapper)
    {
        _mapper = mapper;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var users = new List<User> {
            new User {
                FirstName = "John",
                LastName = "Doe",
                Department = new Department { Name = "IT" }
            }
        };

        var result = _mapper.Map<List<UserViewModel>>(users);
        return Ok(result);
    }
}
```