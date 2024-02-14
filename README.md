![LEAN Data Source SDK](http://cdn.quantconnect.com.s3.us-east-1.amazonaws.com/datasources/Github_LeanDataSourceSDK.png)

# Lean CoinAPI DataSource Plugin

[![Build Status](https://github.com/QuantConnect/LeanDataSdk/workflows/Build%20%26%20Test/badge.svg)](https://github.com/QuantConnect/LeanDataSdk/actions?query=workflow%3A%22Build%20%26%20Test%22)

### Introduction

Welcome to the CoinAPI Connector Library for .NET 6. This open-source project provides a robust and efficient C# library designed to seamlessly connect with the CoinAPI. The library facilitates easy integration with the QuantConnect [LEAN Algorithmic Trading Engine](https://github.com/quantConnect/Lean), offering a clear and straightforward way for users to incorporate CoinAPI's extensive financial datasets into their algorithmic trading strategies.

### CoinAPI Overview
CoinAPI is a reliable provider of real-time and historical financial market data, offering support for traditional asset classes such as cryptocurrencies and crypto futures across various exchanges. With CoinAPI, developers can access a wealth of data to enhance their trading strategies and decision-making processes.

### Features

- **Easy Integration:** Simple and intuitive integration process, allowing developers to quickly incorporate CoinAPI's data into their trading algorithms.
- **Rich Financial Data:** Access to a vast array of real-time and historical data for cryptocurrencies and crypto futures, empowering developers to make informed trading decisions.
- **Flexible Configuration:** Customizable settings to tailor the integration according to specific trading needs and preferences.
- **Symbol SecurityType Support:**
  - [x] Crypto
  - [x] CryptoFuture
- **Exchange Support:**
  - [x] COINBASE
  - [x] BITFINEX
  - [x] BINANCE
  - [x] KRAKEN
  - [x] BINANCEUS
- **Backtesting and Research:** Seamlessly test and refine your trading algorithms using CoinAPI's data within QuantConnect LEAN's backtesting and research modes, enabling you to optimize your strategies with confidence.

### Contribute to the Project
Contributions to this open-source project are welcome! If you find any issues, have suggestions for improvements, or want to add new features, please open an issue or submit a pull request.

### Installation
To contribute to the CoinAPI Connector Library for .NET 6 within QuantConnect LEAN, follow these steps:
1. **Obtain API Key:** Sign up for a free CoinAPI key [here](https://docs.coinapi.io/) if you don't have one.
2. **Fork the Project:** Fork the repository by clicking the "Fork" button at the top right of the GitHub page.
3. Clone Your Forked Repository:
```
git clone https://github.com/your-username/Lean.DataSource.CoinAPI.git
```
4. **Configuration:**
  - Set the `coinapi-api-key` in your QuantConnect configuration (config.json or environment variables).
  - [optional] Set the `coinapi-product` (by default: Free)
```
{
    "coinapi-api-key": "",
    "coinapi-product": "",
}
```

### Price Plan
For detailed information on CoinAPI's pricing plans, please refer to the [CoinAPI Pricing](https://www.coinapi.io/market-data-api/pricing) page.

### Documentation
Refer to the [documentation](https://www.quantconnect.com/docs/v2/lean-cli/datasets/coinapi) for detailed information on the library's functions, parameters, and usage examples.

### License
This project is licensed under the MIT License - see the [LICENSE](#) file for details.

Happy coding and algorithmic trading!