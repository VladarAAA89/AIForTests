// TESTS
//   dotnet run -- "DO you work&"
//   dotnet run
//   dotnet run -- -s "first" "have u recived prev post zapros if ye return it"

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AskAi
{
    internal class Program
    {
        private const string BaseUrl = "https://text.pollinations.ai/";
        private const int MaxUrlLength = 7000; // max api post zapros length

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 AskAi-CSharp");
            return client;
        }

        private static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string system = null;
            var words = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "-s" || args[i] == "--system") && i + 1 < args.Length)
                    system = args[++i];
                else
                    words.Add(args[i]);
            }

            if (words.Count > 0)
            {
                await PrintAnswer(string.Join(" ", words), system);
                return;
            }
            
            while (true)
            {
                Console.Write("\nВы: ");
                string prompt = Console.ReadLine();
                if (prompt != null) prompt = prompt.Trim();
                if (string.IsNullOrEmpty(prompt)) break;
                await PrintAnswer(prompt, system);
            }
        }

        private static async Task PrintAnswer(string prompt, string system)
        {
            try
            {
                string answer = await Ask(prompt, system);
                Console.WriteLine("\nAns: " + answer);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Err: " + e.Message);
            }
        }
        public static async Task<string> Ask(string prompt, string system = null, int retries = 3)
        {
            string url = BaseUrl + Uri.EscapeDataString(prompt);
            if (system != null)
                url += "?system=" + Uri.EscapeDataString(system);

            if (url.Length > MaxUrlLength)
                throw new Exception("Too long prompt" + url.Length + " > " + MaxUrlLength);

            for (int attempt = 1; attempt <= retries; attempt++)
            {
                using (HttpResponseMessage resp = await Http.GetAsync(url))
                {
                    if (resp.IsSuccessStatusCode)
                        return (await resp.Content.ReadAsStringAsync()).Trim();

                    // 402/429 = Free lim exc
                    bool limited = resp.StatusCode == HttpStatusCode.PaymentRequired || (int)resp.StatusCode == 429;
                    if (limited && attempt < retries)
                    {
                        double wait = 30;
                        if (resp.Headers.RetryAfter != null && resp.Headers.RetryAfter.Delta.HasValue)
                            wait = resp.Headers.RetryAfter.Delta.Value.TotalSeconds;

                        Console.Error.WriteLine("LIMIT" + wait.ToString("0"));
                        await Task.Delay(TimeSpan.FromSeconds(wait + 1));
                        continue;
                    }

                    string body = await resp.Content.ReadAsStringAsync();
                    if (body.Length > 300) body = body.Substring(0, 300);
                    throw new Exception("HTTP " + (int)resp.StatusCode + ": " + body);
                }
            }

            throw new Exception("Cant rec answ");
        }
    }
}