using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

class Program
{
    static async Task Main(string[] args)
    {
        string cookie = args[0];
        const string debugServerUrl = "http://localhost:8090";
        const string getFoldersUrl = "https://www.onlinegdb.com/myfiles/folders";

        using var client = new HttpClient();
        
        // Set up headers (in English comments)
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:134.0) Gecko/20100101 Firefox/134.0");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        client.DefaultRequestHeaders.Add("Referer", "https://www.onlinegdb.com/");
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        try
        {
            // First request: get the folder list
            try
            {
                await client.GetAsync(debugServerUrl);
            }
            catch (Exception e)
            {
                Console.WriteLine("Debug server not running.");
            }

            HttpResponseMessage folderResponse = await client.GetAsync(getFoldersUrl);
            folderResponse.EnsureSuccessStatusCode();

            using var folderStream = await folderResponse.Content.ReadAsStreamAsync();
            Stream decompressedFolderStream = folderStream;

            // Decompress response if needed
            if (folderResponse.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                decompressedFolderStream = new GZipStream(folderStream, CompressionMode.Decompress);
            }
            else if (folderResponse.Content.Headers.ContentEncoding.Contains("br"))
            {
                decompressedFolderStream = new BrotliStream(folderStream, CompressionMode.Decompress);
            }

            using var folderReader = new StreamReader(decompressedFolderStream, Encoding.UTF8);
            string folderResponseBody = await folderReader.ReadToEndAsync();

            // Parse JSON and find the target folder by its name
            var folderData = JsonSerializer.Deserialize<ResponseData>(folderResponseBody);
            folderData?.Data?.ForEach(f => Console.WriteLine(f.Text));
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error: {e.Message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unexpected error: {e.Message}");
        }
    }

    class ResponseData
    {
        [JsonPropertyName("result")]
        public string Result { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("data")]
        public List<Folder> Data { get; set; }
    }

    class Folder
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("parent")]
        public int? Parent { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    class ShareResponse
    {
        [JsonPropertyName("result")]
        public string Result { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("embedable")]
        public bool Embedable { get; set; }

        [JsonPropertyName("ts_modified")]
        public long? TsModified { get; set; }
    }
}
