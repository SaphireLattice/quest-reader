namespace QuestReader.Services;

public class FileDownloader
{
    public static async Task DownloadList(string basePath, Uri baseAssetsUrl, IEnumerable<string> files)
    {
        if (!Directory.Exists(Path.Join(basePath, "assets")))
            Directory.CreateDirectory(Path.Join(basePath, "assets"));

        var downloadTasks = new List<Task>();

        using (var client = new HttpClient()) {
            client.BaseAddress = baseAssetsUrl;
            Console.Out.WriteLine($"Downloading missing files...");

            await Task.WhenAll(
                files
                    .Where(file => !File.Exists(Path.Join(basePath, "assets", file)))
                    .Select(file => client.GetStreamAsync(file).ContinueWith(async (stream) => {
                            Console.Out.WriteLine($"Downloading {file}");
                            using var fileWrite = File.OpenWrite(Path.Join(basePath, "assets", file));
                            await (await stream).CopyToAsync(fileWrite);
                            stream.Dispose();
                            fileWrite.Dispose();
                        })
                    )
                    .ToList()
            );
            Console.Out.WriteLine($"All files done");
        }
    }
}
