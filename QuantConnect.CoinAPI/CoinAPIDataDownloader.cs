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

using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Data.Market;

namespace QuantConnect.Lean.DataSource.CoinAPI
{
    public class CoinAPIDataDownloader : IDataDownloader, IDisposable
    {
        private readonly CoinApiDataProvider _historyProvider;

        private readonly MarketHoursDatabase _marketHoursDatabase;

        public CoinAPIDataDownloader()
        {
            _historyProvider = new CoinApiDataProvider();
            _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
        }

        public IEnumerable<BaseData> Get(DataDownloaderGetParameters dataDownloaderGetParameters)
        {
            if (dataDownloaderGetParameters.TickType != TickType.Trade)
            {
                Log.Error($"{nameof(CoinAPIDataDownloader)}.{nameof(Get)}: Not supported data type - {dataDownloaderGetParameters.TickType}. " +
                    $"Currently available support only for historical of type - {nameof(TickType.Trade)}");
                yield break;
            }

            if (dataDownloaderGetParameters.EndUtc < dataDownloaderGetParameters.StartUtc)
            {
                Log.Error($"{nameof(CoinAPIDataDownloader)}.{nameof(Get)}:InvalidDateRange. The history request start date must precede the end date, no history returned");
                yield break;
            }

            var symbol = dataDownloaderGetParameters.Symbol;

            var historyRequests = new HistoryRequest(
                    startTimeUtc: dataDownloaderGetParameters.StartUtc,
                    endTimeUtc: dataDownloaderGetParameters.EndUtc,
                    dataType: typeof(TradeBar),
                    symbol: symbol,
                    resolution: dataDownloaderGetParameters.Resolution,
                    exchangeHours: _marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType),
                    dataTimeZone: _marketHoursDatabase.GetDataTimeZone(symbol.ID.Market, symbol, symbol.SecurityType),
                    fillForwardResolution: dataDownloaderGetParameters.Resolution,
                    includeExtendedMarketHours: true,
                    isCustomData: false,
                    dataNormalizationMode: DataNormalizationMode.Raw,
                    tickType: TickType.Trade);

            foreach (var slice in _historyProvider.GetHistory(historyRequests))
            {
                yield return slice;
            }
        }

        public void Dispose()
        {
            _historyProvider.DisposeSafely();
        }
    }
}
