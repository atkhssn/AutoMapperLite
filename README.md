# AutoMapperLite

AutoMapperLite is a lightweight object-to-object mapper for .NET.  
It supports simple and nested class mapping using reflection.

## Features

- Map between objects with matching properties
- Custom property resolvers
- Nested model mapping
- No dependencies

## Example

```csharp
services.AddAutoMapperLite(cfg =>
{
    cfg.CreateMap<Address, AddressDto>();
    cfg.CreateMap<User, UserDto>()
       .ForMember(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}");
});

var mapper = serviceProvider.GetRequiredService<IMapper>();
UserDto dto = mapper.Map<User, UserDto>(user);