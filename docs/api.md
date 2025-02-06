# API Documentation

## Overview

The Blockchain Analyzer API provides endpoints for monitoring and analyzing blockchain data, including tokens and liquidity pools.

## Base URL

```
http://localhost:5000/api/v1
```

## Authentication

Currently, the API is designed for internal use and does not require authentication.

## Endpoints

### Tokens

#### Get Token
```http
GET /tokens/{address}?chainId={chainId}
```

Parameters:
- `address`: Token contract address
- `chainId`: Blockchain network identifier

Response:
```json
{
  "address": "0x...",
  "symbol": "TOKEN",
  "name": "Example Token",
  "decimals": 18,
  "totalSupply": "1000000000000000000000000",
  "chainId": "1"
}
```

#### List Tokens
```http
GET /tokens?chainId={chainId}&page={page}&pageSize={pageSize}
```

Parameters:
- `chainId`: Blockchain network identifier
- `page`: Page number (default: 1)
- `pageSize`: Items per page (default: 20)

Response:
```json
{
  "items": [
    {
      "address": "0x...",
      "symbol": "TOKEN1",
      "name": "Example Token 1",
      "decimals": 18,
      "totalSupply": "1000000000000000000000000",
      "chainId": "1"
    }
  ],
  "totalCount": 100,
  "page": 1,
  "pageSize": 20
}
```

### Pools

#### Get Pool
```http
GET /pools/{address}?factory={factory}
```

Parameters:
- `address`: Pool contract address
- `factory`: Factory contract address

Response:
```json
{
  "address": "0x...",
  "token0": {
    "address": "0x...",
    "symbol": "TOKEN0"
  },
  "token1": {
    "address": "0x...",
    "symbol": "TOKEN1"
  },
  "factory": "0x...",
  "reserve0": "1000000000000000000",
  "reserve1": "1000000000000000000",
  "type": "UniswapV2"
}
```

#### List Pools
```http
GET /pools?factory={factory}&page={page}&pageSize={pageSize}
```

Parameters:
- `factory`: Factory contract address
- `page`: Page number (default: 1)
- `pageSize`: Items per page (default: 20)

Response:
```json
{
  "items": [
    {
      "address": "0x...",
      "token0": {
        "address": "0x...",
        "symbol": "TOKEN0"
      },
      "token1": {
        "address": "0x...",
        "symbol": "TOKEN1"
      },
      "factory": "0x...",
      "reserve0": "1000000000000000000",
      "reserve1": "1000000000000000000",
      "type": "UniswapV2"
    }
  ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 20
}
```

#### Get Pool Reserves
```http
GET /pools/{address}/reserves
```

Parameters:
- `address`: Pool contract address

Response:
```json
{
  "reserve0": "1000000000000000000",
  "reserve1": "1000000000000000000",
  "lastUpdated": "2025-02-06T22:00:00Z"
}
```

### Monitoring

#### Get Monitoring Status
```http
GET /monitoring/status
```

Response:
```json
{
  "isRunning": true,
  "currentBlock": 21790170,
  "lastProcessedBlock": 21790165,
  "blocksToProcess": 5,
  "lastUpdate": "2025-02-06T22:00:00Z"
}
```

## Error Handling

### Error Response Format
```json
{
  "error": {
    "code": "NOT_FOUND",
    "message": "Token not found",
    "details": "Token with address 0x... was not found on chain 1"
  }
}
```

### Common Error Codes

| Code | Description |
|------|-------------|
| BAD_REQUEST | Invalid request parameters |
| NOT_FOUND | Resource not found |
| RATE_LIMIT | Too many requests |
| SERVER_ERROR | Internal server error |

## Rate Limiting

The API implements rate limiting based on IP address:

- 100 requests per minute per IP
- 5000 requests per hour per IP

Rate limit headers:
```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1707254400
```

## Pagination

All list endpoints support pagination with the following parameters:

- `page`: Page number (1-based)
- `pageSize`: Items per page (max 100)

Response includes pagination metadata:
```json
{
  "items": [...],
  "totalCount": 100,
  "page": 1,
  "pageSize": 20,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

## Data Models

### Token
```typescript
interface Token {
  address: string;      // Contract address
  symbol: string;       // Token symbol
  name: string;         // Token name
  decimals: number;     // Token decimals
  totalSupply: string;  // Total supply (BigInt string)
  chainId: string;      // Network identifier
}
```

### Pool
```typescript
interface Pool {
  address: string;      // Pool contract address
  token0: Token;        // First token
  token1: Token;        // Second token
  factory: string;      // Factory contract address
  reserve0: string;     // Token0 reserve (BigInt string)
  reserve1: string;     // Token1 reserve (BigInt string)
  type: PoolType;       // Pool type (enum)
}
```

## Examples

### Curl Examples

Get Token:
```bash
curl -X GET "http://localhost:5000/api/v1/tokens/0x...?chainId=1" \
  -H "Accept: application/json"
```

List Pools:
```bash
curl -X GET "http://localhost:5000/api/v1/pools?factory=0x...&page=1&pageSize=20" \
  -H "Accept: application/json"
```

### C# Example

```csharp
using var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5000/api/v1/")
};

var response = await httpClient.GetAsync($"tokens/{address}?chainId={chainId}");
if (response.IsSuccessStatusCode)
{
    var token = await response.Content.ReadFromJsonAsync<Token>();
    Console.WriteLine($"Token: {token.Symbol}");
}
```

## WebSocket Support

Real-time updates are available through WebSocket connections:

```javascript
const ws = new WebSocket('ws://localhost:5000/api/v1/ws');

ws.onmessage = (event) => {
    const data = JSON.parse(event.data);
    console.log('New pool:', data);
};
```

## API Versioning

The API uses URL versioning:
- Current version: `v1`
- Version format: `/api/v{version}/`
- Supported versions: `v1`

## Support

For API support:
1. Check API documentation
2. Review error messages
3. Contact development team
4. Submit issue on GitHub