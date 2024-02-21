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
using QuantConnect.Data;
using QuantConnect.Logging;
using QuantConnect.Data.Market;

namespace QuantConnect.DataSource.CoinAPI.Tests
{
    public static class CoinApiTestHelper
    {
        public static readonly Symbol BTCUSDKraken = Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Kraken);
        public static readonly Symbol BTCUSDBitfinex = Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Bitfinex);
        public static readonly Symbol BTCUSDCoinbase = Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Coinbase);
        public static readonly Symbol BTCUSDTBinance = Symbol.Create("BTCUSDT", SecurityType.Crypto, Market.Binance);
        public static readonly Symbol BTCUSDTBinanceUS = Symbol.Create("BTCUSDT", SecurityType.Crypto, Market.BinanceUS);

        /// <summary>
        /// PERPETUAL BTCUSDT
        /// </summary>
        public static readonly Symbol BTCUSDFutureBinance = Symbol.Create("BTCUSD", SecurityType.CryptoFuture, Market.Binance);

        public static void AssertSymbol(Symbol actualSymbol, Symbol expectedSymbol)
        {
            Assert.IsTrue(actualSymbol == expectedSymbol, $"Unexpected Symbol: Expected {expectedSymbol}, but received {actualSymbol}.");
        }

        public static void AssertBaseData(List<BaseData> tradeBars, Resolution expectedResolution)
        {
            Assert.Greater(tradeBars.Count, 0);
            foreach (var tick in tradeBars)
            {
                Assert.IsNotNull(tick);
                Assert.Greater(tick.Price, 0);
                Assert.Greater(tick.Value, 0);
                Assert.Greater(tick.Time, DateTime.UnixEpoch);
                Assert.Greater(tick.EndTime, DateTime.UnixEpoch);

                if (tick.DataType == MarketDataType.Tick)
                {
                    return;
                }

                Assert.IsTrue(tick.DataType == MarketDataType.TradeBar || tick.DataType == MarketDataType.QuoteBar, $"Unexpected data type: Expected TradeBar or QuoteBar, but received {tick.DataType}.");

                switch (tick)
                {
                    case TradeBar trade:
                        Assert.Greater(trade.Low, 0);
                        Assert.Greater(trade.Open, 0);
                        Assert.Greater(trade.High, 0);
                        Assert.Greater(trade.Close, 0);
                        Assert.Greater(trade.Volume, 0);
                        Assert.IsTrue(trade.Period.ToHigherResolutionEquivalent(true) == expectedResolution);
                        break;
                    default:
                        Assert.Fail($"{nameof(CoinApiDataQueueHandlerTest)}.{nameof(AssertBaseData)}: The tick type doesn't support");
                        break;
                }
            }
        }

        public static SubscriptionDataConfig GetSubscriptionDataConfigs(Symbol symbol, Resolution resolution)
        {
            return GetSubscriptionDataConfig<TradeBar>(symbol, resolution);
        }

        public static SubscriptionDataConfig GetSubscriptionTickDataConfigs(Symbol symbol)
        {
            return new SubscriptionDataConfig(GetSubscriptionDataConfig<Tick>(symbol, Resolution.Tick), tickType: TickType.Trade);
        }

        private static SubscriptionDataConfig GetSubscriptionDataConfig<T>(Symbol symbol, Resolution resolution)
        {
            return new SubscriptionDataConfig(
                typeof(T),
                symbol,
                resolution,
                TimeZones.Utc,
                TimeZones.Utc,
                true,
                extendedHours: false,
                false);
        }
    }
}
