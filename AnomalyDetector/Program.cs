using Azure;
using Azure.AI.AnomalyDetector;
using Azure.AI.AnomalyDetector.Models;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AnomalyDetector
{
    class Program
    {
        private static readonly AnomalyDetectorClient _anomalyClient;

        /// <summary>
        /// Ctor setting AnomalyDetectorClient
        /// Set your endPoint / apiKey from your anoamly detector azure resource 
        /// </summary>
        static Program()
        {
            // Create your own anomaly detector service in azure 
            // Set values belows

            //read endpoint and apiKey
            string endPoint = "put-your-end-point-here";
            string apiKey = "put-your-key-here";

            var endpointUri = new Uri(endPoint);
            var credential = new AzureKeyCredential(apiKey);

            _anomalyClient = new AnomalyDetectorClient(endpointUri, credential);
        }

        /// <summary>
        /// Detected anomalies within the adx_cost_change_row_count.csv
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            try
            {
                // Read data from local adx_cost_change_row_count.csv
                var datapath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Data", "adx_cost_change_row_count.csv");

                // CSV fortmat is set to time & value columns
                // Create a TimeSeriesPoint object with these properties <time, value>
                var list = File.ReadAllLines(datapath, Encoding.UTF8)
                    .Where(e => e.Trim().Length != 0)
                    .Select(e => e.Split(','))
                    .Where(e => e.Length == 2)
                    .Select(e => new TimeSeriesPoint(float.Parse(e[1])) { Timestamp = DateTime.Parse(e[0]) }).ToList()
                    ;

                // Create detection object with Granularity: Daily & Sensitivity: 25
                // Sensitivity is Between 0-99, the lower the value is, the larger the margin value will be which means less anomalies will be accepted.
                DetectRequest request = new DetectRequest(list)
                {
                    Granularity = TimeGranularity.Daily,
                    Sensitivity = 25
                };

                Console.WriteLine("Expect a total of 3 anomalies in the entire time series.");
                var totalAnomaliesInSeries = await AnomaliesInEntireSeries(request);
                Console.WriteLine($"Detected a total of {totalAnomaliesInSeries} anomalies in the entire time series.");

                Console.WriteLine("Expect an anomaly for Latest value in time series.");
                var isLatestValueAnAnomaly = await IsLatestValueAnAnomaly(request);
                Console.WriteLine($"Detected a {isLatestValueAnAnomaly } anomaly in time series.");
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine(String.Format("Entire detection failed: {0}", ex.Message));
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Detection error. {0}", ex.Message));
                throw;
            }
        }

        /// <summary>
        /// Verifies all anomalies within time series
        /// </summary>
        /// <param name="detectionRequest">Object containing time series values</param>
        /// <returns>Int: Total amount of anomalies in time series</returns>
        static async Task<int> AnomaliesInEntireSeries(DetectRequest detectionRequest)
        {
            //     This operation generates a model with an entire series, each point is detected
            //     with the same model. With this method, points before and after a certain point
            //     are used to determine whether it is an anomaly. The entire detection can give
            //     user an overall status of the time series.
            EntireDetectResponse result = await _anomalyClient.DetectEntireSeriesAsync(detectionRequest);

            // Using this EntireTimeSeries we expect 3 anomalies in this data set:
            // (2021-10-04T00:00:00Z,5208), (2021-12-16T00:00:00Z,500), (2022-01-30T00:00:00Z,1000)
            var hasAnomaly = false;
            var totalAnomalies = 0;

            for (int i = 0; i < detectionRequest.Series.Count; ++i)
            {
                if (result.IsAnomaly[i])
                {
                    Console.WriteLine("An anomaly was detected at index: {0}.", i);
                    
                    hasAnomaly = true;
                    totalAnomalies++;
                }
            }

            if (!hasAnomaly)
            {
                Console.WriteLine("No anomalies detected in the series.");
            }

            return totalAnomalies;
        }

        /// <summary>
        /// Verifies if Latest value in time series is an anomaly
        /// </summary>
        /// <param name="detectionRequest">Object containing time series values</param>
        /// <returns>bool: true if latest value is an anomaly</returns>
        static async Task<bool> IsLatestValueAnAnomaly(DetectRequest detectionRequest)
        {
            // This operation generates a model using points before the latest one. With this
            // method, only historical points are used to determine whether the target point
            // is an anomaly. The latest point detecting operation matches the scenario of real-time
            // monitoring of business metrics.
            var latestResponseResult = await _anomalyClient.DetectLastPointAsync(detectionRequest);

            // Using this LastPoint we expect an anomaly for latest value in dataset: (2022-01-30T00:00:00Z,1000)
            Console.WriteLine($"A {latestResponseResult.Value.IsAnomaly} anomaly was detected for the latest value in dataset.");

            return latestResponseResult.Value.IsAnomaly;
        }
    }
}
