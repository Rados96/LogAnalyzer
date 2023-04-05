using System;
using System.Collections.Concurrent; //to provide thread-safe collection classes ConcurrentQueue, ConcurrentDictionary, Partitioner that should be used in multithreading
using System.Collections.Generic; // to provides generic collections (allow you to create strongly-typed collections) such as List, Dictionary...
using System.Linq; // to support query operations on collections
using System.Net; // to provide networking classes such as Dns, IPHostEntry.
using System.Threading.Tasks; //to provide parallelization (Parallel.ForEach)
using Tx.Windows; // to provide a set of classes that enable you to consume Windows event logs(W3CEvent, W3CEnumerable)

namespace Unifedpost
{
    public class Program
    {
        static void Main(string[] args)
        {
            LogAnalyzer logAnalyzer = new LogAnalyzer();

            IEnumerable<W3CEvent> file = logAnalyzer.ChoosingFile();

            // 1. Extract all client IP’s from the log file and count the number of hits per IP.
            Dictionary<string, int> clientIPs = logAnalyzer.ExtractClientIPsFromLogFile(file);

            // 3. Resolve the hostname for the IP’s (DNS lookup).
            ConcurrentQueue<KeyValuePair<string, int>> clientIPsWithHostNames = logAnalyzer.ResolveHostNamesForIPs(clientIPs);

            // 2. Sort the IP’s by the number of hits (descending
            List<KeyValuePair<string, int>> sortedClientIPsByCounts = logAnalyzer.SortClietIPsByCountsDescending(clientIPsWithHostNames);

            // Print results
            logAnalyzer.printResults(sortedClientIPsByCounts);
        }

        public class LogAnalyzer
        {
            public IEnumerable<W3CEvent> ChoosingFile()
            {
                string filePath = @"..\ex040730.log";
                string filePath2 = @"..\ex120326.log";
                bool validInput = false;

                IEnumerable<W3CEvent> file = Enumerable.Empty<W3CEvent>();

                Console.WriteLine("Which file you want to analyze? For ex040730.log type 1. For ex120326.log type 2. Your choice: ");

                while (!validInput)
                {
                    int chosenNumber = Convert.ToInt32(Console.ReadLine());

                    if (chosenNumber == 1 || chosenNumber == 2)
                    {
                        if (chosenNumber == 1) file = W3CEnumerable.FromFile(filePath);
                        if (chosenNumber == 2) file = W3CEnumerable.FromFile(filePath2);
                        validInput = true;
                    }
                    else
                    {
                        Console.WriteLine("Invalid input. Please choose 1 or 2.");
                    }
                }
                return file;
            }

            public Dictionary<string, int> ExtractClientIPsFromLogFile(IEnumerable<W3CEvent> file)
            {
                return file.GroupBy(x => x.c_ip).ToDictionary(x => x.Key, x => x.Count());
            }

            public ConcurrentQueue<KeyValuePair<string, int>> ResolveHostNamesForIPs(Dictionary<string, int> clientIPs)
            {
                ConcurrentQueue<KeyValuePair<string, int>> clientIPsWithHostnames = new ConcurrentQueue<KeyValuePair<string, int>>();
                ConcurrentDictionary<string, IPHostEntry> hostEntryCache = new ConcurrentDictionary<string, IPHostEntry>();

                var partitioner = Partitioner.Create(clientIPs);

                Parallel.ForEach(partitioner, kvp =>
                {
                    string key = kvp.Key;
                    int value = kvp.Value;
                    string resultKey;
                    IPHostEntry hostEntry;

                    if (hostEntryCache.TryGetValue(key, out hostEntry))
                    {
                        resultKey = $"{hostEntry.HostName} ({key})";
                    }
                    else
                    {
                        try
                        {
                            hostEntry = Dns.GetHostEntry(key);
                            hostEntryCache.TryAdd(key, hostEntry);
                            resultKey = $"{hostEntry.HostName} ({key})";
                        }
                        catch (Exception ex)
                        {
                            // The DNS lookup failed, so use a default result key
                            resultKey = $"(IP {key} doesn’t have a hostname)";
                        }
                    }

                    clientIPsWithHostnames.Enqueue(new KeyValuePair<string, int>(resultKey, value));
                });
                return clientIPsWithHostnames;
            }

            public List<KeyValuePair<string, int>> SortClietIPsByCountsDescending(ConcurrentQueue<KeyValuePair<string, int>> clientIPs)
            {
                return clientIPs.OrderByDescending(pair => pair.Value).ToList();
            }

            public void printResults(List<KeyValuePair<string, int>> clientIPs)
            {
                foreach (KeyValuePair<string, int> result in clientIPs)
                {
                    Console.WriteLine($"{result.Key} - {result.Value}");
                }
            }
        }
    }
}
