using Microsoft.VisualBasic;
using Spectre.Console;
using System;
using System.Collections.Concurrent;
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

        static bool IsFermatProbablePrime(BigInteger number, ProgressTask task, int k = 5)
        {
            if (number <= 1)
            {
                return false;
            }

            if (number <= 3)
            {
                return true;
            }
            var stepSize = (double)1 / (double)k;
            Random random = new Random();
            for (int i = 0; i < k; i++)
            {
                BigInteger a = random.NextBigInteger(2, number - 1);
                if (BigInteger.ModPow(a, number - 1, number) != 1)
                {
                    task.Increment(1);
                    return false;
                }
                task.Increment(stepSize);
            }
            return true;
        }

        static void Main(string[] args)
        {
            var saveFolder = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\";
            // Check for todo && done files

            var minExponent = 1;
            var maxExponent = 0;
            var maxCoresDefault = Environment.ProcessorCount;
            var coresToUse = maxCoresDefault;
            var numberOfFermatTests = 0;
            var range = new List<int>();
            var doneExponents = new ConcurrentBag<int>();
            if (File.Exists(saveFolder + "todo.txt"))
            {
                AnsiConsole.Status()
                    .AutoRefresh(true)
                    .Spinner(Spinner.Known.Default)
                    .Start("Found existing work, parsing...", ctx =>
                    {
                        if (File.Exists(saveFolder + "done.txt"))
                        {
                            using (TextReader tr = new StreamReader(saveFolder + "done.txt"))
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

                        using (TextReader tr = new StreamReader(saveFolder + "todo.txt"))
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
                using (TextWriter tw = new StreamWriter(saveFolder + "todo.txt", false))
                {
                    foreach (var item in range)
                    {
                        tw.WriteLine(item);
                    }
                }
            }
            Console.WriteLine("Probable Mersenne Prime Numbers:");
            doneExponents.Clear();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = coresToUse,                
            };

            AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                    new TaskDescriptionColumn(),    // Task description
                    new ProgressBarColumn(),        // Progress bar
                    new SpinnerColumn(),            // Spinner
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
                    BigInteger mersenneExponent = BigInteger.Pow(2, p) - 1;
                    var subTask = ctx.AddTask($"Testing 2^{p}-1");
                    subTask.IsIndeterminate = true;
                    subTask.MaxValue = 1;
                    if (IsFermatProbablePrime(mersenneExponent, subTask, numberOfFermatTests))
                    {
                        AnsiConsole.MarkupLine($"[red]2^{p} - 1 is probable prime![/]");
                        WriteLineToFile(p.ToString() + "  --  " + mersenneExponent.ToString(), "found.txt");
                    }
                    subTask.Increment(1);
                    WriteLineToFile(p.ToString(), "done.txt");
                    mainTask.Increment(totalRangeStep);
                });

                File.Delete(saveFolder + "todo.txt");
                File.Delete(saveFolder + "done.txt");
            });
            AnsiConsole.Markup("[bold green]Done! Press a key to finish.[/] ");
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
