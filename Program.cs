using Microsoft.VisualBasic;
using Spectre.Console;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Primer
{
    class Program
    {
        static object fileLock = true;

        static bool IsFermatProbablePrime(BigInteger number, int k = 5)
        {
            if (number <= 1)
            {
                return false;
            }

            if (number <= 3)
            {
                return true;
            }

            if (number % 2 == 0)
            {
                return false;
            }

            Random random = new Random();

            for (int i = 0; i < k; i++)
            {
                BigInteger a = random.NextBigInteger(2, number - 1);
                if (BigInteger.ModPow(a, number - 1, number) != 1)
                {
                    return false;
                }
            }
            return true;
        }

        static void Main(string[] args)
        {
            var saveFolder = Environment.CurrentDirectory + "\\";
            var todoFilePath = saveFolder + "PrimerTodo.txt";
            var doneFilePath = saveFolder + "PrimerDone.txt";
            var foundFilePath = saveFolder + "PrimerFound.txt";
            var minExponent = 1;
            var maxExponent = 0;
            var maxCoresDefault = Environment.ProcessorCount;
            var coresToUse = maxCoresDefault;
            var numberOfFermatTests = 0;
            var range = new List<int>();
            var doneExponents = new ConcurrentBag<int>();
            // Check && see if we need to pick up where we left off
            if (File.Exists(todoFilePath))
            {
                AnsiConsole.Status()
                    .AutoRefresh(true)
                    .Spinner(Spinner.Known.Default)
                    .Start("Found existing work, parsing...", ctx =>
                    {
                        if (File.Exists(doneFilePath))
                        {
                            using (TextReader tr = new StreamReader(doneFilePath))
                            {
                                while (tr.Peek() > 0)
                                {
                                    if (int.TryParse(tr.ReadLine(), out var lineVal))
                                    {
                                        doneExponents.Add(lineVal);
                                    }
                                }
                            }
                        }

                        using (TextReader tr = new StreamReader(todoFilePath))
                        {
                            while (tr.Peek() > 0)
                            {
                                if (int.TryParse(tr.ReadLine(), out var lineVal))
                                {
                                    if (!doneExponents.Any(a => a == lineVal))
                                    {
                                        range.Add(lineVal);
                                    }
                                }
                            }
                        }
                    });

                Console.WriteLine("Confirm options before resuming...");
                do
                {
                    coresToUse = AnsiConsole.Ask<int>($"Enter the number of cores to use ({maxCoresDefault}):", maxCoresDefault);
                    numberOfFermatTests = AnsiConsole.Ask<int>($"Number of Fermat tests to use (5):", 5);
                }
                while ((coresToUse <= 0) || (coresToUse > maxCoresDefault) || (numberOfFermatTests <= 0));
            }
            else
            {
                do
                {
                    minExponent = AnsiConsole.Ask<int>("Enter the [green]minimum[/] exponent:", 105);
                    maxExponent = AnsiConsole.Ask<int>("Enter the [red]maximum[/] exponent:", 610);
                    maxCoresDefault = Environment.ProcessorCount;
                    coresToUse = AnsiConsole.Ask<int>($"Enter the number of cores to use ({maxCoresDefault}):", maxCoresDefault);
                    numberOfFermatTests = AnsiConsole.Ask<int>($"Number of Fermat tests to use (5):", 5);
                }
                while ((minExponent < 2) || (maxExponent < minExponent) || (coresToUse <= 0) || (coresToUse > maxCoresDefault) || (numberOfFermatTests <= 0));

                range = Enumerable.Range(minExponent, maxExponent - minExponent + 1).ToList();
                // Save list
                using (TextWriter tw = new StreamWriter(todoFilePath, false))
                {
                    foreach (var item in range)
                    {
                        tw.WriteLine(item);
                    }
                }
            }

            // Clean up memory before we start
            doneExponents.Clear();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = coresToUse,                
            };
            Console.WriteLine("Probable Mersenne Prime Numbers:");
            AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                    new TaskDescriptionColumn(),    
                    new ProgressBarColumn(),        
                    new SpinnerColumn(),            
            })
            .AutoClear(true)
            .HideCompleted(true)            
            .Start(ctx =>
            {
                var mainTask = ctx.AddTask("[green]Total Progress[/]");
                mainTask.MaxValue = 1;
                var totalRangeStep = (double)1 / (double)range.Count;

                Parallel.ForEach(range, parallelOptions, p =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    BigInteger mersenneExponent = BigInteger.Pow(2, p) - 1;
                    var subTask = ctx.AddTask($"Testing 2^{p}-1");
                    subTask.IsIndeterminate = true;
                    subTask.MaxValue = 1;
                    if (IsFermatProbablePrime(mersenneExponent, numberOfFermatTests))
                    {
                        var timeElapsed = sw.Elapsed.ToString(@"dd\.hh\:mm\:ss\.ff");
                        AnsiConsole.MarkupLine($"[red]2^{p} - 1 is probable prime![/]");
                        WriteLineToFile($"{p}  --  {mersenneExponent} -- Time to test: {timeElapsed}", foundFilePath);
                    }
                    subTask.Increment(1);
                    WriteLineToFile(p.ToString(), doneFilePath);
                    mainTask.Increment(totalRangeStep);
                });

                File.Delete(todoFilePath);
                File.Delete(doneFilePath);
            });
            AnsiConsole.MarkupLine($"[bold green]Done! Results written to: {foundFilePath}[/] ");
            Console.WriteLine("Press a key to exit");
            Console.ReadKey();
        }

        static void WriteLineToFile(string line, string target)
        {
            lock (fileLock)
            {
                using (TextWriter tw = new StreamWriter(target, true))
                {
                    tw.WriteLine(line);
                }
            }
        }
    }


    static class RandomExtensions
    {
        public static BigInteger NextBigInteger(this Random random, BigInteger minValue, BigInteger maxValue)
        {
            byte[] bytes = (maxValue - minValue).ToByteArray();
            BigInteger result;
            do
            {
                random.NextBytes(bytes);
                bytes[bytes.Length - 1] &= (byte)0x7F; // Ensure positive number
                result = new BigInteger(bytes);
            }
            while (result >= maxValue || result < minValue);

            return result;
        }
    }
}
