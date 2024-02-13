﻿/*
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
using System.Collections.Concurrent;

namespace QuantConnect.CoinAPI.Tests
{
    [TestFixture]
    public class CoinApiDataQueueHandlerTest
    {
        private CoinApiDataQueueHandler _coinApiDataQueueHandler;
        private CancellationTokenSource _cancellationTokenSource;

        [SetUp]
        public void SetUp()
        {
            _coinApiDataQueueHandler = new();
            _cancellationTokenSource = new();
        }

        [TearDown]
        public void TearDown()
        {
            _cancellationTokenSource.Dispose();

            if (_coinApiDataQueueHandler != null)
            {
                _coinApiDataQueueHandler.Dispose();
            }
        }

        [Test]
        public void SubscribeToBTCUSDSecondOnCoinbaseDataStreamTest()
        {
            var resetEvent = new AutoResetEvent(false);
            var tradeBars = new List<BaseData>();
            var resolution = Resolution.Second;
            var symbol = CoinApiTestHelper.BTCUSDCoinbase;
            var dataConfig = CoinApiTestHelper.GetSubscriptionDataConfigs(symbol, resolution);

            CoinApiTestHelper.ProcessFeed(
                _coinApiDataQueueHandler.Subscribe(dataConfig, (s, e) => { }),
                tick =>
                {
                    if (tick != null)
                    {
                        Log.Debug($"{nameof(CoinApiDataQueueHandlerTest)}: tick: {tick}");
                        tradeBars.Add(tick);

                        if (tradeBars.Count > 5)
                        {
                            resetEvent.Set();
                        }
                    }
                },
            () => _cancellationTokenSource.Cancel());

            Assert.IsTrue(resetEvent.WaitOne(TimeSpan.FromSeconds(60), _cancellationTokenSource.Token));

            _coinApiDataQueueHandler.Unsubscribe(dataConfig);

            CoinApiTestHelper.AssertSymbol(tradeBars.First().Symbol, symbol);

            CoinApiTestHelper.AssertBaseData(tradeBars, resolution);
        }

        [Test]
        public void SubscribeToBTCUSDSecondOnDifferentMarkets()
        {
            var resetEvent = new AutoResetEvent(false);
            var tradeBars = new List<TradeBar>();
            var resolution = Resolution.Second;
            var minimDataFromExchange = 5;

            var symbolBaseData = new ConcurrentDictionary<Symbol, List<BaseData>>
            {
                [CoinApiTestHelper.BTCUSDKraken] = new(),
                [CoinApiTestHelper.BTCUSDTBinance] = new(),
                [CoinApiTestHelper.BTCUSDBitfinex] = new(),
                [CoinApiTestHelper.BTCUSDCoinbase] = new()
            };

            var dataConfigs = new List<SubscriptionDataConfig>();
            foreach (var symbol in symbolBaseData.Keys)
            {
                dataConfigs.Add(CoinApiTestHelper.GetSubscriptionDataConfigs(symbol, resolution));
            }

            foreach (var config in dataConfigs)
            {
                CoinApiTestHelper.ProcessFeed(
                    _coinApiDataQueueHandler.Subscribe(config, (s, e) => { }),
                    tick =>
                    {
                        if (tick != null)
                        {
                            Log.Debug($"{nameof(CoinApiDataQueueHandlerTest)}: tick: {tick}");
                            symbolBaseData[tick.Symbol].Add(tick);
                        }
                    },
                () =>
                {
                    _cancellationTokenSource.Cancel();
                });
            }

            resetEvent.WaitOne(TimeSpan.FromSeconds(60), _cancellationTokenSource.Token);

            foreach (var data in symbolBaseData)
            {
                if (data.Value.Count > minimDataFromExchange)
                {
                    Log.Debug($"Unsubscribe: Symbol: {data.Key}, BaseData.Count: {data.Value.Count}");
                    var config = dataConfigs.Where(x => x.Symbol == data.Key).First();
                    _coinApiDataQueueHandler.Unsubscribe(config);
                    dataConfigs.Remove(config);
                }
            }

            if (dataConfigs.Count != 0)
            {
                resetEvent.WaitOne(TimeSpan.FromSeconds(30), _cancellationTokenSource.Token);
            }

            foreach (var config in dataConfigs)
            {
                _coinApiDataQueueHandler.Unsubscribe(config);
            }

            foreach (var data in symbolBaseData.Values)
            {
                CoinApiTestHelper.AssertBaseData(data, resolution);
            }
        }

        [Test]
        public void SubscribeToBTCUSDFutureTickOnDifferentMarkets()
        {
            var resetEvent = new AutoResetEvent(false);
            var resolution = Resolution.Tick;
            var tickData = new List<BaseData>();
            var symbol = CoinApiTestHelper.BTCUSDTFutureBinance;
            var config = CoinApiTestHelper.GetSubscriptionTickDataConfigs(symbol);

            CoinApiTestHelper.ProcessFeed(
                _coinApiDataQueueHandler.Subscribe(config, (s, e) => { }),
                tick =>
                {
                    if (tick != null)
                    {
                        Log.Debug($"{nameof(CoinApiDataQueueHandlerTest)}: tick: {tick}");
                        tickData.Add(tick);

                        if (tickData.Count > 5)
                        {
                            resetEvent.Set();
                        }
                    }
                },
            () =>
            {
                _cancellationTokenSource.Cancel();
            });

            resetEvent.WaitOne(TimeSpan.FromSeconds(60), _cancellationTokenSource.Token);

            // if seq is empty, give additional chance
            if (tickData.Count == 0) 
            {
                resetEvent.WaitOne(TimeSpan.FromSeconds(60), _cancellationTokenSource.Token);
            }

            _coinApiDataQueueHandler.Unsubscribe(config);

            if (tickData.Count == 0)
            {
                Assert.Fail($"{nameof(CoinApiDataQueueHandlerTest)}.{nameof(SubscribeToBTCUSDFutureTickOnDifferentMarkets)} is nothing returned. {symbol}|{resolution}|tickData = {tickData.Count}");
            }

            CoinApiTestHelper.AssertSymbol(tickData.First().Symbol, symbol);

            CoinApiTestHelper.AssertBaseData(tickData, resolution);
        }
    }
}
