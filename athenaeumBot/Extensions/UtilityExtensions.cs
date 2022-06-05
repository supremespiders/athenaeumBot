using athenaeumBot.Models;
using athenaeumBot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace athenaeumBot.Extensions
{
    public static class UtilityExtensions
    {
        private static Regex _space = new Regex(@"\s+");

        public static string GetStringBetween(this string text, string start, string end)
        {
            var p1 = text.IndexOf(start, StringComparison.Ordinal) + start.Length;
            if (p1 == start.Length - 1) return null;
            var p2 = text.IndexOf(end, p1, StringComparison.Ordinal);
            if (p2 == -1) return null;
            return end == "" ? text.Substring(p1) : text.Substring(p1, p2 - p1);
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }
        public static string ReplaceInvalidChars(this string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        public static async Task Work<T2>(this List<T2> items, int maxThreads, Func<T2, Task> action)
        {
            var tasks = new List<Task>();
            int i = 0;
            var worked = 0;
            do
            {
                if (i < items.Count)
                {
                    var item = items[i];
                    Notifier.Display($"Working on {i + 1} / {items.Count}");
                    tasks.Add(action(item));
                    i++;
                }

                if (tasks.Count != maxThreads && i < items.Count) continue;
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    tasks.Remove(t);
                    await t;
                    worked++;
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error(ex.Message);
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error(e.ToString());
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }

                if (tasks.Count == 0 && i == items.Count) break;
            } while (true);

            Notifier.Display($"completed {items.Count}");
        }

        public static async Task WaitThenAddResult<T>(this List<Task<T>> tasks, List<T> results)
        {
            try
            {
                var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(t);
                results.Add(t.GetAwaiter().GetResult());
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (KnownException ex)
            {
                Notifier.Error(ex.Message);
                var t = tasks.FirstOrDefault(x => x.IsFaulted);
                tasks.Remove(t);
            }
            catch (Exception e)
            {
                Notifier.Error(e.ToString());
                var t = tasks.FirstOrDefault(x => x.IsFaulted);
                tasks.Remove(t);
            }
        }

        public static async Task<List<T>> Work<T, T2>(this List<T2> items, int maxThreads, Func<T2, Task<T>> func)
        {
            var tasks = new List<Task<T>>();
            var results = new List<T>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                Notifier.Display($"Working on {i + 1} / {items.Count}");
                tasks.Add(func(item));
                if (tasks.Count != maxThreads) continue;
                await tasks.WaitThenAddResult(results).ConfigureAwait(false);
            }

            while (tasks.Count != 0)
            {
                await tasks.WaitThenAddResult(results).ConfigureAwait(false);
            }

            Notifier.Display($"completed {items.Count}");
            return results;
        }

        public static async Task<List<T>> Work<T, T2>(this List<T2> items, int maxThreads, Func<T2, Task<List<T>>> func)
        {
            var tasks = new List<Task<List<T>>>();
            var results = new List<T>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                // if (i % 100 == 0)
                Notifier.Display($"Working on {i + 1} / {items.Count}");
                tasks.Add(Task.Run(() => func(item)));
                if (tasks.Count == maxThreads)
                {
                    try
                    {
                        var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                        results.AddRange(t.GetAwaiter().GetResult());
                        tasks.Remove(t);
                    }
                    catch (TaskCanceledException)
                    {
                        throw;
                    }
                    catch (KnownException ex)
                    {
                        Notifier.Error(ex.Message);
                        var t = tasks.FirstOrDefault(x => x.IsFaulted);
                        tasks.Remove(t);
                    }
                    catch (Exception e)
                    {
                        Notifier.Error(e.ToString());
                        var t = tasks.FirstOrDefault(x => x.IsFaulted);
                        tasks.Remove(t);
                    }
                }
            }

            while (tasks.Count != 0)
            {
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    results.AddRange(t.GetAwaiter().GetResult());
                    tasks.Remove(t);
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error(ex.Message);
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error(e.ToString());
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
            }

            Notifier.Display($"completed {items.Count}");
            return results;
        }

        public static DateTime UnixTimeStampToDateTime(this long unixTimeStamp)
        {
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddMilliseconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }

        public static string NodeText(this HtmlDocument doc, string xpath)
        {
            return doc.DocumentNode.SelectSingleNode(xpath)?.InnerText;
        }

        public static void Save<T>(this List<T> items, string path = null)
        {
            var name = typeof(T).Name;
            if (path != null) name = path;
            File.WriteAllText(name, JsonConvert.SerializeObject(items));
        }

        public static List<T> Load<T>(this string path)
        {
            return JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path));
        }

        public static async Task<List<T>> Scrape<T>(this IReadOnlyList<string> inputs, Func<string, Task<T>> work) where T : IWebItem
        {
            var name = typeof(T).Name;
            var outputs = new List<T>();
            if (File.Exists(name))
                outputs = name.Load<T>();
            if (outputs == null) throw new KnownException($"Null output on file");
            var collected = outputs.Select(x => x.Url).ToHashSet();
            var remainingInputs = inputs.ToHashSet();
            remainingInputs.RemoveWhere(x => collected.Contains(x));
            Notifier.Display("Start working");

            for (var i = 0; i < remainingInputs.Count; i++)
            {
                var input = inputs[i];
                Notifier.Progress(i + 1, inputs.Count);
                Notifier.Display($"Working on {i + 1} / {inputs.Count}");
                try
                {
                    outputs.Add(await work(input));
                }
                catch (TaskCanceledException)
                {
                    outputs.Save(name);
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error(ex.Message);
                }
                catch (Exception e)
                {
                    Notifier.Error(e.ToString());
                }
            }

            Notifier.Display("Work completed");
            return outputs;
        }


        private static async Task<List<T>> LoopTasks<T>(this IReadOnlyList<string> inputs, List<T> outputs, int threads, Func<string, Task<T>> work)
        {
            var name = typeof(T).Name;
            Notifier.Display("Start working");
            var i = 0;
            var taskUrls = new Dictionary<int, string>();
            var tasks = new List<Task<T>>();
            do
            {
                if (i < inputs.Count)
                {
                    var item = inputs[i];
                    Notifier.Display($"Working on {i + 1} / {inputs.Count} , Total collected : {outputs.Count}");
                    var t = work(item);
                    taskUrls.Add(t.Id, item);
                    tasks.Add(t);
                    i++;
                }

                if (tasks.Count != threads && i < inputs.Count) continue;
                var currentTaskId = -1;
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    currentTaskId = t.Id;
                    tasks.Remove(t);
                    outputs.Add(await t);
                }
                catch (TaskCanceledException e)
                {
                    outputs.Save(name);
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{ex.Message}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{e}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }

                if (tasks.Count == 0 && i == inputs.Count) break;
            } while (true);

            outputs.Save(name);
            Notifier.Display("Work completed");
            return outputs;
        }

        private static async Task<List<T>> LoopTasks<T>(this IReadOnlyList<string> inputs, List<T> outputs, int threads, Func<string, Task<List<T>>> work)
        {
            var name = typeof(T).Name;
            if (name == "String") name = "URLS";
            Notifier.Display("Start working");
            var i = 0;
            var taskUrls = new Dictionary<int, string>();
            var tasks = new List<Task<List<T>>>();
            do
            {
                if (i < inputs.Count)
                {
                    var item = inputs[i];
                    Notifier.Display($"Working on {i + 1} / {inputs.Count} , Total collected : {outputs.Count}");
                    var t = work(item);
                    taskUrls.Add(t.Id, item);
                    tasks.Add(t);
                    i++;
                }

                if (tasks.Count != threads && i < inputs.Count) continue;
                var currentTaskId = -1;
                try
                {
                    var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                    currentTaskId = t.Id;
                    tasks.Remove(t);
                    outputs.AddRange(await t);
                }
                catch (TaskCanceledException e)
                {
                    outputs.Save(name);
                    throw;
                }
                catch (KnownException ex)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{ex.Message}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }
                catch (Exception e)
                {
                    Notifier.Error($"{taskUrls[currentTaskId]}\n{e}");
                    var t = tasks.FirstOrDefault(x => x.IsFaulted);
                    tasks.Remove(t);
                }

                if (tasks.Count == 0 && i == inputs.Count) break;
            } while (true);

            outputs.Save(name);
            Notifier.Display("Work completed");
            return outputs;
        }

        public static async Task<List<T>> ScrapeParallel<T>(this IReadOnlyList<string> inputs, int threads, Func<string, Task<T>> work) where T : IWebItem
        {
            var name = typeof(T).Name;
            var outputs = new List<T>();
            if (File.Exists(name))
                outputs = name.Load<T>();
            if (outputs == null) throw new KnownException($"Null output on file");
            outputs = outputs.GroupBy(x => x.Url).Select(x => x.First()).ToList();
            outputs.Save(name);
            var collected = outputs.Select(x => x.Url).ToHashSet();
            var remainingInputs = inputs.ToHashSet();
            remainingInputs.RemoveWhere(x => collected.Contains(x));
            inputs = remainingInputs.ToList();
            if (inputs.Count == 0) throw new KnownException($"No input to work on, total data : {outputs.Count}");

            return await inputs.LoopTasks(outputs, threads, work);
        }

        public static async Task<List<T>> ScrapeParallel<T>(this IReadOnlyList<string> inputs, int threads, Func<string, Task<List<T>>> work) where T : IWebItem
        {
            var name = typeof(T).Name;
            var outputs = new List<T>();
            if (File.Exists(name))
                outputs = name.Load<T>();
            if (outputs == null) throw new KnownException($"Null output on file");
            var collected = outputs.Select(x => x.Url).ToHashSet();
            var remainingInputs = inputs.ToHashSet();
            remainingInputs.RemoveWhere(x => collected.Contains(x));
            inputs = remainingInputs.ToList();
            if (inputs.Count == 0) throw new KnownException($"No input to work on, total data : {outputs.Count}");

            return await inputs.LoopTasks(outputs, threads, work);
        }

        public static async Task<List<string>> ScrapeUrlsParallel(this IReadOnlyList<string> inputs, int threads, Func<string, Task<List<string>>> work)
        {
            var name = "URLS";
            var outputs = new List<string>();
            if (File.Exists(name))
                outputs = name.Load<string>();
            if (outputs == null) throw new KnownException($"Null output on file");
            outputs = outputs.Distinct().ToList();
            outputs.Save(name);
            var collected = outputs.ToHashSet();
            var remainingInputs = inputs.ToHashSet();
            remainingInputs.RemoveWhere(x => collected.Contains(x));
            inputs = remainingInputs.ToList();
            if (inputs.Count == 0) throw new KnownException($"No input to work on, total data : {outputs.Count}");

            return await inputs.LoopTasks(outputs, threads, work);
        }

        public static string Clean(this string s)
        {
            return _space.Replace(WebUtility.HtmlDecode(s).Replace("\n", "").Replace("\r", "").Trim(), " ");
        }
    }
}