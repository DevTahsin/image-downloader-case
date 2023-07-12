using System;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;

namespace AdCreative.Parallelism;
class Program
{
    const string INPUT_FILE = "Input.json";
    const string DOWNLOAD_URL = "https://picsum.photos/200/300";
    static async Task Main(string[] args)
    {
        Input input = new Input();

        // input fields filled by the user
        InputHandle(input);

        // if the output folder is not exist, create it
        if (!Directory.Exists(input.SavePath))
        {
            Directory.CreateDirectory(input.SavePath);
        }
        // if the output folder is exist, ask the user to clear it
        else
        {
            Console.WriteLine("Output folder already exists. If you continue, the folder will be cleared.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Directory.Delete(input.SavePath, true);
            // create the output folder
            Directory.CreateDirectory(input.SavePath);
        }

        // total downloaded images is 0 at the beginning
        int totalDownloaded = 0;
        // while process is going user press ctrl+c to stop the process and clear the output folder
        Console.CancelKeyPress += delegate
        {
            Console.WriteLine("Process is stopped by user.");
            Console.WriteLine("Clearing output folder...");
            Directory.Delete(input.SavePath, true);
            Console.WriteLine("Done.");
        };

        // download images with parallelism using Parallel.For
        Parallel.For(0, input.Count, new ParallelOptions { MaxDegreeOfParallelism = input.Parallelism }, async (i) =>
        {
            DownloadImage downloadImage = new DownloadImage();
            downloadImage.DownloadUrl = DOWNLOAD_URL;
            // set the delegate to show the progress
            downloadImage.DelDownloadImageInstance += () =>
            {
                totalDownloaded++;
                // clear the console and show the progress
                Console.Clear();
                Console.WriteLine($"Downloading {input.Count} images ({input.Parallelism}) parallel downloads at most");
                Console.WriteLine($"Progress: {totalDownloaded}/{input.Count}");
                // if all images are downloaded, show the message and exit
                if (totalDownloaded == input.Count)
                {
                    Console.WriteLine("All images are downloaded.");
                    Console.WriteLine("Press any key to exit...");
                }

            };
            // download the image
            await downloadImage.Download($"{input.SavePath}/{i + 1}");
        });

        Console.ReadKey();
    }


    /// <summary>
    /// Handle the input file or custom prompt
    /// </summary>
    /// <param name="input"> input object to fill the fields </param>
    static void InputHandle(Input input)
    {
        // file is not exist or json is not a valid json then enter the prompt mode
        if (!File.Exists(INPUT_FILE) || input.FromJson(File.ReadAllText(INPUT_FILE)) == false)
        {
            Console.WriteLine(INPUT_FILE + " is not a valid input.");
            Console.WriteLine("Entering custom prompt mode.");
            int count = default;
            do
            {
                Console.WriteLine("Enter the number of images to download:");
            } while (int.TryParse(Console.ReadLine(), out count) == false);
            input.Count = count;

            int parallelism = default;
            do
            {
                Console.WriteLine("Enter the maximum parallel download limit:");
            } while (int.TryParse(Console.ReadLine(), out parallelism) == false);
            input.Parallelism = parallelism;

            string path = default;
            Console.WriteLine("Enter the save path (default: ./outputs)");
            path = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "./outputs";
            }
            input.SavePath = path;
        }
    }
}

/// <summary>
/// Download image from URL using HttpClient with event and delegate methodolgy for feedback
/// </summary>
public class DownloadImage
{
    // url of the image
    public string DownloadUrl { get; set; }
    // event and delegate for feedback
    public delegate void DelDownloadImage();
    public DelDownloadImage DelDownloadImageInstance;


    /// <summary>
    /// download image and save it to the given path
    /// </summary>
    /// <param name="fileName">file name for the image</param>
    /// <returns></returns>
    public async Task Download(string fileName)
    {

        HttpClient client = new HttpClient();

        // download image and show current progress with ProgressBar using event and delegate
        using (HttpResponseMessage response = client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead).Result)
        {
            // ensure the response is successful
            response.EnsureSuccessStatusCode();
            string fileExtension = response.Content.Headers.ContentType.MediaType.Split('/')[1];
            // get the image from the response as a stream
            using (Stream inputStream = await response.Content.ReadAsStreamAsync())
            {
                // save the image to the given path using FileStream
                using (Stream outputStream = File.OpenWrite(fileName + "." + fileExtension))
                {
                    // copy the image stream to the file stream
                    await inputStream.CopyToAsync(outputStream);
                }
            }

            if (DelDownloadImageInstance != null)
            {
                // call the delegate after the image is downloaded
                DelDownloadImageInstance();
            }
        }
    }
}



public class Input
{
    public int Count { get; set; }
    public int Parallelism { get; set; }
    public string? SavePath { get; set; }

    public Input()
    {
        Count = default;
        Parallelism = default;
        SavePath = null;
    }

    public bool FromJson(string json)
    {
        try
        {
            Input input = JsonSerializer.Deserialize<Input>(json);
            if (input == null)
            {
                return false;
            }
            if (input.Count <= 0 || input.Parallelism <= 0 || input.SavePath == null)
            {
                return false;
            }
            Count = input.Count;
            Parallelism = input.Parallelism;
            SavePath = input.SavePath;
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return false;
        }
    }

}