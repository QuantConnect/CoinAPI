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

using NodaTime;
using RestSharp;
using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Logging;
using QuantConnect.Data.Market;
using QuantConnect.CoinAPI.Messages;
using QuantConnect.Lean.Engine.DataFeeds;
using HistoryRequest = QuantConnect.Data.HistoryRequest;

namespace QuantConnect.CoinAPI
{
    public partial class CoinApiDataQueueHandler
    {
        /// <summary>
        /// Indicates whether the warning for invalid history <see cref="TickType"/> has been fired.
        /// </summary>
        private bool _invalidHistoryDataTypeWarningFired;

        public override void Initialize(HistoryProviderInitializeParameters parameters)
        {
            // NOP
        }

        public override IEnumerable<Slice> GetHistory(IEnumerable<HistoryRequest> requests, DateTimeZone sliceTimeZone)
        {
            var subscriptions = new List<Subscription>();
            foreach (var request in requests)
            {
                var history = GetHistory(request);
                var subscription = CreateSubscription(request, history);
                subscriptions.Add(subscription);
            }
            return CreateSliceEnumerableFromSubscriptions(subscriptions, sliceTimeZone);
        }

        public IEnumerable<BaseData> GetHistory(HistoryRequest historyRequest)
        {
            if (historyRequest.Symbol.SecurityType != SecurityType.Crypto && historyRequest.Symbol.SecurityType != SecurityType.CryptoFuture)
            {
                Log.Error($"CoinApiDataQueueHandler.GetHistory(): Invalid security type {historyRequest.Symbol.SecurityType}");
                yield break;
            }

            if (historyRequest.Resolution == Resolution.Tick)
            {
                Log.Error($"CoinApiDataQueueHandler.GetHistory(): No historical ticks, only OHLCV timeseries");
                yield break;
            }

            if (historyRequest.DataType == typeof(QuoteBar))
            {
                if (!_invalidHistoryDataTypeWarningFired)
                {
                    Log.Error("CoinApiDataQueueHandler.GetHistory(): No historical QuoteBars , only TradeBars");
                    _invalidHistoryDataTypeWarningFired = true;
                }
                yield break;
            }

            var resolutionTimeSpan = historyRequest.Resolution.ToTimeSpan();
            var lastRequestedBarStartTime = historyRequest.EndTimeUtc.RoundDown(resolutionTimeSpan);
            var currentStartTime = historyRequest.StartTimeUtc.RoundUp(resolutionTimeSpan);
            var currentEndTime = lastRequestedBarStartTime;

            // Perform a check of the number of bars requested, this must not exceed a static limit
            var dataRequestedCount = (currentEndTime - currentStartTime).Ticks
                                     / resolutionTimeSpan.Ticks;

            if (dataRequestedCount > HistoricalDataPerRequestLimit)
            {
                currentEndTime = currentStartTime
                                 + TimeSpan.FromTicks(resolutionTimeSpan.Ticks * HistoricalDataPerRequestLimit);
            }

            while (currentStartTime < lastRequestedBarStartTime)
            {
                var coinApiSymbol = _symbolMapper.GetBrokerageSymbol(historyRequest.Symbol);
                var coinApiPeriod = _ResolutionToCoinApiPeriodMappings[historyRequest.Resolution];

                // Time must be in ISO 8601 format
                var coinApiStartTime = currentStartTime.ToStringInvariant("s");
                var coinApiEndTime = currentEndTime.ToStringInvariant("s");

                // Construct URL for rest request
                var baseUrl =
                    "https://rest.coinapi.io/v1/ohlcv/" +
                    $"{coinApiSymbol}/history?period_id={coinApiPeriod}&limit={HistoricalDataPerRequestLimit}" +
                    $"&time_start={coinApiStartTime}&time_end={coinApiEndTime}";

                // Execute
                var client = new RestClient(baseUrl);
                var restRequest = new RestRequest(Method.GET);
                restRequest.AddHeader("X-CoinAPI-Key", _apiKey);
                var response = client.Execute(restRequest);

                // Log the information associated with the API Key's rest call limits.
                TraceRestUsage(response);

                // Deserialize to array
                var coinApiHistoryBars = JsonConvert.DeserializeObject<HistoricalDataMessage[]>(response.Content);

                // Can be no historical data for a short period interval
                if (!coinApiHistoryBars.Any())
                {
                    Log.Error($"CoinApiDataQueueHandler.GetHistory(): API returned no data for the requested period [{coinApiStartTime} - {coinApiEndTime}] for symbol [{historyRequest.Symbol}]");
                    continue;
                }

                foreach (var ohlcv in coinApiHistoryBars)
                {
                    yield return
                        new TradeBar(ohlcv.TimePeriodStart, historyRequest.Symbol, ohlcv.PriceOpen, ohlcv.PriceHigh,
                            ohlcv.PriceLow, ohlcv.PriceClose, ohlcv.VolumeTraded, historyRequest.Resolution.ToTimeSpan());
                }

                currentStartTime = currentEndTime;
                currentEndTime += TimeSpan.FromTicks(resolutionTimeSpan.Ticks * HistoricalDataPerRequestLimit);
            }
        }

        private void TraceRestUsage(IRestResponse response)
        {
            var total = GetHttpHeaderValue(response, "x-ratelimit-limit");
            var used = GetHttpHeaderValue(response, "x-ratelimit-used");
            var remaining = GetHttpHeaderValue(response, "x-ratelimit-remaining");

            Log.Trace($"CoinApiDataQueueHandler.TraceRestUsage(): Used {used}, Remaining {remaining}, Total {total}");
        }

        private string GetHttpHeaderValue(IRestResponse response, string propertyName)
        {
            return response.Headers
                .FirstOrDefault(x => x.Name == propertyName)?
                .Value.ToString();
        }

        // WARNING: here to be called from tests to reduce explicitly the amount of request's output 
        protected void SetUpHistDataLimit(int limit)
        {
            HistoricalDataPerRequestLimit = limit;
        }
    }
}
