using QuantConnect.Logging;
using System.Globalization;
using QuantConnect.Configuration;

namespace QuantConnect.CoinAPI.Converter
{
    internal class Program
    {
        public static string DataFleetDeploymentDate = "QC_DATAFLEET_DEPLOYMENT_DATE";

        private static void Main(string[] args)
        {
            var rawDataFolder = new DirectoryInfo(Config.Get("raw-data-folder", "/raw"));
            var temporaryOutputDirectory = Config.Get("temp-output-directory", "/temp-output-directory");
            // Allow for caller to specify which market they want to process
            var market = args.Length != 0 && !string.IsNullOrWhiteSpace(args[0])
                ? args[0]
                : null;

            var securityType = SecurityType.Crypto;
            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                securityType = (SecurityType)Enum.Parse(typeof(SecurityType), args[1], true);
            }

            Environment.SetEnvironmentVariable(DataFleetDeploymentDate, "20240211");

            var processingDateValue = Environment.GetEnvironmentVariable(DataFleetDeploymentDate);
            var processingDate = DateTime.ParseExact(processingDateValue, "yyyyMMdd", CultureInfo.InvariantCulture);

            Log.Trace($"Price.Crypto.CoinApi.Main(): Processing {processingDate} for market: {market ?? "*"} SecurityType: {securityType}");

            var converter = new CoinApiDataConverter(processingDate, rawDataFolder.FullName, temporaryOutputDirectory, market, securityType);

            if (!converter.Run())
            {
                Log.Error($"Price.Crypto.CoinApi.Main(): Processing CoinAPI for date {processingDate:yyyy-MM-dd} failed");
                Environment.Exit(1);
            }

            Environment.Exit(0);
        }
    }
}