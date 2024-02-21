/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using NUnit.Framework;
using QuantConnect.Util;
using QuantConnect.Tests;
using QuantConnect.Logging;

namespace QuantConnect.DataSource.CoinAPI.Tests
{
    [TestFixture]
    public class CoinAPIDataDownloaderTests
    {
        private CoinAPIDataDownloader _downloader;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _downloader = new();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _downloader.DisposeSafely();
        }

        private static IEnumerable<TestCaseData> HistoricalValidDataTestCases
        {
            get
            {
                yield return new TestCaseData(CoinApiTestHelper.BTCUSDTBinance, Resolution.Minute, new DateTime(2024, 1, 1, 20, 0, 0), new DateTime(2024, 1, 1, 21, 0, 0));
                yield return new TestCaseData(CoinApiTestHelper.BTCUSDKraken, Resolution.Minute, new DateTime(2024, 1, 1, 20, 0, 0), new DateTime(2024, 1, 1, 21, 0, 0));
                yield return new TestCaseData(CoinApiTestHelper.BTCUSDBitfinex, Resolution.Hour, new DateTime(2024, 1, 1, 0, 0, 0), new DateTime(2024, 1, 1, 12, 0, 0));
                yield return new TestCaseData(CoinApiTestHelper.BTCUSDTBinance, Resolution.Daily, new DateTime(2024, 1, 1), new DateTime(2024, 2, 1));
            }
        }

        [TestCaseSource(nameof(HistoricalValidDataTestCases))]
        public void DownloadsHistoricalDataWithValidDataTestParameters(Symbol symbol, Resolution resolution, DateTime startDateTimeUtc, DateTime endDateTimeUtc)
        {
            var parameters = new DataDownloaderGetParameters(symbol, resolution, startDateTimeUtc, endDateTimeUtc, TickType.Trade);

            var downloadResponse = _downloader.Get(parameters).ToList();

            Assert.IsNotEmpty(downloadResponse);

            Log.Trace($"{symbol}.{resolution}.[{startDateTimeUtc} - {endDateTimeUtc}]: Amount = {downloadResponse.Count}");

            CoinApiTestHelper.AssertSymbol(downloadResponse.First().Symbol, symbol);

            CoinApiTestHelper.AssertBaseData(downloadResponse, resolution);
        }

        private static IEnumerable<TestCaseData> HistoricalInvalidDataTestCases
        {
            get
            {
                yield return new TestCaseData(CoinApiTestHelper.BTCUSDTBinance, Resolution.Tick, new DateTime(2024, 1, 1, 20, 0, 0), new DateTime(2024, 1, 1, 21, 0, 0), TickType.Trade)
                    .SetDescription($"Not supported - {Resolution.Tick}");
                yield return new TestCaseData(CoinApiTestHelper.BTCUSDTBinance, Resolution.Tick, new DateTime(2024, 1, 1), new DateTime(2023, 1, 1), TickType.Trade)
                    .SetDescription("Wrong startDateTime - startDateTime > endDateTime");
                yield return new TestCaseData(CoinApiTestHelper.BTCUSDTBinance, Resolution.Tick, new DateTime(2024, 1, 1), new DateTime(2024, 2, 1), TickType.Quote)
                    .SetDescription($"Not supported - {TickType.Quote}");
                yield return new TestCaseData(CoinApiTestHelper.BTCUSDTBinance, Resolution.Tick, new DateTime(2024, 1, 1), new DateTime(2024, 2, 1), TickType.OpenInterest)
                    .SetDescription($"Not supported - {TickType.OpenInterest}");
            }
        }

        [TestCaseSource(nameof(HistoricalInvalidDataTestCases))]
        public void DownloadsHistoricalDataWithInvalidDataTestParameters(Symbol symbol, Resolution resolution, DateTime startDateTimeUtc, DateTime endDateTimeUtc, TickType tickType)
        {
            var parameters = new DataDownloaderGetParameters(symbol, resolution, startDateTimeUtc, endDateTimeUtc, tickType);

            var downloadResponse = _downloader.Get(parameters).ToList();

            Assert.IsEmpty(downloadResponse);
        }

        private static IEnumerable<TestCaseData> HistoricalInvalidDataThrowExceptionTestCases
        {
            get
            {
                TestGlobals.Initialize();
                yield return new TestCaseData(Symbol.Create("BTCBTC", SecurityType.Crypto, Market.Binance))
                    .SetDescription($"Wrong Symbol - 'BTCBTC'");
                yield return new TestCaseData(Symbol.Create("ETHUSDT", SecurityType.Equity, Market.Binance))
                    .SetDescription($"Wrong SecurityType - {SecurityType.Equity}");
            }
        }

        [TestCaseSource(nameof(HistoricalInvalidDataThrowExceptionTestCases))]
        public void DownloadsHistoricalDataWithInvalidDataTestParametersThrowException(Symbol symbol)
        {
            var parameters = new DataDownloaderGetParameters(symbol, Resolution.Minute, new DateTime(2024, 1, 1), new DateTime(2024, 2, 1), TickType.Trade);

            Assert.That(() => _downloader.Get(parameters).ToList(), Throws.Exception);
        }
    }
}
