using System.Text.Json;
using System.Text.Json.Serialization;
using RazorEngineCore;

namespace QuestReader
{
    public record ThreadPost
    {
        public int Id { get; set; }
        public string Author { get; set; }
        public string Uid { get; set; }
        public string RawHtml { get; set; }
        public string? File { get; set; }
        public string? Filename { get; set; }
        public string? Title { get; set; }
        public string? Tripcode { get; set; }
        public DateTime Date { get; set; }

        [JsonIgnore]
        public bool IsChapterAnnounce { get; set; } = false;
        public ChapterMetadata? Chapter { get; set; }
    }

    public record Metadata
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public Uri AuthorPage { get; set; }
        public string? AuthorTwitter { get; set; }
        public Uri AssetsBaseUrl { get; set; }
        public string SocialPreview { get; set; }
        public List<int> Threads { get; set; }
        public List<ChapterMetadata> Chapters { get; set; }

        public override string ToString() => $"\"{Name}\" by {Author}";
    }
    public record ChapterMetadata
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Subtitle { get; set; }
        public int Start { get; set; }
        public int? Announce { get; set; }
        public int End { get; set; }
    }

    public enum ParamType
    {
        Invalid,
        PostId,
        UniqueId,
        Username
    }

    public enum ParamError
    {
        Invalid,
        NoError,
        NotFound
    }

    public class Program
    {

        public static async Task Main(string[] args)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            if (args.Length < 1) {
                Console.WriteLine("Missing quest name, provide it as the first argument");
            }

            var basePath = $"quests/{args[0]}";

            using var fileStream = File.OpenRead(Path.Combine(basePath, "metadata.json"));
            var metadata = JsonSerializer.Deserialize<Metadata>(fileStream, options)
                    ?? throw new InvalidDataException("Empty deserialisation result for quest metadata");
            fileStream.Dispose();

            Console.Out.WriteLine($"Loaded metadata: {metadata}");
            var allPosts = metadata.Threads.SelectMany(tId => {
                using var fileStream = File.OpenRead(Path.Combine(basePath, $"thread_{tId}.json"));
                var threadData = JsonSerializer.Deserialize<List<ThreadPost>>(fileStream, options)
                    ?? throw new InvalidDataException("Empty deserialisation result for thread data");
                fileStream.Dispose();

                return threadData;
            });

            var byAuthor = allPosts.GroupBy(p => p.Author).OrderBy(g => g.Count()).ToList();

            if (args.Length < 1 || args[1] != "skip") {
                for (int i = 0; i < byAuthor.Count; i++)
                {
                    var item = byAuthor[i];
                    Console.Out.WriteLine($"[{byAuthor.Count - i, 2}] User \"{item.Key}\" has {item.Count()} posts with {item.Where(p => p.File is not null).Count()} files");
                }
                Console.Write("\nType #s of the author entry (split by ,) or <skip> to load a saved list: ");
            }
            var inputRaw = args.Length >= 1 && args[1] == "skip" ? "skip" : Console.ReadLine() ?? throw new Exception("AAAA");
            var accepted = new List<ThreadPost>();
            if (inputRaw != "skip") {
                var authors = inputRaw.Split(",").Select(i => byAuthor[^(int.Parse(i.Trim()))].Key);
                accepted = ProcessLoop(allPosts, authors);

                if (!accepted.Any()) {
                    Console.Out.WriteLine("\nNothing accepted, nothing written. Exiting");
                    return;
                }
                Console.Out.WriteLine("");
                using var writer = File.OpenWrite(Path.Join(basePath, $"accepted.json"));
                JsonSerializer.Serialize(writer, accepted.Select(i => i.Id).OrderBy(i => i).ToList(), options);
                writer.Dispose();
                Console.Out.WriteLine($"Successfully wrote {accepted.Count} ids, referencing {accepted.Where(a => a.File is not null).Count()} files");
            } else {
                using var postsListStream = File.OpenRead(Path.Combine(basePath, "accepted.json"));
                var ids = JsonSerializer.Deserialize<List<int>>(postsListStream, options)
                        ?? throw new InvalidDataException("Empty deserialisation result for quest metadata");
                accepted = allPosts.Where(p => ids.Contains(p.Id)).ToList();
                Console.Out.WriteLine($"Loaded a list of {accepted.Count} posts, referencing {accepted.Where(a => a.File is not null).Count()} files");
            }

            Console.Write("\nContinue? [yes/NO]: ");
            if ((args.Length < 1 || args[1] != "skip") && Console.ReadLine() != "yes")
                return;

            Console.Out.WriteLine("");

            if (!Directory.Exists(Path.Join(basePath, "assets")))
                Directory.CreateDirectory(Path.Join(basePath, "assets"));

            var files = accepted
                .Where(a => a.File is not null)
                .Select(a => (a.File, a.Filename))
                .ToList();

            var downloadTasks = new List<Task>();

            using (var client = new HttpClient()) {
                client.BaseAddress = metadata.AssetsBaseUrl;
                Console.Out.WriteLine($"Downloading missing files...");

                await Task.WhenAll(
                    files
                        .Where(file => !File.Exists(Path.Join(basePath, "assets", file.File)))
                        .Select(file => client.GetStreamAsync(file.File).ContinueWith(async (stream) => {
                                Console.Out.WriteLine($"Downloading {file.File} (aka \"{file.Filename}\")");
                                using var fileWrite = File.OpenWrite(Path.Join(basePath, "assets", file.File));
                                await (await stream).CopyToAsync(fileWrite);
                            })
                        )
                );
                Console.Out.WriteLine($"All files done");
            }

            var chapterAnnounces = metadata.Chapters.Select(c => c.Announce ?? c.Start);
            accepted.Where(p => chapterAnnounces.Contains(p.Id)).ToList().ForEach(p => {
                p.IsChapterAnnounce = true;
                p.Chapter = metadata.Chapters.Single(c => (c.Announce ?? c.Start) == p.Id);
            });

            var razorEngine = new RazorEngine();
            var templateFile = "page_template.cshtml";
            var baseUrl = "";
            var template = razorEngine.Compile<HtmlSafeTemplate>(File.ReadAllText("page_template.cshtml"));

            Console.WriteLine($"Using \"{templateFile}\" with base URL {baseUrl}");
            string result = template.Run(instance =>
            {
                instance.Model = new AnonymousTypeWrapper(new
                {
                    Metadata = metadata,
                    Posts = accepted,
                    Now = @DateTime.UtcNow,
                    //BaseUrl = "assets"
                    BaseUrl = $"/static/{args[0]}"
                });
            });

            Console.WriteLine($"Template output {result.Length} bytes");
            File.WriteAllText(Path.Join(basePath, "output.html"), result);
        }

        public static List<ThreadPost> ProcessLoop(IEnumerable<ThreadPost> allPosts, IEnumerable<string> authors)
        {

            /*
                Add to processing queue:
                    q/queue #uid
                    q/queue @name

                a/accept #uid
                a/accept @name
                a/accept #uid >### - accept with post count more than #

                i/inspect #uid
                i/inspect 12345
                i/inspect @name

                Drop _only from queue_
                    d/del/delete/drop #uid
                    d/del/delete/drop name

                Remove from accepted
                    r/rm/remove #uid
                    r/rm/remove name

                Status/list:
                    s/l
                    l uid(s)
                    l user(s)/name(s)
                    l accepted

                q/quit
                h/help this
            */

            var accepted = new List<ThreadPost>();
            var processing = allPosts.Where(p => authors.Contains(p.Author)).ToHashSet();

            Console.WriteLine($"\nProcesing {processing.Count} posts");
            {
                var grouped = processing.GroupBy(p => new { p.Uid, p.Author }).OrderBy(g => g.Count());
                foreach (var item in grouped)
                {
                    Console.Out.WriteLine($"#{item.Key.Uid} - \"{item.Key.Author}\": {item.Count()} posts");
                }
            }

            var loopedWhenEmpty = false;
            while (!loopedWhenEmpty || processing.Any()) {
                loopedWhenEmpty = !processing.Any();
                if (loopedWhenEmpty) {
                    loopedWhenEmpty = true;
                    Console.Out.WriteLine("NOTE: No more entries left in queue. This is the last loop unless you run a <list>, <inspect>, or <add> new entries!");
                }

                Console.Write("> ");
                var loopInput = Console.ReadLine();
                if (loopInput is null)
                    break;
                loopInput = loopInput.Trim();
                if (loopInput == "quit") {
                    break;
                }

                var noParam = !loopInput.Contains(' ', StringComparison.InvariantCulture);
                var paramType = ParamType.Invalid;
                var paramError = ParamError.NoError;

                if (loopInput.StartsWith("i ") || loopInput.StartsWith("inspect ")) {
                    loopedWhenEmpty = false;
                    var affected = HandleParams(processing, loopInput, out paramType, out paramError);
                    if (CheckError(paramType, paramError))
                        continue;

                    Console.Out.WriteLine($"{affected.Count} items");
                    foreach (var item in affected) {
                        var fileDesc = item.File is not null ? $" - File {item.Filename}" : "";
                        Console.Out.WriteLine($"{item.Id} - {item.Date: s} - {item.Author} - #{item.Uid}{fileDesc}\n\n{item.RawHtml}\n---");
                    }
                    continue;
                }

                if (loopInput.StartsWith("a ") || loopInput.StartsWith("add ") || loopInput.StartsWith("accept ")) {
                    var affected = new List<ThreadPost>();
                    if (loopInput == "a all" || loopInput == "add all" || loopInput == "accept all")
                        affected = processing.ToList();
                    else
                        affected = HandleParams(processing, loopInput, out paramType, out paramError);
                    if (CheckError(paramType, paramError))
                        continue;

                    Console.Out.WriteLine($"{affected.Count} items");
                    processing.RemoveWhere(i => affected.Contains(i));
                    accepted.AddRange(affected);
                    continue;
                }

                if (loopInput.StartsWith("d ") || loopInput.StartsWith("del ") || loopInput.StartsWith("delete ") || loopInput.StartsWith("drop ")) {
                    var affected = HandleParams(processing, loopInput, out paramType, out paramError);
                    if (CheckError(paramType, paramError))
                        continue;

                    Console.Out.WriteLine($"{affected.Count} items");
                    processing.RemoveWhere(i => affected.Contains(i));
                    continue;
                }

                if (loopInput.StartsWith("q ") || loopInput.StartsWith("queue ")) {
                    loopedWhenEmpty = false;
                    var affected = new List<ThreadPost>();
                    if (loopInput == "q #" || loopInput == "queue #")
                        affected = allPosts.Where(p => processing.Any(pr => pr.Uid == p.Uid)).ToList();
                    else
                        affected = HandleParams(allPosts, loopInput, out paramType, out paramError);

                    if (CheckError(paramType, paramError))
                        continue;

                    Console.Out.WriteLine($"{affected.Count} items");
                    processing.UnionWith(affected);
                    continue;
                }

                if (noParam && (loopInput.StartsWith("l") || loopInput.StartsWith("list"))) {
                    var grouped = processing.GroupBy(p => new { p.Uid, p.Author }).OrderBy(g => g.Count());
                    foreach (var item in grouped)
                    {
                        Console.Out.WriteLine($"#{item.Key.Uid} - \"{item.Key.Author}\": {item.Count()} posts");
                    }
                    continue;
                }

                if (loopInput.StartsWith("l ") || loopInput.StartsWith("list ")) {
                    var split = loopInput.Split(" ");
                    switch (split[1])
                    {
                        case "uid":
                        case "uids": {
                            var grouped = allPosts.GroupBy(p => new { p.Uid }).OrderBy(g => g.Count());
                            foreach (var item in grouped)
                            {
                                Console.Out.WriteLine($"#{item.Key.Uid}: {item.Count()} posts {item.Where(i => i.File is not null).Count()} files");
                            }
                            break;
                        }

                        case "user":
                        case "users":
                        case "author":
                        case "authors":
                        case "name":
                        case "names":
                        case "username":
                        case "usernames": {
                            var grouped = allPosts.GroupBy(p => new { p.Author }).OrderBy(g => g.Count());
                            foreach (var item in grouped)
                            {
                                Console.Out.WriteLine($"\"{item.Key.Author}\": {item.Count()} posts with {item.Where(i => i.File is not null).Count()} files");
                            }
                            break;
                        }

                        case "a":
                        case "accepted": {
                            var grouped = accepted.GroupBy(p => new { p.Author, p.Uid }).OrderBy(g => g.Count());
                            foreach (var item in grouped)
                            {
                                Console.Out.WriteLine($"\"{item.Key.Author}\" - #{item.Key.Uid}: {item.Count()} posts with {item.Where(i => i.File is not null).Count()} files");
                            }
                            break;
                        }


                        default:
                            break;
                    }
                    continue;
                }
            }

            return accepted;
        }

        public static bool CheckError(ParamType paramType, ParamError paramError)
        {
            if (paramError == ParamError.NoError)
                return false;
            Console.Out.WriteLine($"Error {paramError} for param {paramType}");
            return true;
        }

        public static List<ThreadPost> HandleParams(IEnumerable<ThreadPost> targetList, string input, out ParamType paramType, out ParamError paramError, int skip = 1)
        {
            var output = new List<ThreadPost>();
            var split = input.Split(" ").Skip(skip);
            paramType = ParamType.Invalid;
            paramError = ParamError.Invalid;

            foreach (var param in split)
            {
                if (param.StartsWith("#")) {
                    paramType = ParamType.UniqueId;
                    paramError = ParamError.NoError;
                    output.AddRange(targetList.Where(p => p.Uid == param[1..]));
                    if (!output.Any())
                        paramError = ParamError.NotFound;
                } else if (param.StartsWith("@")) {
                    paramType = ParamType.Username;
                    paramError = ParamError.NoError;
                    var name = param[1..];
                    output.AddRange(targetList.Where(p => p.Author == name));
                    if (!output.Any())
                        paramError = ParamError.NotFound;
                } else if (int.TryParse(param, out var postId)) {
                    paramType = ParamType.PostId;
                    paramError = ParamError.NoError;
                    output.AddRange(targetList.Where(p => p.Id == postId));
                    if (!output.Any())
                        paramError = ParamError.NotFound;
                }

                if (paramError != ParamError.NoError) {
                    return null!;
                }
            }


            return output.OrderBy(i => i.Id).ToList();
        }
    }
}