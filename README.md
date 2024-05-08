# Analyzer Project Overview

The Analyzer project is a sophisticated blockchain analysis tool designed for monitoring and analyzing blockchain transactions. Developed with the .NET platform, this tool offers real-time data collection, processing, and notification capabilities to cater to various blockchain networks.

## Key Features

- **Real-time Transaction Monitoring**: Tracks and analyzes transactions within designated block ranges to identify patterns, anomalies, or specific conditions.
- **Token Observation**: Automatically detects new token deployments and significant transactions involving tokens, offering insights into token dynamics and behaviors.
- **Notification System**: Utilizes Telegram to deliver real-time alerts and updates on identified events or transactions, directly to stakeholders or systems.
- **Configurability**: Supports various blockchain configurations, allowing users to tailor the tool's monitoring capabilities to specific needs without modifying the source code.

## Project Components

### Entry Point
- `Program.cs`: Initializes the application, setting up dependencies and services based on the provided configuration.

### Services
This component contains several services that are integral to the system's operation:

- `AnalyzerObserverService.cs`: A service dedicated to observing and analyzing blockchain data for configured block ranges and transaction types.
- `DataCollectorService.cs`: Responsible for gathering blockchain data. It filters and processes transactions according to predefined criteria.
- `TokenObserverService.cs`: Focuses on tracking and analyzing new tokens and significant token-related events.

### Database Layer (`DbLayer`)
Handles all database interactions:

- `DbContext.cs`: Configures and maintains the connection to the SQLite database, mapping the application's data models.
- Models:
  - `TokenEntity.cs`: Represents a token with properties such as address, symbol, transaction count, etc.
  - `TransactionHash.cs`: Links transactions to tokens, storing hashes and related data.
  - `Exchange.cs` & `Pool.cs`: Store information related to exchanges and liquidity pools associated with tokens.

### Models
Defines various configuration and data models used across the application.

### Notifier
- `TelegramNotifier.cs`: Implements the logic for sending notifications via Telegram.
- `ITelegramNotifier.cs`: Defines the interface for Telegram notifications.

## How It Works

1. **Data Collection**: The `DataCollectorService` continuously polls the blockchain for new data, focusing on the blocks and transactions specified in the configuration.
2. **Data Analysis**: Transactions are analyzed in real-time to detect new tokens, track token transfers, and monitor exchanges and pools.
3. **Notification**: The `TelegramNotifier` sends out alerts based on the analysis, informing subscribers of important events like new tokens or significant transactions.

## Use Cases

- **Blockchain Developers**: Monitor and debug new deployments or track transactions on their contracts.
- **Financial Analysts**: Gain insights into token dynamics and transaction flows on the blockchain.
- **Crypto Enthusiasts**: Keep updated with real-time information about token launches and major blockchain events.

## Configuration

The tool can be configured to monitor different blockchains by adjusting the `appSettings.json` files for each supported blockchain (e.g., BSC, HECO, POLYGON).

## Getting Started

Clone the repository and set up the required environment variables and configuration files according to your blockchain of interest. Run the application as a .NET Core service.

## Contributing

Contributions are welcome! Feel free to fork the repository, make changes, and submit a pull request.

