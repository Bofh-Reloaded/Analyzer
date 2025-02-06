# Setup Guide

## System Requirements

### Required Software
- .NET 6.0 SDK or later
- SQLite 3.x
- Git (for cloning repository)
- Visual Studio Code or Visual Studio 2022 (recommended)

### Development Tools
```bash
# Install Entity Framework Core tools
dotnet tool install --global dotnet-ef --version 6.0.26
```

## Installation Steps

### 1. Clone Repository
```bash
# Clone the repository
git clone https://github.com/yourusername/blockchain-analyzer.git

# Navigate to project directory
cd blockchain-analyzer
```

### 2. Configuration Setup

#### Database Configuration
Create or modify `appsettings.json` in the API project:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=local.db"
  }
}
```

#### Blockchain Configuration
Configure RPC endpoint and chain settings:
```json
{
  "ChainConfig": {
    "ChainId": "1",
    "Name": "Ethereum",
    "RpcUrl": "rpc.ankr.com/eth",
    "RpcPort": 443,
    "BlockTime": 15,
    "ConfirmationBlocks": 12
  }
}
```

#### Monitoring Settings
Optimize monitoring parameters:
```json
{
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

### 3. Database Setup

```bash
# Restore packages
dotnet restore

# Create initial migration
dotnet ef migrations add InitialCreate --project src/AnalyzerCore.Infrastructure/AnalyzerCore.Infrastructure.csproj --startup-project src/AnalyzerCore.Api/AnalyzerCore.Api.csproj

# Apply migrations
dotnet ef database update --project src/AnalyzerCore.Infrastructure/AnalyzerCore.Infrastructure.csproj --startup-project src/AnalyzerCore.Api/AnalyzerCore.Api.csproj
```

### 4. Build and Run

```bash
# Build solution
dotnet build

# Run application
dotnet run --project src/AnalyzerCore.Api/AnalyzerCore.Api.csproj
```

## Configuration Details

### Chain Configuration

| Setting | Description | Example |
|---------|-------------|---------|
| ChainId | Network identifier | "1" for Ethereum mainnet |
| Name | Network name | "Ethereum" |
| RpcUrl | RPC endpoint URL | "rpc.ankr.com/eth" |
| RpcPort | RPC endpoint port | 443 for HTTPS |
| BlockTime | Average block time | 15 seconds for Ethereum |
| ConfirmationBlocks | Required confirmations | 12 blocks |

### Monitoring Configuration

| Setting | Description | Recommended |
|---------|-------------|-------------|
| PollingInterval | Time between block checks | 120000 ms (2 min) |
| BlocksToProcess | Blocks per iteration | 5 blocks |
| BatchSize | Blocks per RPC call | 1 block |
| RetryDelay | Base retry delay | 30000 ms |
| MaxRetries | Maximum retry attempts | 5 attempts |
| RequestDelay | Delay between RPC calls | 10000 ms |

## Environment Setup

### Development Environment
```bash
# Set environment
export ASPNETCORE_ENVIRONMENT=Development

# Use development settings
cp appsettings.Development.json appsettings.json
```

### Production Environment
```bash
# Set environment
export ASPNETCORE_ENVIRONMENT=Production

# Use production settings
cp appsettings.Production.json appsettings.json
```

## Troubleshooting

### Common Issues

1. **Database Connection**
```bash
# Verify database file
ls -l local.db

# Check permissions
chmod 644 local.db
```

2. **RPC Connection**
```bash
# Test RPC endpoint
curl -X POST -H "Content-Type: application/json" \
  --data '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}' \
  https://your-rpc-endpoint
```

3. **Entity Framework Tools**
```bash
# Verify installation
dotnet ef --version

# Reinstall if needed
dotnet tool uninstall --global dotnet-ef
dotnet tool install --global dotnet-ef --version 6.0.26
```

### Logging

Configure logging levels in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "AnalyzerCore.Infrastructure.BackgroundServices": "Debug"
    }
  }
}
```

View logs:
```bash
# Real-time log viewing
tail -f logs/analyzer.log

# Filter errors
grep -i error logs/analyzer.log
```

## Performance Tuning

### RPC Rate Limiting

Adjust settings based on RPC provider limits:
```json
{
  "Monitoring": {
    "BlocksToProcess": 5,    // Decrease if hitting rate limits
    "RequestDelay": 10000,   // Increase if getting 429 errors
    "BatchSize": 1          // Keep at 1 for stability
  }
}
```

### Memory Optimization

Monitor memory usage:
```bash
# View process memory
ps -o pid,rss,command -p $(pgrep -f AnalyzerCore.Api)

# Monitor in real-time
top -p $(pgrep -f AnalyzerCore.Api)
```

## Security Considerations

### RPC Endpoint Security
- Use HTTPS endpoints only
- Consider using API keys
- Implement rate limiting
- Monitor usage patterns

### Database Security
- Set proper file permissions
- Regular backups
- Monitor disk space
- Use connection string encryption

## Maintenance

### Regular Tasks
1. Monitor disk space
2. Check log files
3. Verify database integrity
4. Update dependencies
5. Review performance metrics

### Backup Procedures
```bash
# Backup database
cp local.db local.db.backup

# Backup configuration
cp appsettings.json appsettings.json.backup

# Archive logs
tar -czf logs-$(date +%Y%m%d).tar.gz logs/
```

## Support

For additional support:
1. Check issue tracker
2. Review documentation
3. Contact development team
4. Join community discussions