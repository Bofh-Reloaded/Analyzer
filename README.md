# 🔍 Blockchain Analyzer

A powerful blockchain analysis tool built with Clean Architecture principles in .NET Core, designed for real-time monitoring and analysis of blockchain activities.

## 🌟 Key Features

- 🔄 **Real-time Blockchain Monitoring**
  - Continuous block scanning and analysis
  - Smart contract interaction detection
  - Automatic pool discovery
  - Transaction pattern recognition

- 🪙 **Token Tracking**
  - New token detection
  - Pool creation monitoring
  - Reserve updates tracking
  - Token pair analysis

- 🏗️ **Clean Architecture**
  - Domain-Driven Design
  - SOLID principles
  - CQRS pattern
  - Repository pattern

## 🏛️ Architecture Overview

### 📦 Domain Layer
The core business logic and rules:
- `Token` and `Pool` entities
- Repository interfaces
- Blockchain service interfaces
- Domain models and value objects

### 🎯 Application Layer
Use cases and business operations:
- Command handlers for token creation
- Pool management operations
- CQRS implementation with MediatR

### 🔧 Infrastructure Layer
External concerns implementation:
- Blockchain service implementation
- Database repositories
- Background monitoring service
- Entity Framework Core configuration

### 🎮 API Layer
Application entry point:
- Dependency injection setup
- Configuration management
- Background service hosting

## 🚀 Getting Started

### Prerequisites
- 📋 .NET 6.0 SDK
- 🗄️ SQLite
- 🌐 Blockchain RPC endpoint

### Configuration
1. Update `appsettings.json`:
```json
{
  "ChainConfig": {
    "ChainId": "1",
    "Name": "Ethereum",
    "RpcUrl": "your-rpc-url",
    "RpcPort": 8545
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

# Run migrations
dotnet ef database update

# Start application
dotnet run
```

## 💡 How It Works

1. 🔍 **Block Monitoring**
   - Continuously polls blockchain for new blocks
   - Processes blocks in configurable batch sizes
   - Identifies contract interactions

2. 🏊 **Pool Detection**
   - Analyzes contract interactions
   - Identifies pool creation events
   - Tracks pool reserves and updates

3. 🪙 **Token Tracking**
   - Automatically detects new tokens
   - Retrieves token information
   - Maintains token relationships

## 🛠️ Technical Details

### Database Schema
- `Tokens`: Stores token information
- `Pools`: Tracks liquidity pools
- Relationships maintained via Entity Framework Core

### Background Services
- `BlockchainMonitorService`: Main monitoring service
- Configurable polling intervals
- Automatic retry on failures

### Smart Contract Interaction
- ERC20 token support
- Uniswap V2 compatible pools
- Extensible for other protocols

## 🔐 Security

- ✅ Input validation
- 🔒 Safe contract interaction
- 🛡️ Error handling
- 🔄 Retry policies

## ⚙️ Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| PollingInterval | Block check frequency (ms) | 60000 |
| BlocksToProcess | Blocks per batch | 500 |
| ChainId | Network identifier | 1 |
| RpcUrl | Blockchain node URL | localhost |

## 🤝 Contributing

1. Fork the repository
2. Create your feature branch
3. Commit your changes
4. Push to the branch
5. Open a Pull Request

## 📝 Best Practices

- ✨ Clean Code principles
- 📏 SOLID design
- 🧪 Separation of concerns
- 🎯 Single responsibility
- 🔄 Dependency inversion

## 📚 Documentation

For more detailed information about specific components:

- 🏗️ [Architecture Guide](docs/architecture.md)
- 🔧 [Setup Guide](docs/setup.md)
- 📖 [API Documentation](docs/api.md)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- .NET Community
- Blockchain developers
- Open source contributors

---
Built with ❤️ for the blockchain community
