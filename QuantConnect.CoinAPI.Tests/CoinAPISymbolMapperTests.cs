/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 *
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using NUnit.Framework;

namespace QuantConnect.CoinAPI.Tests
{
    [TestFixture, Explicit("")]
    public class CoinAPISymbolMapperTests
    {
        private CoinApiSymbolMapper _coinApiSymbolMapper;

        [OneTimeSetUp]
        public void SetUp()
        {
            _coinApiSymbolMapper = new CoinApiSymbolMapper();
        }

        [TestCase("COINBASE_SPOT_BTC_USD", "BTCUSD", Market.Coinbase)]
        [TestCase("COINBASE_SPOT_BCH_USD", "BCHUSD", Market.Coinbase)]
        [TestCase("BITFINEX_SPOT_BTC_USD", "BTCUSD", Market.Bitfinex)]
        [TestCase("BITFINEX_SPOT_BCHABC_USD", "BCHUSD", Market.Bitfinex)]
        [TestCase("BITFINEX_SPOT_BCHSV_USD", "BSVUSD", Market.Bitfinex)]
        [TestCase("BITFINEX_SPOT_ABS_USD", "ABYSSUSD", Market.Bitfinex)]
        public void ReturnsCorrectLeanSymbol(string coinApiSymbolId, string leanTicker, string market)
        {
            var symbol = _coinApiSymbolMapper.GetLeanSymbol(coinApiSymbolId, SecurityType.Crypto, string.Empty);

            Assert.That(symbol.Value, Is.EqualTo(leanTicker));
            Assert.That(symbol.ID.SecurityType, Is.EqualTo(SecurityType.Crypto));
            Assert.That(symbol.ID.Market, Is.EqualTo(market));
        }

        [TestCase("BTCUSD", Market.Coinbase, "COINBASE_SPOT_BTC_USD")]
        [TestCase("BCHUSD", Market.Coinbase, "COINBASE_SPOT_BCH_USD")]
        [TestCase("BTCUSD", Market.Bitfinex, "BITFINEX_SPOT_BTC_USD")]
        [TestCase("BCHUSD", Market.Bitfinex, "BITFINEX_SPOT_BCHABC_USD")]
        [TestCase("BSVUSD", Market.Bitfinex, "BITFINEX_SPOT_BCHSV_USD")]
        [TestCase("ABYSSUSD", Market.Bitfinex, "BITFINEX_SPOT_ABS_USD")]
        public void ReturnsCorrectBrokerageSymbol(string leanTicker, string market, string coinApiSymbolId)
        {
            var symbol = Symbol.Create(leanTicker, SecurityType.Crypto, market);

            var symbolId = _coinApiSymbolMapper.GetBrokerageSymbol(symbol);

            Assert.That(symbolId, Is.EqualTo(coinApiSymbolId));
        }

        [TestCase("BTCUSDT", Market.Binance, "BINANCEFTS_PERP_BTC_USDT")]
        public void ReturnsCorrectBrokerageFutureSymbol(string leanTicker, string market, string coinApiSymbolId)
        {
            var symbol = Symbol.Create(leanTicker, SecurityType.CryptoFuture, market);

            var symbolId = _coinApiSymbolMapper.GetBrokerageSymbol(symbol);

            Assert.That(symbolId, Is.EqualTo(coinApiSymbolId));
        }

        [TestCase("BTCUSDT", Market.Kraken)]
        public void TryGetWrongBrokerageFutureSymbolThrowException(string leanTicker, string market)
        {
            var symbol = Symbol.Create(leanTicker, SecurityType.CryptoFuture, market);
            Assert.Throws<Exception>(() => _coinApiSymbolMapper.GetBrokerageSymbol(symbol));
        }
    }
}
