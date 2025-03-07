using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

class Program
{
    static async Task Main(string[] args)
    {
        string targetFolderName = "4_rozdzial";
        string language = "c";
        string projectName = "Test";
        string cookie = args[0];
        const string getFoldersUrl = "https://www.onlinegdb.com/myfiles/folders";
        const string createProjectUrl = "https://www.onlinegdb.com/share";
        const string generateShareUrl = "https://www.onlinegdb.com/share";
        

        using (HttpClient client = new HttpClient())
        {
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
                var targetFolder = folderData?.Data?.Find(f => f.Text == targetFolderName);

                if (targetFolder == null)
                {
                    Console.WriteLine($"Folder not found: {targetFolderName}");
                    return;
                }

                Console.WriteLine($"Found folder '{targetFolderName}' with ID: {targetFolder.Id}");

                // Prepare source code payload
                var srcPayload = new[]
                {
                    new
                    {
                        name = "source code",
                        content = "#include <stdio.h>\n\nint main()\n{\n    printf(\"Marcin Olejnik\\n\");\n    return 0;\n}",
                        readonly_ranges = Array.Empty<object>()
                    }
                };

                string srcAsString = JsonSerializer.Serialize(srcPayload);

                // Prepare JSON data for creating the project in the chosen folder
                var projectData = new
                {
                    src = srcAsString,
                    stdin = "",
                    lang = language,
                    cmd_line_args = "",
                    input_method = "I",
                    type = "M",
                    replay_events = Array.Empty<object>(),
                    ss_id = (string?)null,
                    parent = targetFolder.Id,
                    name = projectName,
                    save = true,
                    uid = (string?)null
                };

                var options = new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string jsonContent = JsonSerializer.Serialize(projectData, options);

                Console.WriteLine("JSON data for creating project:");
                Console.WriteLine(jsonContent);

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // First POST request to create the project
                HttpResponseMessage createResponse = await client.PostAsync(createProjectUrl, content);
                createResponse.EnsureSuccessStatusCode();

                using var createStream = await createResponse.Content.ReadAsStreamAsync();
                Stream decompressedCreateStream = createStream;

                if (createResponse.Content.Headers.ContentEncoding.Contains("gzip"))
                {
                    decompressedCreateStream = new GZipStream(createStream, CompressionMode.Decompress);
                }
                else if (createResponse.Content.Headers.ContentEncoding.Contains("br"))
                {
                    decompressedCreateStream = new BrotliStream(createStream, CompressionMode.Decompress);
                }

                using var createReader = new StreamReader(decompressedCreateStream, Encoding.UTF8);
                string createResponseBody = await createReader.ReadToEndAsync();

                Console.WriteLine("Project creation response:");
                Console.WriteLine(createResponseBody);

                // At this point, the project is created under the correct folder,
                // but the returned UID does not give the share link. We need a second request:

                // Prepare second request data (simplified version as per your curl example)
                // Note: "parent", "save", "uid", etc. are not present here.
                var shareRequestData = new
                {
                    src = srcAsString,
                    stdin = "",
                    lang = language,
                    cmd_line_args = "",
                    input_method = "I",
                    type = "M",
                    replay_events = Array.Empty<object>(),
                    ss_id = (string?)null
                };

                string shareJson = JsonSerializer.Serialize(shareRequestData, options);
                var shareContent = new StringContent(shareJson, Encoding.UTF8, "application/json");

                // Second POST request to generate the share link
                HttpResponseMessage shareResponse = await client.PostAsync(generateShareUrl, shareContent);
                shareResponse.EnsureSuccessStatusCode();

                using var shareStream = await shareResponse.Content.ReadAsStreamAsync();
                Stream decompressedShareStream = shareStream;

                if (shareResponse.Content.Headers.ContentEncoding.Contains("gzip"))
                {
                    decompressedShareStream = new GZipStream(shareStream, CompressionMode.Decompress);
                }
                else if (shareResponse.Content.Headers.ContentEncoding.Contains("br"))
                {
                    decompressedShareStream = new BrotliStream(shareStream, CompressionMode.Decompress);
                }

                using var shareReader = new StreamReader(decompressedShareStream, Encoding.UTF8);
                string shareResponseBody = await shareReader.ReadToEndAsync();

                Console.WriteLine("Share link response:");
                Console.WriteLine(shareResponseBody);

                // Deserialize the second response to extract the new UID for the share link
                var shareData = JsonSerializer.Deserialize<ShareResponse>(shareResponseBody);

                if (shareData != null && shareData.Result == "OK" && !string.IsNullOrEmpty(shareData.Uid))
                {
                    // Display the final share link in the console
                    string finalLink = $"https://www.onlinegdb.com/fork/{shareData.Uid}";
                    Console.WriteLine($"Your share link: {finalLink}");
                }
                else
                {
                    Console.WriteLine("Could not retrieve share UID from response.");
                }
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
