# 🔍 Blockchain Analyzer

A powerful blockchain analysis tool built with Clean Architecture principles in .NET Core 6.0, designed for real-time monitoring and analysis of blockchain activities with robust error handling and rate limiting.

## 🌟 Key Features

- 🔄 **Real-time Blockchain Monitoring**
  - Efficient block scanning with configurable batch sizes
  - Smart contract interaction detection with retry policies
  - Automatic pool discovery and validation
  - Transaction pattern recognition with error handling
  - Rate limit-aware processing with exponential backoff

- 🪙 **Token Tracking**
  - Automatic new token detection and validation
  - Pool creation monitoring with factory pattern
  - Reserve updates tracking with optimistic concurrency
  - Token pair analysis with relationship management
  - Placeholder token creation for unknown assets

- 🏗️ **Clean Architecture**
  - Domain-Driven Design with rich domain models
  - SOLID principles implementation
  - CQRS pattern with MediatR
  - Repository pattern with Entity Framework Core
  - Factory methods for entity creation

## 🏛️ Architecture Overview

### 📦 Domain Layer
Core business logic and domain models:
- Entities:
  - `Token`: Represents ERC20 tokens with validation
  - `Pool`: Represents liquidity pools with reserve tracking
- Value Objects:
  - `PoolType`: Enumeration of supported pool types
  - `ChainConfig`: Blockchain configuration settings
- Repository Interfaces:
  - `ITokenRepository`: Token data access contract
  - `IPoolRepository`: Pool data access contract
- Service Interfaces:
  - `IBlockchainService`: Blockchain interaction contract

### 🎯 Application Layer
Use cases and business operations:
- Commands:
  - `CreateTokenCommand`: Token creation and validation
  - `CreatePoolCommand`: Pool creation with token relationships
  - `UpdatePoolReservesCommand`: Reserve updates handling
- Command Handlers:
  - Validation logic
  - Domain model creation
  - Repository coordination
- CQRS Implementation:
  - Command/Query separation
  - MediatR for in-process messaging

### 🔧 Infrastructure Layer
External concerns implementation:
- Blockchain Service:
  - RPC communication with retry policies
  - Smart contract interaction
  - Rate limit handling
- Repositories:
  - Entity Framework Core implementation
  - SQLite database integration
  - Optimistic concurrency handling
- Background Services:
  - `BlockchainMonitorService` with batch processing
  - Configurable polling intervals
  - Error handling with exponential backoff

### 🎮 API Layer
Application entry point:
- Dependency Injection:
  - Service lifetime management
  - Configuration binding
  - Logger setup
- Configuration:
  - Environment-based settings
  - Chain-specific configurations
  - Monitoring parameters

## 🚀 Getting Started

### Prerequisites
- 📋 .NET 6.0 SDK
- 🗄️ SQLite
- 🌐 Ethereum RPC endpoint (Infura, Ankr, or local node)
- 🛠️ Entity Framework Core tools (`dotnet tool install --global dotnet-ef`)

### Configuration
1. Update `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=local.db"
  },
  "ChainConfig": {
    "ChainId": "1",
    "Name": "Ethereum",
    "RpcUrl": "your-rpc-url",
    "RpcPort": 443,
    "BlockTime": 15,
    "ConfirmationBlocks": 12
  },
  "Monitoring": {
    "PollingInterval": 120000,
    "BlocksToProcess": 5,
    "BatchSize": 1,
    "RetryDelay": 30000,
    "MaxRetries": 5,
    "RequestDelay": 10000
  }
}
```

### Installation
```bash
# Clone repository
git clone https://github.com/yourusername/blockchain-analyzer.git

# Navigate to project
cd blockchain-analyzer

# Restore packages
dotnet restore

# Create database
dotnet ef database update

# Start application
dotnet run --project src/AnalyzerCore.Api/AnalyzerCore.Api.csproj
```

## 💡 How It Works

### Block Processing
1. **Block Monitoring**
   - Polls blockchain for new blocks using configured RPC endpoint
   - Processes blocks in configurable batch sizes
   - Implements exponential backoff for rate limiting
   - Handles RPC errors with retry policies

2. **Contract Analysis**
   - Detects contract interactions in transactions
   - Validates contract code presence
   - Identifies pool contracts using interface detection
   - Handles failed contract calls gracefully

3. **Pool Management**
   - Creates new pool entries with factory pattern
   - Maintains token relationships
   - Updates reserves with optimistic concurrency
   - Handles pool creation edge cases

### Data Management
1. **Token Tracking**
   - Creates placeholder tokens for unknown addresses
   - Updates token information asynchronously
   - Maintains token relationships in pools
   - Handles token creation race conditions

2. **Database Operations**
   - Uses Entity Framework Core with SQLite
   - Implements repository pattern
   - Handles concurrent updates
   - Maintains data consistency

## 🛠️ Technical Details

### Entity Models
```csharp
public class Pool
{
    public string Address { get; private set; }
    public Token Token0 { get; private set; }
    public Token Token1 { get; private set; }
    public string Factory { get; private set; }
    public decimal Reserve0 { get; private set; }
    public decimal Reserve1 { get; private set; }
    public PoolType Type { get; private set; }
    
    public static Pool Create(...)  // Factory method
    public void UpdateReserves(...) // Domain logic
}
```

### Background Service
```csharp
public class BlockchainMonitorService : BackgroundService
{
    protected override async Task ExecuteAsync(...)
    {
        // Batch processing with retry policies
        // Rate limit handling
        // Error recovery
    }
}
```

### Configuration Options

| Setting | Description | Default | Notes |
|---------|-------------|---------|-------|
| PollingInterval | Block check frequency (ms) | 120000 | Adjust based on RPC limits |
| BlocksToProcess | Blocks per iteration | 5 | Balance throughput vs. memory |
| BatchSize | Blocks per RPC call | 1 | Optimize for rate limits |
| RetryDelay | Base delay for retries (ms) | 30000 | Used with exponential backoff |
| MaxRetries | Maximum retry attempts | 5 | Prevent infinite retries |
| RequestDelay | Delay between RPC calls (ms) | 10000 | Respect rate limits |

## 🔐 Security & Error Handling

- ✅ Input Validation
  - Address format validation
  - Chain ID verification
  - Contract code verification

- 🔒 Safe Contract Interaction
  - Read-only calls
  - Contract interface validation
  - Timeout handling

- 🛡️ Error Recovery
  - Exponential backoff
  - Circuit breaker pattern
  - Graceful degradation

- 🔄 Rate Limiting
  - Request throttling
  - Batch size optimization
  - Adaptive delays

## 🤝 Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📚 Documentation

For more detailed information:

- 🏗️ [Architecture Guide](docs/architecture.md)
- 🔧 [Setup Guide](docs/setup.md)
- 📖 [API Documentation](docs/api.md)
- 🔍 [Monitoring Guide](docs/monitoring.md)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- .NET Community
- Blockchain developers
- Open source contributors

---
Built with ❤️ using .NET 6.0 and Clean Architecture
