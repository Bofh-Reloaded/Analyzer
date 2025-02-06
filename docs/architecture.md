# Architecture Guide

## Overview

The Blockchain Analyzer follows Clean Architecture principles, separating concerns into distinct layers with clear dependencies flowing inward.

```
[API] -> [Application] -> [Domain]
   ↓           ↓            ↑
   └─── [Infrastructure] ───┘
```

## Layer Details

### Domain Layer

The core business logic layer, completely independent of external concerns.

#### Entities
- **Token**
  ```csharp
  public class Token
  {
      public string Address { get; private set; }
      public string Symbol { get; private set; }
      public string Name { get; private set; }
      public int Decimals { get; private set; }
      public decimal TotalSupply { get; private set; }
      
      public static Token Create(...) // Factory method
  }
  ```

- **Pool**
  ```csharp
  public class Pool
  {
      public string Address { get; private set; }
      public Token Token0 { get; private set; }
      public Token Token1 { get; private set; }
      public decimal Reserve0 { get; private set; }
      public decimal Reserve1 { get; private set; }
      
      public static Pool Create(...) // Factory method
      public void UpdateReserves(...) // Domain logic
  }
  ```

#### Value Objects
- `PoolType`: Enumeration of supported pool types
- `ChainConfig`: Blockchain configuration settings

#### Repository Interfaces
- `ITokenRepository`: Contract for token data access
- `IPoolRepository`: Contract for pool data access

### Application Layer

Contains application logic and use cases.

#### Commands
- `CreateTokenCommand`: Creates new tokens
- `CreatePoolCommand`: Creates new pools
- `UpdatePoolReservesCommand`: Updates pool reserves

#### Command Handlers
```csharp
public class CreatePoolCommandHandler : IRequestHandler<CreatePoolCommand, Pool>
{
    private readonly IPoolRepository _poolRepository;
    private readonly ITokenRepository _tokenRepository;
    
    public async Task<Pool> Handle(CreatePoolCommand request, CancellationToken cancellationToken)
    {
        // Validation and business logic
        // Entity creation
        // Repository coordination
    }
}
```

### Infrastructure Layer

Implements interfaces defined in the domain layer.

#### Blockchain Service
```csharp
public class BlockchainService : IBlockchainService
{
    private readonly Web3 _web3;
    private readonly AsyncRetryPolicy _retryPolicy;
    
    public async Task<BigInteger> GetCurrentBlockNumberAsync(...)
    public async Task<IEnumerable<BlockData>> GetBlocksAsync(...)
    public async Task<PoolInfo> GetPoolInfoAsync(...)
}
```

#### Repositories
```csharp
public class TokenRepository : ITokenRepository
{
    private readonly ApplicationDbContext _context;
    
    public async Task<Token> GetByAddressAsync(...)
    public async Task<Token> AddAsync(...)
}
```

#### Background Services
```csharp
public class BlockchainMonitorService : BackgroundService
{
    protected override async Task ExecuteAsync(...)
    {
        // Block processing
        // Contract analysis
        // Rate limiting
    }
}
```

### API Layer

Application entry point and configuration.

#### Program.cs
```csharp
public static class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // Service registration
                // Configuration binding
                // Background service setup
            });
}
```

## Design Patterns

### Factory Pattern
Used for entity creation with validation and initialization:
```csharp
public static Pool Create(string address, Token token0, Token token1, ...)
{
    // Validation
    // Initialization
    return new Pool { ... };
}
```

### Repository Pattern
Abstracts data access behind interfaces:
```csharp
public interface ITokenRepository
{
    Task<Token> GetByAddressAsync(string address, string chainId);
    Task<Token> AddAsync(Token token);
}
```

### CQRS Pattern
Separates read and write operations using MediatR:
```csharp
public class CreatePoolCommand : IRequest<Pool>
{
    public string Address { get; set; }
    public string Token0Address { get; set; }
    public string Token1Address { get; set; }
}
```

## Error Handling

### Retry Policies
```csharp
Policy
    .Handle<Exception>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => 
            TimeSpan.FromSeconds(Math.Pow(2, attempt))
    );
```

### Validation
```csharp
public static class Guard
{
    public static void AgainstNullAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            throw new DomainException("Address cannot be null");
    }
}
```

## Dependencies

### Internal Dependencies
- Domain → None
- Application → Domain
- Infrastructure → Domain, Application
- API → All Layers

### External Dependencies
- Entity Framework Core
- MediatR
- Nethereum
- Polly
- Serilog

## Testing Strategy

### Unit Tests
- Domain logic
- Command handlers
- Value objects
- Entity validation

### Integration Tests
- Repository implementations
- Database operations
- Blockchain service

### End-to-End Tests
- API endpoints
- Background services
- Complete workflows