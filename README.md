# AutoMapperLite

A lightweight, customizable object-to-object mapping library for .NET projects.  
Designed for internal use in your projects to simplify DTO and entity mapping with easy-to-use profiles, support for nested properties, and flexible configuration.

Targets `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`.

📖 Full documentation (Quick Start, API reference, runnable examples): [`docs/index.html`](docs/index.html) — open it locally in a browser, or enable GitHub Pages on this repo (Settings → Pages → deploy from `/docs`) to serve it live.

---

## Features

- Simple and fast object mapping between source and destination types.
- Supports nested object mapping, including nested `List<T>` properties whose item types have a registered map.
- Fluent profile-based configuration similar to AutoMapper.
- Supports `ForMember` and `ForPath` for custom member and nested member mappings.
- Supports collection mapping (e.g., List<T>).
- Integration via Dependency Injection (DI) — `IMapperConfig` and `IMapper` are both registered as singletons, since neither holds per-request state.
- Thread-safe: mapping configuration and the mapper itself can be used concurrently.
- Designed as a small NuGet package for internal project use.

---

## Installation

Install the NuGet package in your project:

```bash
dotnet add package AutoMapperLite --version 3.0.3
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

        // ForMember/ForPath functions must return the already-mapped value, not the raw
        // source sub-object — the library does not implicitly convert types for you. When
        // the source and destination property types differ (as here: Country vs.
        // CountryViewModel), map through the mapper itself inside the function.
        var mapper = new Mapper(config);

        // Map with ForMember for direct property mapping
        config.CreateMap<Organization, OrganizationViewModel>()
            .ForMember(dest => dest.CountryViewModel, src => mapper.Map<CountryViewModel>(src.Country));

        // Map with ForPath for nested properties
        config.CreateMap<Location, LocationViewModel>()
            .ForPath(dest => dest.OrganizationViewModel.CountryViewModel, src => mapper.Map<CountryViewModel>(src.Organization.Country));
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
│ └── MapperConfig.cs # Mapping registry (CreateMap / GetMap / HasMap)
│
├── Mapping/
│ ├── MapBuilder.cs # Fluent mapping configuration builder (ForMember / ForPath)
│ └── Profile.cs # Base Profile class for defining maps
│
├── Interfaces/
│ ├── IMapper.cs # Mapper interface
│ └── IMapperConfig.cs # Mapper configuration interface
│
├── Extensions/
│ └── ServiceCollectionExtensions.cs # DI extension methods
│
├── AutoMapperLite.Tests/ # xUnit test project
├── AutoMapperLite.Benchmarks/ # BenchmarkDotNet benchmark project
│
├── README.md # This documentation file
├── LICENSE # MIT License
├── CONTRIBUTING.md # Contribution guidelines
├── CODE_OF_CONDUCT.md # Contributor Covenant
├── SECURITY.md # Vulnerability reporting policy
└── AutoMapperLite.csproj # Library project file
```

---

## Contributing

This package is designed for internal or commercial use in your projects.
Feel free to fork, modify, and enhance as needed. Pull requests and suggestions
are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) for how to get started,
and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community expectations.

For reporting security vulnerabilities, see [SECURITY.md](SECURITY.md).

---

## License

AutoMapper Lite is open-source software licensed under the [MIT License](LICENSE).

You are free to use, modify, distribute, and use this software commercially,
subject to the conditions of the MIT License.

---

## Contact

For questions or help, contact your internal dev team or maintainer.

Email: [atik.hassan@outlook.com](mailto:atik.hassan@outlook.com)

GitHub: [AutoMapperLite](https://github.com/atkhssn/AutoMapperLite)

---

Thank you for using **AutoMapperLite**!  
Happy Mapping 😊
