# AutoMapperLite

A lightweight, customizable object-to-object mapping library for .NET projects.  
Designed for internal use in your projects to simplify DTO and entity mapping with easy-to-use profiles, support for nested properties, and flexible configuration.

---

## Features

- Simple and fast object mapping between source and destination types.
- Supports nested object mapping.
- Fluent profile-based configuration similar to AutoMapper.
- Supports `ForMember` and `ForPath` for custom member and nested member mappings.
- Supports collection mapping (e.g., List<T>).
- Integration via Dependency Injection (DI).
- Designed as a small NuGet package for internal project use.

---

## Installation

Install the NuGet package in your project:

```bash
dotnet add package AutoMapperLite --version 3.0.1
```

### 1. Register AutoMapperLite in your DI container

In your Program.cs or startup configuration:

```csharp
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Register all profiles from the executing assembly
builder.Services.AddAutoMapperLite(Assembly.GetExecutingAssembly());

var app = builder.Build();

````

### 2. Create Mapping Profiles

Define your mappings by creating classes inheriting from Profile and override Configure method:

```csharp
using AutoMapperLite;
using AutoMapperLite.Interfaces;

public class MyMappingProfile : Profile
{
    public override void Configure(IMapperConfig config)
    {
        // Basic map
        config.CreateMap<Country, CountryViewModel>();

        // Map with ForMember for direct property mapping
        config.CreateMap<Organization, OrganizationViewModel>()
            .ForMember(dest => dest.CountryViewModel, src => src.Country);

        // Map with ForPath for nested properties
        config.CreateMap<Location, LocationViewModel>()
            .ForPath(dest => dest.OrganizationViewModel.CountryViewModel, src => src.Organization.Country);
    }
}
```

### 3. Inject and Use Mapper

Inject IMapper in your classes and map objects easily:

```csharp
public class MyService
{
    private readonly IMapper _mapper;

    public MyService(IMapper mapper)
    {
        _mapper = mapper;
    }

    public LocationViewModel GetLocationViewModel(Location location)
    {
        return _mapper.Map<LocationViewModel>(location);
    }
}
```
---

## API Overview

### CreateMap<TSource, TDestination>()
Defines a mapping between source and destination types.

### ForMember(destinationMember, sourceFunc)
Configures mapping for a **single direct property** of the destination object.

- **destinationMember**: Expression to specify a destination property (e.g. `dest => dest.CountryViewModel`)
- **sourceFunc**: Function to select source property/value.

### ForPath(destinationPath, sourceFunc)
Configures mapping for **nested properties** within the destination object.

- **destinationPath**: Expression specifying a nested destination property path (e.g. `dest => dest.OrganizationViewModel.CountryViewModel.Name`)
- **sourceFunc**: Function to select source property/value.

---

## When to use ForMember vs ForPath?

| ForMember                                   | ForPath                                          |
|---------------------------------------------|--------------------------------------------------|
| Maps a direct, single-level property        | Maps a nested property several levels deep       |
| Use when destination property is top-level  | Use when destination has nested complex objects  |
| Simpler and direct                          | Allows mapping into nested objects’ properties   |

---

## Supported Features

- Map single objects and collections (`List<T>`).
- Auto-mapping properties with the same name and compatible types.
- Custom member mapping via `ForMember` and `ForPath`.
- Nested object instantiation and mapping.
- DI-friendly registration with extension method `AddAutoMapperLite`.
- Profiles discovery from assemblies.

---

## Folder Structure

```bash
AutoMapperLite/
│
├── Core/
│ ├── Mapper.cs # Core mapper logic and reflection
│ ├── MapBuilder.cs # Mapping configuration builder
│ └── Profile.cs # Base Profile class for defining maps
│
├── Interfaces/
│ ├── IMapper.cs # Mapper interface
│ ├── IMapperConfig.cs # Mapper configuration interface
│
├── Extensions/
│ └── ServiceCollectionExtensions.cs # DI extension methods
│
├── README.md # This documentation file
└── AutoMapperLite.csproj # Project file
```

---

## Contributing

This package is designed for internal or commercial use in your projects.  
Feel free to fork, modify, and enhance as needed. Pull requests and suggestions are welcome.

---

## License

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

---

## Contact

For questions or help, contact your internal dev team or maintainer.
Email: [atik.hassan@outlook.com](mailto:atik.hassan@outlook.com)
GitHub: [AutoMapperLite](https://github.com/atkhssn/AutoMapperLite)

---

Thank you for using **AutoMapperLite**!  
Happy Mapping 😊
